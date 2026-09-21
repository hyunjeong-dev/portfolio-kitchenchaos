using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    sealed class SteamCloudSaveService : IPlatformCloudSaveService
    {
        const string CLOUD_FILE_NAME = "portfolio_kitchen_chaos_game_save.json";
        const int MAX_SAVE_FILE_SIZE = 1024 * 1024;

        static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(10);

        public bool IsAvailable { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        CallResult<RemoteStorageFileReadAsyncComplete_t> _readCallResult;
        CallResult<RemoteStorageFileWriteAsyncComplete_t> _writeCallResult;
        UniTaskCompletionSource<PlatformCloudReadResult> _readCompletionSource;
        UniTaskCompletionSource<PlatformOperationResult> _writeCompletionSource;
        SteamAPICall_t _pendingReadApiCall = SteamAPICall_t.Invalid;
        byte[] _pendingWriteData;
        int _pendingReadSize;
        bool _isInitialized;
        bool _isShutdown;

        public void Initialize()
        {
            if (_isInitialized || _isShutdown)
            {
                return;
            }

            _isInitialized = true;
            _readCallResult =
                CallResult<RemoteStorageFileReadAsyncComplete_t>.Create(OnFileRead);
            _writeCallResult =
                CallResult<RemoteStorageFileWriteAsyncComplete_t>.Create(OnFileWritten);

            try
            {
                if (!SteamRemoteStorage.IsCloudEnabledForAccount())
                {
                    FailInitialization("Steam Cloud is disabled for the current account.");
                    return;
                }

                if (!SteamRemoteStorage.IsCloudEnabledForApp())
                {
                    FailInitialization("Steam Cloud is disabled for App ID 480.");
                    return;
                }

                IsAvailable = true;
                FailureReason = string.Empty;
                SteamLog.Info("Cloud", $"Ready. File: {CLOUD_FILE_NAME}");
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Cloud", "Initialize", exception);
                FailInitialization("An exception occurred while checking Steam Cloud availability.");
            }
        }

        public async UniTask<PlatformCloudReadResult> ReadAsync(
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformCloudReadResult.Failure(PlatformOperationResult.Cancelled());
            }

            if (!IsAvailable)
            {
                return PlatformCloudReadResult.Failure(
                    PlatformOperationResult.Unavailable(FailureReason));
            }

            if (_readCompletionSource != null)
            {
                const string errorMessage = "Another cloud save read is already pending.";
                SteamLog.Warning("Cloud", errorMessage);
                return PlatformCloudReadResult.Failure(
                    PlatformOperationResult.ApiFailure(errorMessage));
            }

            try
            {
                if (!SteamRemoteStorage.FileExists(CLOUD_FILE_NAME))
                {
                    SteamLog.Info("Cloud", "No remote save file found.");
                    return PlatformCloudReadResult.NotFound();
                }

                var fileSize = SteamRemoteStorage.GetFileSize(CLOUD_FILE_NAME);
                if (fileSize <= 0 || fileSize > MAX_SAVE_FILE_SIZE)
                {
                    var errorMessage =
                        $"Remote save size is invalid. Bytes: {fileSize}, Max: {MAX_SAVE_FILE_SIZE}.";
                    SteamLog.Warning("Cloud", errorMessage);
                    return PlatformCloudReadResult.Failure(
                        PlatformOperationResult.InvalidData(errorMessage));
                }

                var completionSource =
                    new UniTaskCompletionSource<PlatformCloudReadResult>();
                _readCompletionSource = completionSource;
                _pendingReadSize = fileSize;
                _pendingReadApiCall = SteamRemoteStorage.FileReadAsync(
                    CLOUD_FILE_NAME,
                    0,
                    (uint)fileSize);

                if (_pendingReadApiCall == SteamAPICall_t.Invalid)
                {
                    return CompleteReadFailure(
                        PlatformOperationResult.ApiFailure(
                            "SteamRemoteStorage.FileReadAsync returned an invalid API call."));
                }

                _readCallResult.Set(_pendingReadApiCall);
                WatchReadTimeoutAsync(completionSource).Forget(SteamLogUnhandledException);
                return await AwaitReadAsync(completionSource.Task, cancellationToken);
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Cloud", "Read", exception);
                return CompleteReadFailure(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while reading the cloud save."));
            }
        }

        public async UniTask<PlatformOperationResult> WriteAsync(
            byte[] data,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformOperationResult.Cancelled();
            }

            if (!IsAvailable)
            {
                return PlatformOperationResult.Unavailable(FailureReason);
            }

            if (data == null || data.Length == 0 || data.Length > MAX_SAVE_FILE_SIZE)
            {
                var dataLength = data?.Length ?? 0;
                var errorMessage =
                    $"Cloud save data size is invalid. Bytes: {dataLength}, Max: {MAX_SAVE_FILE_SIZE}.";
                SteamLog.Warning("Cloud", errorMessage);
                return PlatformOperationResult.InvalidData(errorMessage);
            }

            if (_writeCompletionSource != null)
            {
                const string errorMessage = "Another cloud save write is already pending.";
                SteamLog.Warning("Cloud", errorMessage);
                return PlatformOperationResult.ApiFailure(errorMessage);
            }

            var completionSource = new UniTaskCompletionSource<PlatformOperationResult>();
            _writeCompletionSource = completionSource;
            _pendingWriteData = data;

            try
            {
                var apiCall = SteamRemoteStorage.FileWriteAsync(
                    CLOUD_FILE_NAME,
                    _pendingWriteData,
                    (uint)_pendingWriteData.Length);
                if (apiCall == SteamAPICall_t.Invalid)
                {
                    return CompleteWrite(
                        PlatformOperationResult.ApiFailure(
                            "SteamRemoteStorage.FileWriteAsync returned an invalid API call."));
                }

                _writeCallResult.Set(apiCall);
                WatchWriteTimeoutAsync(completionSource).Forget(SteamLogUnhandledException);
                return await SteamAsyncUtility.AwaitOperationAsync(
                    completionSource.Task,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Cloud", "Write", exception);
                return CompleteWrite(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while writing the cloud save."));
            }
        }

        public void Shutdown()
        {
            if (_isShutdown)
            {
                return;
            }

            _isShutdown = true;
            _isInitialized = false;
            StopAcceptingRequests();
            _readCallResult?.Dispose();
            _writeCallResult?.Dispose();
            _readCallResult = null;
            _writeCallResult = null;
            CompleteReadFailure(PlatformOperationResult.Cancelled());
            CompleteWrite(PlatformOperationResult.Cancelled());
        }

        internal void StopAcceptingRequests()
        {
            IsAvailable = false;
            FailureReason = "Cloud save service was shut down.";
        }

        void OnFileRead(RemoteStorageFileReadAsyncComplete_t callback, bool ioFailure)
        {
            if (_readCompletionSource == null)
            {
                return;
            }

            if (ioFailure || callback.m_eResult != EResult.k_EResultOK ||
                callback.m_hFileReadAsync != _pendingReadApiCall ||
                callback.m_nOffset != 0 || callback.m_cubRead != _pendingReadSize)
            {
                CompleteReadFailure(
                    PlatformOperationResult.ApiFailure(
                        $"Cloud read callback failed. Result: {callback.m_eResult}, " +
                        $"IO failure: {ioFailure}."));
                return;
            }

            try
            {
                var data = new byte[callback.m_cubRead];
                if (!SteamRemoteStorage.FileReadAsyncComplete(
                        callback.m_hFileReadAsync,
                        data,
                        callback.m_cubRead))
                {
                    CompleteReadFailure(
                        PlatformOperationResult.ApiFailure(
                            "SteamRemoteStorage.FileReadAsyncComplete returned false."));
                    return;
                }

                SteamLog.Info("Cloud", $"Loaded. Bytes: {data.Length}");
                CompleteRead(PlatformCloudReadResult.Success(data));
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Cloud", "Read callback", exception);
                CompleteReadFailure(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while completing the cloud read."));
            }
        }

        void OnFileWritten(RemoteStorageFileWriteAsyncComplete_t callback, bool ioFailure)
        {
            if (_writeCompletionSource == null)
            {
                return;
            }

            if (ioFailure || callback.m_eResult != EResult.k_EResultOK)
            {
                CompleteWrite(
                    PlatformOperationResult.ApiFailure(
                        $"Cloud write callback failed. Result: {callback.m_eResult}, " +
                        $"IO failure: {ioFailure}."));
                return;
            }

            var byteCount = _pendingWriteData?.Length ?? 0;
            SteamLog.Info("Cloud", $"Saved. Bytes: {byteCount}");
            CompleteWrite(PlatformOperationResult.Success());
        }

        async UniTask WatchReadTimeoutAsync(
            UniTaskCompletionSource<PlatformCloudReadResult> completionSource)
        {
            await UniTask.Delay(_requestTimeout, DelayType.Realtime);
            if (_readCompletionSource != completionSource)
            {
                return;
            }

            _readCallResult.Cancel();
            CompleteReadFailure(
                PlatformOperationResult.Timeout("Cloud read callback timed out."));
        }

        async UniTask WatchWriteTimeoutAsync(
            UniTaskCompletionSource<PlatformOperationResult> completionSource)
        {
            await UniTask.Delay(_requestTimeout, DelayType.Realtime);
            if (_writeCompletionSource != completionSource)
            {
                return;
            }

            _writeCallResult.Cancel();
            CompleteWrite(PlatformOperationResult.Timeout("Cloud write callback timed out."));
        }

        PlatformCloudReadResult CompleteRead(PlatformCloudReadResult result)
        {
            var completionSource = _readCompletionSource;
            _readCompletionSource = null;
            _pendingReadApiCall = SteamAPICall_t.Invalid;
            _pendingReadSize = 0;
            completionSource?.TrySetResult(result);
            return result;
        }

        PlatformCloudReadResult CompleteReadFailure(PlatformOperationResult operation)
        {
            LogOperationFailure("Read", operation);
            return CompleteRead(PlatformCloudReadResult.Failure(operation));
        }

        PlatformOperationResult CompleteWrite(PlatformOperationResult result)
        {
            LogOperationFailure("Write", result);
            var completionSource = _writeCompletionSource;
            _writeCompletionSource = null;
            _pendingWriteData = null;
            completionSource?.TrySetResult(result);
            return result;
        }

        void FailInitialization(string failureReason)
        {
            IsAvailable = false;
            FailureReason = failureReason;
            SteamLog.Warning("Cloud", $"Unavailable. {FailureReason}");
        }

        static void LogOperationFailure(string operation, PlatformOperationResult result)
        {
            if (result.Status == PlatformOperationStatus.Success ||
                result.Status == PlatformOperationStatus.Cancelled)
            {
                return;
            }

            SteamLog.Warning(
                "Cloud",
                $"{operation} failed. Status: {result.Status}, Reason: {result.ErrorMessage}");
        }

        static async UniTask<PlatformCloudReadResult> AwaitReadAsync(
            UniTask<PlatformCloudReadResult> task,
            CancellationToken cancellationToken)
        {
            try
            {
                return await task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return PlatformCloudReadResult.Failure(PlatformOperationResult.Cancelled());
            }
        }

        static void SteamLogUnhandledException(Exception exception)
        {
            SteamLog.Exception("Cloud", "Timeout watcher", exception);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    sealed class SteamLeaderboardService : IPlatformLeaderboardService
    {
        delegate bool GetDownloadedLeaderboardEntryDelegate(
            SteamLeaderboardEntries_t entries,
            int index,
            out LeaderboardEntry_t entry,
            int[] details,
            int detailsMax);

        const string LEADERBOARD_NAME_PREFIX = "PORTFOLIO_KITCHEN_CHAOS_STAGE_";
        const string LEADERBOARD_NAME_SUFFIX = "_SCORE_V1";
        const int MAX_DOWNLOAD_ENTRY_COUNT = 100;

        static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(10);
        static readonly int[] _emptyDetails = Array.Empty<int>();

        public bool IsAvailable { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        readonly Dictionary<int, SteamLeaderboard_t> _leaderboardHandles = new();

        CallResult<LeaderboardFindResult_t> _findCallResult;
        CallResult<LeaderboardScoreUploaded_t> _uploadCallResult;
        CallResult<LeaderboardScoresDownloaded_t> _downloadCallResult;
        UniTaskCompletionSource<PlatformOperationResult> _findCompletionSource;
        UniTaskCompletionSource<PlatformOperationResult> _uploadCompletionSource;
        UniTaskCompletionSource<PlatformLeaderboardEntriesResult> _downloadCompletionSource;
        Func<string, ELeaderboardSortMethod, ELeaderboardDisplayType, SteamAPICall_t>
            _findOrCreateLeaderboard;
        Func<SteamLeaderboard_t, ELeaderboardUploadScoreMethod, int, int[], int, SteamAPICall_t>
            _uploadLeaderboardScore;
        Func<SteamLeaderboard_t, ELeaderboardDataRequest, int, int, SteamAPICall_t>
            _downloadLeaderboardEntries;
        Func<SteamLeaderboard_t, ELeaderboardSortMethod> _getLeaderboardSortMethod;
        Func<SteamLeaderboard_t, ELeaderboardDisplayType> _getLeaderboardDisplayType;
        GetDownloadedLeaderboardEntryDelegate _getDownloadedLeaderboardEntry;
        Func<CSteamID> _getCurrentUserId;
        Func<string> _getPersonaName;
        Func<CSteamID, string> _getFriendPersonaName;
        Func<CSteamID, bool, bool> _requestUserInformation;
        Action<SteamAPICall_t> _setFindCallResult;
        Action<SteamAPICall_t> _setUploadCallResult;
        Action<SteamAPICall_t> _setDownloadCallResult;
        Action _cancelFindCallResult;
        Action _cancelUploadCallResult;
        Action _cancelDownloadCallResult;
        SteamLeaderboard_t _pendingUploadHandle;
        SteamLeaderboard_t _pendingDownloadHandle;
        int _pendingFindStageLevel;
        int _pendingUploadStageLevel;
        int _pendingUploadScore;
        int _pendingDownloadStageLevel;
        ELeaderboardDataRequest _pendingDownloadRequestType;
        string _pendingFindLeaderboardName = string.Empty;
        bool _isInitialized;
        bool _isShutdown;

        public void Initialize()
        {
            if (_isInitialized || _isShutdown)
            {
                return;
            }

            _findOrCreateLeaderboard = SteamUserStats.FindOrCreateLeaderboard;
            _uploadLeaderboardScore = SteamUserStats.UploadLeaderboardScore;
            _downloadLeaderboardEntries = SteamUserStats.DownloadLeaderboardEntries;
            _getLeaderboardSortMethod = SteamUserStats.GetLeaderboardSortMethod;
            _getLeaderboardDisplayType = SteamUserStats.GetLeaderboardDisplayType;
            _getDownloadedLeaderboardEntry = SteamUserStats.GetDownloadedLeaderboardEntry;
            _getCurrentUserId = SteamUser.GetSteamID;
            _getPersonaName = SteamFriends.GetPersonaName;
            _getFriendPersonaName = SteamFriends.GetFriendPersonaName;
            _requestUserInformation = SteamFriends.RequestUserInformation;
            _setFindCallResult = SetFindCallResult;
            _setUploadCallResult = SetUploadCallResult;
            _setDownloadCallResult = SetDownloadCallResult;
            _cancelFindCallResult = () => _findCallResult?.Cancel();
            _cancelUploadCallResult = () => _uploadCallResult?.Cancel();
            _cancelDownloadCallResult = () => _downloadCallResult?.Cancel();
            IsAvailable = true;
            FailureReason = string.Empty;
            _isInitialized = true;
        }

        public async UniTask<PlatformOperationResult> SubmitStageScoreAsync(
            int stageLevel,
            int stageScore,
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

            if (stageLevel <= 0 || stageScore < 0)
            {
                var invalidDataResult = PlatformOperationResult.InvalidData(
                    $"Leaderboard values are outside the supported range. " +
                    $"Stage: 1 or higher, Score: 0-{int.MaxValue}.");
                LogOperationFailure("Submit", invalidDataResult);
                return invalidDataResult;
            }

            var findResult = await FindAsync(stageLevel, cancellationToken);
            if (!findResult.IsSuccess)
            {
                return findResult;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformOperationResult.Cancelled();
            }

            if (!IsAvailable)
            {
                return PlatformOperationResult.Unavailable(FailureReason);
            }

            if (!_leaderboardHandles.TryGetValue(stageLevel, out var leaderboardHandle))
            {
                return PlatformOperationResult.ApiFailure(
                    $"Stage leaderboard handle was not cached. Stage: {stageLevel}.");
            }

            if (_uploadCompletionSource != null)
            {
                const string errorMessage = "Another leaderboard score upload is already pending.";
                SteamLog.Warning("Leaderboard", errorMessage);
                return PlatformOperationResult.ApiFailure(errorMessage);
            }

            var completionSource = new UniTaskCompletionSource<PlatformOperationResult>();
            _uploadCompletionSource = completionSource;
            _pendingUploadHandle = leaderboardHandle;
            _pendingUploadStageLevel = stageLevel;
            _pendingUploadScore = stageScore;

            try
            {
                var apiCall = _uploadLeaderboardScore(
                    leaderboardHandle,
                    ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
                    stageScore,
                    _emptyDetails,
                    0);

                if (apiCall == SteamAPICall_t.Invalid)
                {
                    return CompleteUpload(
                        PlatformOperationResult.ApiFailure(
                            "SteamUserStats.UploadLeaderboardScore returned an invalid API call."));
                }

                _setUploadCallResult(apiCall);
                WatchUploadTimeoutAsync(completionSource).Forget(SteamLogUnhandledException);
                return await SteamAsyncUtility.AwaitOperationAsync(
                    completionSource.Task,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Leaderboard", "Submit", exception);
                return CompleteUpload(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while submitting the leaderboard score."));
            }
        }

        public async UniTask<PlatformLeaderboardEntriesResult> GetStageEntriesAsync(
            int stageLevel,
            int startRank,
            int endRank,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Cancelled());
            }

            if (!IsAvailable)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Unavailable(FailureReason));
            }

            if (stageLevel <= 0 || startRank <= 0 || endRank < startRank ||
                endRank - startRank + 1 > MAX_DOWNLOAD_ENTRY_COUNT)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.InvalidData(
                        $"Stage must be 1 or higher. Global rank range must start at 1 and contain at most " +
                        $"{MAX_DOWNLOAD_ENTRY_COUNT} entries."));
            }

            return await DownloadEntriesAsync(
                stageLevel,
                ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
                startRank,
                endRank,
                cancellationToken);
        }

        public async UniTask<PlatformLeaderboardEntryResult> GetCurrentUserEntryAsync(
            int stageLevel,
            CancellationToken cancellationToken)
        {
            var entriesResult = await DownloadEntriesAsync(
                stageLevel,
                ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobalAroundUser,
                0,
                0,
                cancellationToken);
            if (!entriesResult.IsSuccess)
            {
                return PlatformLeaderboardEntryResult.Failure(
                    stageLevel,
                    entriesResult.Operation);
            }

            for (var i = 0; i < entriesResult.Entries.Count; i++)
            {
                var entry = entriesResult.Entries[i];
                if (entry.IsCurrentUser)
                {
                    return PlatformLeaderboardEntryResult.Success(stageLevel, entry);
                }
            }

            return PlatformLeaderboardEntryResult.NotFound(stageLevel);
        }

        async UniTask<PlatformLeaderboardEntriesResult> DownloadEntriesAsync(
            int stageLevel,
            ELeaderboardDataRequest requestType,
            int startRank,
            int endRank,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Cancelled());
            }

            if (!IsAvailable)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Unavailable(FailureReason));
            }

            if (stageLevel <= 0)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.InvalidData("Stage must be 1 or higher."));
            }

            var findResult = await FindAsync(stageLevel, cancellationToken);
            if (!findResult.IsSuccess)
            {
                return PlatformLeaderboardEntriesResult.Failure(stageLevel, findResult);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Cancelled());
            }

            if (!IsAvailable)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Unavailable(FailureReason));
            }

            if (!_leaderboardHandles.TryGetValue(stageLevel, out var leaderboardHandle))
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.ApiFailure(
                        $"Stage leaderboard handle was not cached. Stage: {stageLevel}."));
            }

            if (_downloadCompletionSource != null)
            {
                const string errorMessage = "Another leaderboard download is already pending.";
                SteamLog.Warning("Leaderboard", errorMessage);
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.ApiFailure(errorMessage));
            }

            var completionSource =
                new UniTaskCompletionSource<PlatformLeaderboardEntriesResult>();
            _downloadCompletionSource = completionSource;
            _pendingDownloadHandle = leaderboardHandle;
            _pendingDownloadStageLevel = stageLevel;
            _pendingDownloadRequestType = requestType;

            try
            {
                var apiCall = _downloadLeaderboardEntries(
                    leaderboardHandle,
                    requestType,
                    startRank,
                    endRank);

                if (apiCall == SteamAPICall_t.Invalid)
                {
                    return CompleteDownloadFailure(
                        PlatformOperationResult.ApiFailure(
                            "SteamUserStats.DownloadLeaderboardEntries returned an invalid API call."));
                }

                _setDownloadCallResult(apiCall);
                WatchDownloadTimeoutAsync(completionSource).Forget(SteamLogUnhandledException);
                return await AwaitEntriesAsync(completionSource.Task, cancellationToken, stageLevel);
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Leaderboard", "Download", exception);
                return CompleteDownloadFailure(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while downloading leaderboard entries."));
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
            _findCallResult?.Dispose();
            _uploadCallResult?.Dispose();
            _downloadCallResult?.Dispose();
            _findCallResult = null;
            _uploadCallResult = null;
            _downloadCallResult = null;
            _leaderboardHandles.Clear();
            CompleteFind(PlatformOperationResult.Cancelled());
            CompleteUpload(PlatformOperationResult.Cancelled());
            CompleteDownloadFailure(PlatformOperationResult.Cancelled());
        }

        internal void StopAcceptingRequests()
        {
            IsAvailable = false;
            FailureReason = "Leaderboard service was shut down.";
        }

        internal static string GetLeaderboardName(int stageLevel)
        {
            return LEADERBOARD_NAME_PREFIX +
                   stageLevel.ToString(CultureInfo.InvariantCulture) +
                   LEADERBOARD_NAME_SUFFIX;
        }

        async UniTask<PlatformOperationResult> FindAsync(
            int stageLevel,
            CancellationToken cancellationToken)
        {
            if (_leaderboardHandles.TryGetValue(stageLevel, out var cachedHandle) &&
                cachedHandle.m_SteamLeaderboard != 0)
            {
                return PlatformOperationResult.Success();
            }

            var completionSource = _findCompletionSource;
            if (completionSource != null)
            {
                if (_pendingFindStageLevel != stageLevel)
                {
                    return PlatformOperationResult.ApiFailure(
                        $"Another stage leaderboard find is already pending. " +
                        $"Pending: {_pendingFindStageLevel}, Requested: {stageLevel}.");
                }
            }
            else
            {
                completionSource = new UniTaskCompletionSource<PlatformOperationResult>();
                _findCompletionSource = completionSource;
                _pendingFindStageLevel = stageLevel;
                _pendingFindLeaderboardName = GetLeaderboardName(stageLevel);
                StartFind(completionSource);
            }

            return await SteamAsyncUtility.AwaitOperationAsync(
                completionSource.Task,
                cancellationToken);
        }

        void StartFind(UniTaskCompletionSource<PlatformOperationResult> completionSource)
        {
            try
            {
                var apiCall = _findOrCreateLeaderboard(
                    _pendingFindLeaderboardName,
                    ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
                    ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric);

                if (apiCall == SteamAPICall_t.Invalid)
                {
                    CompleteFind(
                        PlatformOperationResult.ApiFailure(
                            "SteamUserStats.FindOrCreateLeaderboard returned an invalid API call."));
                    return;
                }

                _setFindCallResult(apiCall);
                WatchFindTimeoutAsync(completionSource).Forget(SteamLogUnhandledException);
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Leaderboard", "Find", exception);
                CompleteFind(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while finding the leaderboard."));
            }
        }

        void SetFindCallResult(SteamAPICall_t apiCall)
        {
            _findCallResult?.Dispose();
            _findCallResult = CallResult<LeaderboardFindResult_t>.Create(OnLeaderboardFound);
            _findCallResult.Set(apiCall);
        }

        void SetUploadCallResult(SteamAPICall_t apiCall)
        {
            _uploadCallResult?.Dispose();
            _uploadCallResult = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);
            _uploadCallResult.Set(apiCall);
        }

        void SetDownloadCallResult(SteamAPICall_t apiCall)
        {
            // Steamworks.NET dispatcher는 callback 반환 후 기존 CallResult를 unregister한다.
            // callback continuation에서 다음 요청이 즉시 시작돼도 영향을 받지 않도록 요청마다 새 인스턴스를 사용한다.
            _downloadCallResult?.Dispose();
            _downloadCallResult =
                CallResult<LeaderboardScoresDownloaded_t>.Create(OnScoresDownloaded);
            _downloadCallResult.Set(apiCall);
        }

        void OnLeaderboardFound(LeaderboardFindResult_t callback, bool ioFailure)
        {
            if (_findCompletionSource == null)
            {
                return;
            }

            if (ioFailure || callback.m_bLeaderboardFound == 0 ||
                callback.m_hSteamLeaderboard.m_SteamLeaderboard == 0)
            {
                CompleteFind(
                    PlatformOperationResult.ApiFailure(
                        $"Leaderboard find failed. IO failure: {ioFailure}."));
                return;
            }

            try
            {
                var sortMethod = _getLeaderboardSortMethod(callback.m_hSteamLeaderboard);
                var displayType = _getLeaderboardDisplayType(callback.m_hSteamLeaderboard);
                if (sortMethod != ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending ||
                    displayType != ELeaderboardDisplayType.k_ELeaderboardDisplayTypeNumeric)
                {
                    CompleteFind(
                        PlatformOperationResult.ApiFailure(
                            $"Leaderboard configuration does not match. " +
                            $"Sort: {sortMethod}, Display: {displayType}."));
                    return;
                }

                _leaderboardHandles[_pendingFindStageLevel] = callback.m_hSteamLeaderboard;
                SteamLog.Info("Leaderboard", $"Ready. Name: {_pendingFindLeaderboardName}");
                CompleteFind(PlatformOperationResult.Success());
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Leaderboard", "Find callback", exception);
                CompleteFind(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while processing the leaderboard handle."));
            }
        }

        void OnScoreUploaded(LeaderboardScoreUploaded_t callback, bool ioFailure)
        {
            if (_uploadCompletionSource == null)
            {
                return;
            }

            if (ioFailure || callback.m_bSuccess == 0 ||
                callback.m_hSteamLeaderboard != _pendingUploadHandle)
            {
                CompleteUpload(
                    PlatformOperationResult.ApiFailure(
                        $"Leaderboard upload failed. IO failure: {ioFailure}."));
                return;
            }

            SteamLog.Info(
                "Leaderboard",
                $"Submitted. Stage: {_pendingUploadStageLevel}, Score: {_pendingUploadScore}, " +
                $"Changed: {callback.m_bScoreChanged != 0}, Rank: {callback.m_nGlobalRankNew}");
            CompleteUpload(PlatformOperationResult.Success());
        }

        void OnScoresDownloaded(LeaderboardScoresDownloaded_t callback, bool ioFailure)
        {
            if (_downloadCompletionSource == null)
            {
                return;
            }

            if (ioFailure || callback.m_hSteamLeaderboard != _pendingDownloadHandle ||
                callback.m_cEntryCount < 0)
            {
                CompleteDownloadFailure(
                    PlatformOperationResult.ApiFailure(
                        $"Leaderboard download failed. IO failure: {ioFailure}."));
                return;
            }

            try
            {
                var currentUserId = _getCurrentUserId();
                var entries = new PlatformLeaderboardEntry[callback.m_cEntryCount];
                var validEntryCount = 0;

                for (var i = 0; i < callback.m_cEntryCount; i++)
                {
                    if (!_getDownloadedLeaderboardEntry(
                            callback.m_hSteamLeaderboardEntries,
                            i,
                            out var steamEntry,
                            _emptyDetails,
                            0))
                    {
                        continue;
                    }

                    entries[validEntryCount++] = new PlatformLeaderboardEntry(
                        steamEntry.m_steamIDUser.ToString(),
                        ResolveUserName(steamEntry.m_steamIDUser, currentUserId),
                        steamEntry.m_nGlobalRank,
                        steamEntry.m_nScore,
                        steamEntry.m_steamIDUser == currentUserId);
                }

                if (validEntryCount != entries.Length)
                {
                    Array.Resize(ref entries, validEntryCount);
                }

                SteamLog.Info(
                    "Leaderboard",
                    $"Downloaded. Stage: {_pendingDownloadStageLevel}, " +
                    $"Request: {_pendingDownloadRequestType}, Entries: {entries.Length}");
                CompleteDownload(
                    PlatformLeaderboardEntriesResult.Success(
                        _pendingDownloadStageLevel,
                        entries));
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Leaderboard", "Download callback", exception);
                CompleteDownloadFailure(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while reading leaderboard entries."));
            }
        }

        string ResolveUserName(CSteamID userId, CSteamID currentUserId)
        {
            if (userId == currentUserId)
            {
                return NormalizeUserName(_getPersonaName(), userId);
            }

            var userName = _getFriendPersonaName(userId);
            if (IsUnknownUserName(userName))
            {
                _requestUserInformation(userId, true);
            }

            return NormalizeUserName(userName, userId);
        }

        static string NormalizeUserName(string userName, CSteamID userId)
        {
            return IsUnknownUserName(userName)
                ? $"Steam User {userId}"
                : userName;
        }

        static bool IsUnknownUserName(string userName)
        {
            return string.IsNullOrWhiteSpace(userName) || userName == "[unknown]";
        }

        async UniTask WatchFindTimeoutAsync(
            UniTaskCompletionSource<PlatformOperationResult> completionSource)
        {
            await UniTask.Delay(_requestTimeout, DelayType.Realtime);
            if (_findCompletionSource != completionSource)
            {
                return;
            }

            _cancelFindCallResult?.Invoke();
            CompleteFind(PlatformOperationResult.Timeout("Leaderboard find callback timed out."));
        }

        async UniTask WatchUploadTimeoutAsync(
            UniTaskCompletionSource<PlatformOperationResult> completionSource)
        {
            await UniTask.Delay(_requestTimeout, DelayType.Realtime);
            if (_uploadCompletionSource != completionSource)
            {
                return;
            }

            _cancelUploadCallResult?.Invoke();
            CompleteUpload(PlatformOperationResult.Timeout("Leaderboard upload callback timed out."));
        }

        async UniTask WatchDownloadTimeoutAsync(
            UniTaskCompletionSource<PlatformLeaderboardEntriesResult> completionSource)
        {
            await UniTask.Delay(_requestTimeout, DelayType.Realtime);
            if (_downloadCompletionSource != completionSource)
            {
                return;
            }

            _cancelDownloadCallResult?.Invoke();
            CompleteDownloadFailure(
                PlatformOperationResult.Timeout("Leaderboard download callback timed out."));
        }

        PlatformOperationResult CompleteFind(PlatformOperationResult result)
        {
            LogOperationFailure("Find", result);
            var completionSource = _findCompletionSource;
            _findCompletionSource = null;
            _pendingFindStageLevel = 0;
            _pendingFindLeaderboardName = string.Empty;
            completionSource?.TrySetResult(result);
            return result;
        }

        PlatformOperationResult CompleteUpload(PlatformOperationResult result)
        {
            LogOperationFailure("Submit", result);
            var completionSource = _uploadCompletionSource;
            _uploadCompletionSource = null;
            _pendingUploadHandle = default;
            _pendingUploadStageLevel = 0;
            _pendingUploadScore = 0;
            completionSource?.TrySetResult(result);
            return result;
        }

        PlatformLeaderboardEntriesResult CompleteDownload(
            PlatformLeaderboardEntriesResult result)
        {
            var completionSource = _downloadCompletionSource;
            _downloadCompletionSource = null;
            _pendingDownloadHandle = default;
            _pendingDownloadStageLevel = 0;
            _pendingDownloadRequestType = default;
            completionSource?.TrySetResult(result);
            return result;
        }

        PlatformLeaderboardEntriesResult CompleteDownloadFailure(
            PlatformOperationResult operation)
        {
            LogOperationFailure("Download", operation);
            return CompleteDownload(
                PlatformLeaderboardEntriesResult.Failure(
                    _pendingDownloadStageLevel,
                    operation));
        }

        static void LogOperationFailure(string operation, PlatformOperationResult result)
        {
            if (result.Status == PlatformOperationStatus.Success ||
                result.Status == PlatformOperationStatus.Cancelled)
            {
                return;
            }

            SteamLog.Warning(
                "Leaderboard",
                $"{operation} failed. Status: {result.Status}, Reason: {result.ErrorMessage}");
        }

        static async UniTask<PlatformLeaderboardEntriesResult> AwaitEntriesAsync(
            UniTask<PlatformLeaderboardEntriesResult> task,
            CancellationToken cancellationToken,
            int stageLevel)
        {
            try
            {
                return await task.AttachExternalCancellation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return PlatformLeaderboardEntriesResult.Failure(
                    stageLevel,
                    PlatformOperationResult.Cancelled());
            }
        }

        static void SteamLogUnhandledException(Exception exception)
        {
            SteamLog.Exception("Leaderboard", "Timeout watcher", exception);
        }
    }
}

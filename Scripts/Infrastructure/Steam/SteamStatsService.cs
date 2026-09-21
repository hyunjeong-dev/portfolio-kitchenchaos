using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    sealed class SteamStatsService : IPlatformStatsService
    {
        static readonly TimeSpan _storeTimeout = TimeSpan.FromSeconds(10);

        public bool IsAvailable { get; private set; }
        public bool IsDirty { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        Callback<UserStatsStored_t> _userStatsStoredCallback;
        UniTaskCompletionSource<PlatformOperationResult> _storeCompletionSource;
        ulong _gameId;
        ulong _changeVersion;
        ulong _storeVersion;
        int _totalStagePlayCount;
        int _totalStageClearCount;
        int _totalStageFailCount;
        bool _isInitialized;
        bool _isShutdown;
        bool _isStoreCallbackPending;

        public void Initialize(AppId_t appId)
        {
            if (_isInitialized || _isShutdown)
            {
                return;
            }

            _isInitialized = true;

            try
            {
                _gameId = new CGameID(appId).m_GameID;
                _userStatsStoredCallback = Callback<UserStatsStored_t>.Create(OnUserStatsStored);

                if (!TryReloadValues())
                {
                    FailInitialization(
                        "Spacewar current-user stats could not be read in this session. " +
                        "SteamUserStats.GetStat returned false, so Stats are disabled while the game continues.");
                    return;
                }

                IsAvailable = true;
                FailureReason = string.Empty;
                SteamLog.Info(
                    "Stats",
                    $"Loaded. Plays: {_totalStagePlayCount}, Clears: {_totalStageClearCount}, " +
                    $"Fails: {_totalStageFailCount}");
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Stats", "Load", exception);
                FailInitialization("An exception occurred while loading Spacewar stats.");
            }
        }

        public bool TryGetInt(PlatformStatId statId, out int value)
        {
            if (!IsAvailable)
            {
                value = 0;
                return false;
            }

            switch (statId)
            {
                case PlatformStatId.TotalStagePlayCount:
                    value = _totalStagePlayCount;
                    return true;
                case PlatformStatId.TotalStageClearCount:
                    value = _totalStageClearCount;
                    return true;
                case PlatformStatId.TotalStageFailCount:
                    value = _totalStageFailCount;
                    return true;
                default:
                    value = 0;
                    return false;
            }
        }

        public bool TrySetInt(PlatformStatId statId, int value)
        {
            if (!IsAvailable || !SteamStatId.TryGetApiName(statId, out var apiName))
            {
                return false;
            }

            try
            {
                if (!SteamUserStats.SetStat(apiName, value))
                {
                    SteamLog.Warning("Stats", $"SetStat rejected. Stat: {statId}, Value: {value}");
                    return false;
                }

                SetCachedValue(statId, value);
                MarkDirty();
                SteamLog.Info("Stats", $"Updated. Stat: {statId}, Value: {value}");
                return true;
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Stats", $"Set {statId}", exception);
                return false;
            }
        }

        public bool TryIncrementInt(PlatformStatId statId, int amount = 1)
        {
            if (amount <= 0 || !TryGetInt(statId, out var currentValue))
            {
                return false;
            }

            var nextValue = (long)currentValue + amount;
            if (nextValue > int.MaxValue)
            {
                SteamLog.Warning("Stats", $"Increment overflow prevented. Stat: {statId}");
                return false;
            }

            return TrySetInt(statId, (int)nextValue);
        }

        public async UniTask<PlatformOperationResult> StoreAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PlatformOperationResult.Cancelled();
            }

            if (!IsAvailable)
            {
                return PlatformOperationResult.Unavailable(FailureReason);
            }

            while (IsDirty)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return PlatformOperationResult.Cancelled();
                }

                if (!IsAvailable)
                {
                    return PlatformOperationResult.Unavailable(FailureReason);
                }

                if (_storeCompletionSource == null && _isStoreCallbackPending)
                {
                    return PlatformOperationResult.Timeout(
                        "A previous StoreStats callback is still pending. Retry is deferred.");
                }

                var task = _storeCompletionSource != null
                    ? _storeCompletionSource.Task
                    : StartStoreAsync();
                var result = await SteamAsyncUtility.AwaitOperationAsync(task, cancellationToken);
                if (!result.IsSuccess)
                {
                    return result;
                }
            }

            return PlatformOperationResult.Success();
        }

        public void MarkDirty()
        {
            if (IsAvailable)
            {
                _changeVersion++;
                IsDirty = true;
            }
        }

        public void Shutdown(bool tryFinalStore = true)
        {
            if (_isShutdown)
            {
                return;
            }

            _isShutdown = true;
            StopAcceptingRequests();
            if (tryFinalStore && _isInitialized)
            {
                TryStoreBeforeShutdown();
            }
            _isStoreCallbackPending = false;
            _userStatsStoredCallback?.Dispose();
            _userStatsStoredCallback = null;
            IsDirty = false;
            _isInitialized = false;
            CompleteStore(PlatformOperationResult.Cancelled());
        }

        internal void StopAcceptingRequests()
        {
            IsAvailable = false;
            FailureReason = "Stats service was shut down.";
        }

        UniTask<PlatformOperationResult> StartStoreAsync()
        {
            var completionSource = new UniTaskCompletionSource<PlatformOperationResult>();
            _storeCompletionSource = completionSource;
            _storeVersion = _changeVersion;
            _isStoreCallbackPending = true;

            try
            {
                if (!SteamUserStats.StoreStats())
                {
                    CompleteStoreApiFailure("SteamUserStats.StoreStats returned false.");
                    return completionSource.Task;
                }
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Stats", "Store", exception);
                CompleteStoreApiFailure("An exception occurred while storing Steam stats.");
                return completionSource.Task;
            }

            WatchStoreTimeoutAsync(completionSource).Forget(
                exception => SteamLog.Exception("Stats", "Store timeout watcher", exception));
            return completionSource.Task;
        }

        async UniTask WatchStoreTimeoutAsync(
            UniTaskCompletionSource<PlatformOperationResult> completionSource)
        {
            await UniTask.Delay(_storeTimeout, DelayType.Realtime);
            if (_storeCompletionSource != completionSource)
            {
                return;
            }

            // StoreStats callback에는 요청 ID가 없으므로 늦은 응답 전까지 재요청은 막는다.
            SteamLog.Warning("Stats", "Store callback timed out. Dirty values are retained.");
            CompleteStore(PlatformOperationResult.Timeout("StoreStats callback timed out."));
        }

        void OnUserStatsStored(UserStatsStored_t callback)
        {
            try
            {
                if (callback.m_nGameID != _gameId || !_isStoreCallbackPending)
                {
                    return;
                }

                _isStoreCallbackPending = false;

                if (callback.m_eResult == EResult.k_EResultOK)
                {
                    IsDirty = _changeVersion != _storeVersion;
                    SteamLog.Info("Stats", "Stored.");
                    CompleteStore(PlatformOperationResult.Success());
                    return;
                }

                if (callback.m_eResult == EResult.k_EResultInvalidParam)
                {
                    TryReloadValues();
                }

                var errorMessage = $"Store callback failed. Result: {callback.m_eResult}";
                SteamLog.Warning("Stats", errorMessage);
                CompleteStore(PlatformOperationResult.ApiFailure(errorMessage));
            }
            catch (Exception exception)
            {
                _isStoreCallbackPending = false;
                const string errorMessage = "An exception occurred while processing the StoreStats callback.";
                SteamLog.Exception("Stats", "Store callback", exception);
                CompleteStore(PlatformOperationResult.ApiFailure(errorMessage));
            }
        }

        bool TryReloadValues()
        {
            if (!TryReadValue(PlatformStatId.TotalStagePlayCount, out var totalStagePlayCount) ||
                !TryReadValue(PlatformStatId.TotalStageClearCount, out var totalStageClearCount) ||
                !TryReadValue(PlatformStatId.TotalStageFailCount, out var totalStageFailCount))
            {
                return false;
            }

            _totalStagePlayCount = totalStagePlayCount;
            _totalStageClearCount = totalStageClearCount;
            _totalStageFailCount = totalStageFailCount;
            IsDirty = false;
            return true;
        }

        void FailInitialization(string failureReason)
        {
            IsAvailable = false;
            FailureReason = failureReason;
            SteamLog.Warning("Stats", $"Unavailable. {FailureReason}");
        }

        static bool TryReadValue(PlatformStatId statId, out int value)
        {
            value = 0;
            return SteamStatId.TryGetApiName(statId, out var apiName) &&
                   SteamUserStats.GetStat(apiName, out value);
        }

        PlatformOperationResult CompleteStoreApiFailure(string errorMessage)
        {
            _isStoreCallbackPending = false;
            SteamLog.Warning("Stats", errorMessage);
            return CompleteStore(PlatformOperationResult.ApiFailure(errorMessage));
        }

        PlatformOperationResult CompleteStore(PlatformOperationResult result)
        {
            var completionSource = _storeCompletionSource;
            _storeCompletionSource = null;
            completionSource?.TrySetResult(result);
            return result;
        }

        void TryStoreBeforeShutdown()
        {
            if (!IsDirty || _isStoreCallbackPending)
            {
                return;
            }

            try
            {
                if (SteamUserStats.StoreStats())
                {
                    SteamLog.Info("Stats", "Final store requested before shutdown.");
                    return;
                }

                SteamLog.Warning("Stats", "Final StoreStats request was rejected during shutdown.");
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Stats", "Final store", exception);
            }
        }

        void SetCachedValue(PlatformStatId statId, int value)
        {
            switch (statId)
            {
                case PlatformStatId.TotalStagePlayCount:
                    _totalStagePlayCount = value;
                    break;
                case PlatformStatId.TotalStageClearCount:
                    _totalStageClearCount = value;
                    break;
                case PlatformStatId.TotalStageFailCount:
                    _totalStageFailCount = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(statId), statId, null);
            }
        }
    }
}

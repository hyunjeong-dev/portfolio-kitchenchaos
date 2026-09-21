using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    sealed class SteamAchievementService : IPlatformAchievementService
    {
        static readonly TimeSpan _storeTimeout = TimeSpan.FromSeconds(10);

        readonly SteamStatsService _statsService;

        public bool IsAvailable { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        Callback<UserAchievementStored_t> _achievementStoredCallback;
        UniTaskCompletionSource<PlatformOperationResult> _storeCompletionSource;
        PlatformAchievementId _pendingAchievementId;
        ulong _gameId;
        bool _isFirstStageClearUnlocked;
        bool _isStageClear100Unlocked;
        bool _isInitialized;
        bool _isShutdown;
        bool _hasPendingStore;

        public SteamAchievementService(SteamStatsService statsService)
        {
            _statsService = statsService;
        }

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
                _achievementStoredCallback = Callback<UserAchievementStored_t>.Create(OnAchievementStored);

                if (!_statsService.IsAvailable ||
                    !TryReadUnlocked(PlatformAchievementId.FirstStageClear, out _isFirstStageClearUnlocked) ||
                    !TryReadUnlocked(PlatformAchievementId.StageClear100, out _isStageClear100Unlocked))
                {
                    FailInitialization(
                        "Spacewar achievement states could not be read. " +
                        "Achievements are disabled for this session.");
                    return;
                }

                IsAvailable = true;
                FailureReason = string.Empty;
                SteamLog.Info(
                    "Achievement",
                    $"Loaded. FirstStageClear: {_isFirstStageClearUnlocked}, " +
                    $"StageClear100: {_isStageClear100Unlocked}");
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Achievement", "Load", exception);
                FailInitialization("An exception occurred while loading Spacewar achievements.");
            }
        }

        public bool TryGetUnlocked(PlatformAchievementId achievementId, out bool isUnlocked)
        {
            if (!IsAvailable)
            {
                isUnlocked = false;
                return false;
            }

            switch (achievementId)
            {
                case PlatformAchievementId.FirstStageClear:
                    isUnlocked = _isFirstStageClearUnlocked;
                    return true;
                case PlatformAchievementId.StageClear100:
                    isUnlocked = _isStageClear100Unlocked;
                    return true;
                default:
                    isUnlocked = false;
                    return false;
            }
        }

        public async UniTask<PlatformOperationResult> UnlockAsync(
            PlatformAchievementId achievementId,
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

            if (TryGetUnlocked(achievementId, out var isUnlocked) && isUnlocked)
            {
                return PlatformOperationResult.Success();
            }

            if (_hasPendingStore)
            {
                if (_pendingAchievementId != achievementId)
                {
                    var errorMessage = $"Another achievement store is already pending: {_pendingAchievementId}";
                    SteamLog.Warning("Achievement", errorMessage);
                    return PlatformOperationResult.ApiFailure(errorMessage);
                }

                return await SteamAsyncUtility.AwaitOperationAsync(
                    _storeCompletionSource.Task,
                    cancellationToken);
            }

            return await SteamAsyncUtility.AwaitOperationAsync(
                StartUnlockAsync(achievementId),
                cancellationToken);
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
            _achievementStoredCallback?.Dispose();
            _achievementStoredCallback = null;
            CompletePendingStore(PlatformOperationResult.Cancelled());
        }

        internal void StopAcceptingRequests()
        {
            IsAvailable = false;
            FailureReason = "Achievement service was shut down.";
        }

        async UniTask<PlatformOperationResult> StartUnlockAsync(
            PlatformAchievementId achievementId)
        {
            if (!SteamAchievementId.TryGetApiName(achievementId, out var apiName))
            {
                var errorMessage = $"Achievement ID is not mapped: {achievementId}";
                SteamLog.Warning("Achievement", errorMessage);
                return PlatformOperationResult.ApiFailure(errorMessage);
            }

            var achievementStoreCompletionSource = new UniTaskCompletionSource<PlatformOperationResult>();
            _storeCompletionSource = achievementStoreCompletionSource;
            _pendingAchievementId = achievementId;
            _hasPendingStore = true;

            try
            {
                if (!SteamUserStats.SetAchievement(apiName))
                {
                    SteamLog.Warning("Achievement", $"SetAchievement rejected: {apiName}");
                    return CompletePendingStore(
                        PlatformOperationResult.ApiFailure($"SetAchievement rejected: {apiName}"));
                }
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Achievement", $"Unlock {apiName}", exception);
                return CompletePendingStore(
                    PlatformOperationResult.ApiFailure($"An exception occurred while unlocking {apiName}."));
            }

            _statsService.MarkDirty();
            var statsStoreResult = await _statsService.StoreAsync(CancellationToken.None);
            if (!statsStoreResult.IsSuccess)
            {
                if (statsStoreResult.Status != PlatformOperationStatus.Cancelled)
                {
                    SteamLog.Warning(
                        "Achievement",
                        $"Store failed. Achievement: {achievementId}, Status: {statsStoreResult.Status}");
                    if (_storeCompletionSource == achievementStoreCompletionSource)
                    {
                        CompletePendingStore(statsStoreResult);
                    }
                }

                return statsStoreResult;
            }

            return await AwaitAchievementStoredAsync(achievementStoreCompletionSource);
        }

        async UniTask<PlatformOperationResult> AwaitAchievementStoredAsync(
            UniTaskCompletionSource<PlatformOperationResult> completionSource)
        {
            var timeoutTask = UniTask.Delay(_storeTimeout, DelayType.Realtime);
            var (hasResult, result) = await UniTask.WhenAny(completionSource.Task, timeoutTask);
            if (hasResult)
            {
                return result;
            }

            var timeoutResult = PlatformOperationResult.Timeout("Achievement store callback timed out.");
            if (_storeCompletionSource == completionSource)
            {
                CompletePendingStore(timeoutResult);
            }
            SteamLog.Warning("Achievement", "Store callback timed out.");
            return timeoutResult;
        }

        void OnAchievementStored(UserAchievementStored_t callback)
        {
            try
            {
                if (callback.m_nGameID != _gameId ||
                    callback.m_nCurProgress != 0 ||
                    callback.m_nMaxProgress != 0 ||
                    !SteamAchievementId.TryGetPlatformId(callback.m_rgchAchievementName, out var achievementId))
                {
                    return;
                }

                SetUnlocked(achievementId);
                SteamLog.Info(
                    "Achievement",
                    $"Unlocked. Achievement: {achievementId}, ApiName: {callback.m_rgchAchievementName}");

                if (_hasPendingStore && _pendingAchievementId == achievementId)
                {
                    CompletePendingStore(PlatformOperationResult.Success());
                }
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Achievement", "Store callback", exception);
                CompletePendingStore(
                    PlatformOperationResult.ApiFailure(
                        "An exception occurred while processing the achievement callback."));
            }
        }

        static bool TryReadUnlocked(PlatformAchievementId achievementId, out bool isUnlocked)
        {
            isUnlocked = false;
            return SteamAchievementId.TryGetApiName(achievementId, out var apiName) &&
                   SteamUserStats.GetAchievement(apiName, out isUnlocked);
        }

        void FailInitialization(string failureReason)
        {
            IsAvailable = false;
            FailureReason = failureReason;
            SteamLog.Warning("Achievement", $"Unavailable. {FailureReason}");
        }

        PlatformOperationResult CompletePendingStore(PlatformOperationResult result)
        {
            var completionSource = _storeCompletionSource;
            _storeCompletionSource = null;
            _pendingAchievementId = default;
            _hasPendingStore = false;
            // UniTask는 결과 전달 중 호출자를 즉시 재개할 수 있으므로 상태부터 비운다.
            completionSource?.TrySetResult(result);
            return result;
        }

        void SetUnlocked(PlatformAchievementId achievementId)
        {
            switch (achievementId)
            {
                case PlatformAchievementId.FirstStageClear:
                    _isFirstStageClearUnlocked = true;
                    break;
                case PlatformAchievementId.StageClear100:
                    _isStageClear100Unlocked = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(achievementId), achievementId, null);
            }
        }
    }
}

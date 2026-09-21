using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    public sealed class SteamPlatformService : IPlatformService
    {
        public const uint SPACEWAR_APP_ID = 480;

        readonly SteamUserService _userService = new();
        readonly SteamOverlayService _overlayService = new();
        readonly SteamStatsService _statsService = new();
        readonly SteamAchievementService _achievementService;
        readonly SteamLeaderboardService _leaderboardService = new();
        readonly SteamCloudSaveService _cloudSaveService = new();

        public PlatformState State { get; private set; } = PlatformState.None;
        public bool IsAvailable => !_isShuttingDown && State == PlatformState.Ready;
        public string FailureReason { get; private set; } = string.Empty;
        public IPlatformUserService User => _userService;
        public IPlatformOverlayService Overlay => _overlayService;
        public IPlatformStatsService Stats => _statsService;
        public IPlatformAchievementService Achievements => _achievementService;
        public IPlatformLeaderboardService Leaderboards => _leaderboardService;
        public IPlatformCloudSaveService CloudSave => _cloudSaveService;

        public event Action<PlatformState> OnStateChanged;

        Callback<SteamShutdown_t> _steamShutdownCallback;
        bool _isSteamInitialized;
        bool _isSteamShutdownRequested;
        bool _isShuttingDown;

        public SteamPlatformService()
        {
            _achievementService = new SteamAchievementService(_statsService);
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_isShuttingDown)
            {
                return UniTask.CompletedTask;
            }

            switch (State)
            {
                case PlatformState.None:
                    break;
                case PlatformState.Initializing:
                case PlatformState.Ready:
                case PlatformState.Failed:
                    return UniTask.CompletedTask;
                case PlatformState.Shutdown:
                    FailureReason = "A shutdown Steam service cannot be initialized again.";
                    return UniTask.CompletedTask;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ChangeState(PlatformState.Initializing);

            try
            {
                if (!Packsize.Test())
                {
                    Fail("Steamworks.NET pack size test failed.");
                    return UniTask.CompletedTask;
                }

                if (!DllCheck.Test())
                {
                    Fail("Steamworks.NET native library compatibility test failed.");
                    return UniTask.CompletedTask;
                }

                var initResult = SteamAPI.InitEx(out var errorMessage);
                if (initResult != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    Fail($"SteamAPI.InitEx failed. result: {initResult}, reason: {errorMessage}");
                    return UniTask.CompletedTask;
                }

                _isSteamInitialized = true;
                _steamShutdownCallback = Callback<SteamShutdown_t>.Create(OnSteamShutdown);

                var activeAppId = SteamUtils.GetAppID().m_AppId;
                if (activeAppId != SPACEWAR_APP_ID)
                {
                    FailAndShutdown($"Unexpected App ID. expected: {SPACEWAR_APP_ID}, actual: {activeAppId}");
                    return UniTask.CompletedTask;
                }

                _userService.Refresh();
                _overlayService.Initialize();
                _statsService.Initialize(new AppId_t(activeAppId));
                _achievementService.Initialize(new AppId_t(activeAppId));
                _leaderboardService.Initialize();
                _cloudSaveService.Initialize();
                FailureReason = string.Empty;
                ChangeState(PlatformState.Ready);

                var user = _userService.CurrentUser;
                SteamLog.Info(
                    $"Initialized. AppId: {activeAppId}, LoggedOn: {user.IsLoggedOn}, " +
                    $"UserId: {user.UserId}, PersonaName: {user.DisplayName}");
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Initialize", exception);
                FailAndShutdown(exception.Message);
            }

            return UniTask.CompletedTask;
        }

        public void Tick()
        {
            if (_isShuttingDown || !_isSteamInitialized || State != PlatformState.Ready)
            {
                return;
            }

            try
            {
                if (!SteamAPI.IsSteamRunning())
                {
                    FailAndShutdown("Steam client is no longer running.");
                    return;
                }

                SteamAPI.RunCallbacks();

                if (_isSteamShutdownRequested)
                {
                    FailAndShutdown("Steam client requested shutdown.");
                }
            }
            catch (Exception exception)
            {
                SteamLog.Exception("Callback pump", exception);
                FailAndShutdown(exception.Message);
            }
        }

        public void Shutdown()
        {
            if (_isShuttingDown || State == PlatformState.Shutdown)
            {
                return;
            }

            ShutdownSteamApi(true);
            ChangeState(PlatformState.Shutdown);
            SteamLog.Info("Shutdown completed.");
        }

        void Fail(string reason)
        {
            FailureReason = reason;
            ChangeState(PlatformState.Failed);
            SteamLog.Warning($"Unavailable. Game continues with fallback. reason: {reason}");
        }

        void FailAndShutdown(string reason)
        {
            ShutdownSteamApi(false);
            Fail(reason);
        }

        void ShutdownSteamApi(bool tryFinalStatsStore)
        {
            _isShuttingDown = true;
            // 어느 요청의 취소가 호출자를 재개하더라도 다른 서비스에 새 요청을 넣지 못하게 한다.
            _cloudSaveService.StopAcceptingRequests();
            _leaderboardService.StopAcceptingRequests();
            _achievementService.StopAcceptingRequests();
            _statsService.StopAcceptingRequests();
            _overlayService.Shutdown();
            _userService.Clear();

            _steamShutdownCallback?.Dispose();
            _steamShutdownCallback = null;
            _isSteamShutdownRequested = false;

            _cloudSaveService.Shutdown();
            _leaderboardService.Shutdown();
            _achievementService.Shutdown();
            _statsService.Shutdown(tryFinalStatsStore);

            if (!_isSteamInitialized)
            {
                _isShuttingDown = false;
                return;
            }

            SteamAPI.Shutdown();
            _isSteamInitialized = false;
            _isShuttingDown = false;
        }

        void OnSteamShutdown(SteamShutdown_t callback)
        {
            _isSteamShutdownRequested = true;
        }

        void ChangeState(PlatformState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            OnStateChanged?.Invoke(State);
        }
    }
}

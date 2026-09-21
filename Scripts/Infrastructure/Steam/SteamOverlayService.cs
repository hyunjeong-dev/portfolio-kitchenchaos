using System;
using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    sealed class SteamOverlayService : IPlatformOverlayService
    {
        public bool IsEnabled
        {
            get
            {
                if (!_isInitialized)
                {
                    return false;
                }

                try
                {
                    return SteamUtils.IsOverlayEnabled();
                }
                catch (Exception exception)
                {
                    SteamLog.Exception("Overlay availability check", exception);
                    return false;
                }
            }
        }

        public bool IsActive { get; private set; }

        public event Action<bool> OnActiveChanged;

        Callback<GameOverlayActivated_t> _overlayActivatedCallback;
        bool _isInitialized;

        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            _overlayActivatedCallback = Callback<GameOverlayActivated_t>.Create(OnOverlayActivated);
            _isInitialized = true;
        }

        public void Shutdown()
        {
            if (!_isInitialized)
            {
                return;
            }

            _overlayActivatedCallback?.Dispose();
            _overlayActivatedCallback = null;
            _isInitialized = false;
            IsActive = false;
        }

        public bool TryOpen(PlatformOverlayTarget target)
        {
            if (!IsEnabled)
            {
                SteamLog.Warning("Overlay is not available yet.");
                return false;
            }

            SteamFriends.ActivateGameOverlay(GetDialogName(target));
            return true;
        }

        void OnOverlayActivated(GameOverlayActivated_t callback)
        {
            IsActive = callback.m_bActive != 0;
            SteamLog.Info($"Overlay active: {IsActive}");
            OnActiveChanged?.Invoke(IsActive);
        }

        static string GetDialogName(PlatformOverlayTarget target)
        {
            return target switch
            {
                PlatformOverlayTarget.Community => "Community",
                PlatformOverlayTarget.Stats => "Stats",
                PlatformOverlayTarget.Achievements => "Achievements",
                _ => "Friends"
            };
        }
    }
}

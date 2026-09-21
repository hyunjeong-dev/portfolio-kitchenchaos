using Game.Common.Platform;
using Steamworks;

namespace Game.Infrastructure.Platform.Steam
{
    sealed class SteamUserService : IPlatformUserService
    {
        public PlatformUserData CurrentUser { get; private set; } = PlatformUserData.Unavailable;

        public void Refresh()
        {
            var isLoggedOn = SteamUser.BLoggedOn();
            if (!isLoggedOn)
            {
                CurrentUser = PlatformUserData.Unavailable;
                return;
            }

            var steamId = SteamUser.GetSteamID();
            CurrentUser = new PlatformUserData(
                steamId.m_SteamID.ToString(),
                SteamFriends.GetPersonaName(),
                true);
        }

        public void Clear()
        {
            CurrentUser = PlatformUserData.Unavailable;
        }
    }
}

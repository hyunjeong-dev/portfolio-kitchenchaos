using System;
using Game.Common.Platform;

namespace Game.Infrastructure.Platform.Steam
{
    static class SteamAchievementId
    {
        const string SPACEWAR_WIN_ONE_GAME = "ACH_WIN_ONE_GAME";
        const string SPACEWAR_WIN_100_GAMES = "ACH_WIN_100_GAMES";

        public static bool TryGetApiName(PlatformAchievementId achievementId, out string apiName)
        {
            switch (achievementId)
            {
                case PlatformAchievementId.FirstStageClear:
                    apiName = SPACEWAR_WIN_ONE_GAME;
                    return true;
                case PlatformAchievementId.StageClear100:
                    apiName = SPACEWAR_WIN_100_GAMES;
                    return true;
                default:
                    apiName = string.Empty;
                    return false;
            }
        }

        public static bool TryGetPlatformId(string apiName, out PlatformAchievementId achievementId)
        {
            if (string.Equals(apiName, SPACEWAR_WIN_ONE_GAME, StringComparison.Ordinal))
            {
                achievementId = PlatformAchievementId.FirstStageClear;
                return true;
            }

            if (string.Equals(apiName, SPACEWAR_WIN_100_GAMES, StringComparison.Ordinal))
            {
                achievementId = PlatformAchievementId.StageClear100;
                return true;
            }

            achievementId = default;
            return false;
        }
    }
}

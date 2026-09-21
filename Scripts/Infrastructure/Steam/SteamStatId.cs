using Game.Common.Platform;

namespace Game.Infrastructure.Platform.Steam
{
    static class SteamStatId
    {
        const string SPACEWAR_NUM_GAMES = "NumGames";
        const string SPACEWAR_NUM_WINS = "NumWins";
        const string SPACEWAR_NUM_LOSSES = "NumLosses";

        public static bool TryGetApiName(PlatformStatId statId, out string apiName)
        {
            switch (statId)
            {
                case PlatformStatId.TotalStagePlayCount:
                    apiName = SPACEWAR_NUM_GAMES;
                    return true;
                case PlatformStatId.TotalStageClearCount:
                    apiName = SPACEWAR_NUM_WINS;
                    return true;
                case PlatformStatId.TotalStageFailCount:
                    apiName = SPACEWAR_NUM_LOSSES;
                    return true;
                default:
                    apiName = string.Empty;
                    return false;
            }
        }
    }
}
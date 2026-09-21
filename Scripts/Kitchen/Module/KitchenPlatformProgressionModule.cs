using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using UnityEngine;

public sealed class KitchenPlatformProgressionModule : KitchenGameModule
{
    const int STAGE_CLEAR_100_ACHIEVEMENT_THRESHOLD = 100;

    bool _hasRecordedStageResult;

    public override void OnBegin()
    {
        base.OnBegin();

        _hasRecordedStageResult = false;
        Context.Events.OnStageResultFinalized += OnStageResultFinalized;
    }

    public override void OnEnd()
    {
        Context.Events.OnStageResultFinalized -= OnStageResultFinalized;
        _hasRecordedStageResult = false;

        base.OnEnd();
    }

    void OnStageResultFinalized(KitchenStageResultData stageResult)
    {
        if (_hasRecordedStageResult || !stageResult.IsValid)
        {
            return;
        }

        _hasRecordedStageResult = true;

        var platformService = PlatformManager.Instance.Service;
        var cancellationToken = Context.GetCancellationTokenOnDestroy();
        if (platformService.Stats.IsAvailable)
        {
            UpdateProgressionAsync(
                    platformService.Stats,
                    platformService.Achievements,
                    stageResult,
                    cancellationToken)
                .Forget(Debug.LogException);
        }

        if (!stageResult.IsGoalAchieved || !platformService.Leaderboards.IsAvailable)
        {
            return;
        }

        platformService.Leaderboards.SubmitStageScoreAsync(
                stageResult.StageLevel,
                stageResult.FinalScore,
                cancellationToken)
            .Forget(Debug.LogException);
    }

    static async UniTask UpdateProgressionAsync(
        IPlatformStatsService statsService,
        IPlatformAchievementService achievementService,
        KitchenStageResultData stageResult,
        CancellationToken cancellationToken)
    {
        var hasChanges = statsService.TryIncrementInt(PlatformStatId.TotalStagePlayCount);
        hasChanges |= statsService.TryIncrementInt(
            stageResult.IsGoalAchieved
                ? PlatformStatId.TotalStageClearCount
                : PlatformStatId.TotalStageFailCount);

        if (!hasChanges)
        {
            return;
        }

        if (stageResult.IsGoalAchieved && achievementService.IsAvailable)
        {
            await achievementService.UnlockAsync(
                PlatformAchievementId.FirstStageClear,
                cancellationToken);

            if (statsService.TryGetInt(PlatformStatId.TotalStageClearCount, out var clearCount) &&
                clearCount >= STAGE_CLEAR_100_ACHIEVEMENT_THRESHOLD)
            {
                await achievementService.UnlockAsync(
                    PlatformAchievementId.StageClear100,
                    cancellationToken);
            }
        }

        await statsService.StoreAsync(cancellationToken);
    }
}

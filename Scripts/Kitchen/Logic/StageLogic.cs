using UnityEngine;

public static class StageLogic
{
    const int DEFAULT_STAGE_LEVEL = 1;

    public static int CurrentStageLevel => SaveDataManager.Instance?.CurrentStageLevel ?? DEFAULT_STAGE_LEVEL;
    
    public static int BestStageLevel => SaveDataManager.Instance?.BestStageLevel ?? 0;
    
    public static int GetNextStageLevel(int currentLevel)
    {
        if (!IsUnlocked(currentLevel))
            return currentLevel;
        
        return Mathf.Min(GameData.Instance.MaxStageLevel, currentLevel + 1);
    }
    
    public static bool IsUnlocked(int level)
    {
        return level <= BestStageLevel;
    }

    public static int GetBestScore(int stageLevel)
    {
        var saveDataManager = SaveDataManager.Instance;
        if (saveDataManager == null || !saveDataManager.TryGetStageSaveData(stageLevel, out var stageSaveData))
        {
            return 0;
        }

        return stageSaveData.BestScore;
    }

    public static int GetStageGrade(int level)
    {
        var bestScore = GetBestScore(level);
        return GetStageGrade(level, bestScore);
    }
    
    public static int GetStageGrade(int level, int currentScore)
    {
        var metadata = GameData.Instance.GetStageInfoDataByLevel(level);
        var grade = 0;
        var goalScores = metadata.GoalScores;
        for (var i = 0; i < goalScores.Length; i++)
        {
            if (currentScore >= goalScores[i])
            {
                grade++;
            }
        }

        return grade;
    }

    public static bool IsUnlockStage(int level)
    {
        if (level == 1) return true;

        return GetStageGrade(level - 1) > 0;
    }
}

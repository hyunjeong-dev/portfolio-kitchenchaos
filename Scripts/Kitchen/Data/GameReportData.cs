using static Generated.GameData;

public sealed class GameReportData
{
    public int SuccessfulMenuCount => _successfulMenuCount;
    public int FailedDeliveryCount => _failedDeliveryCount;
    public int ExpiredMenuCount => _expiredMenuCount;
    public int SuccessfulScore => _successfulScore;
    public int FailedScore => FailedDeliveryCount * _failedDeliveryScore + _expiredRecipePenaltyScore;
    public int MaxGoalScore => _maxGoalScore;
    public int CurrentScore => SuccessfulScore - FailedScore;
    public bool IsGoalAchieved => _hasGoalScore && CurrentScore >= _goalScore;

    bool _hasGoalScore;
    int _goalScore;
    int _maxGoalScore;
    int _failedDeliveryScore;
    int _successfulMenuCount;
    int _successfulScore;
    int _failedDeliveryCount;
    int _expiredMenuCount;
    int _expiredRecipePenaltyScore;

    public void Reset(StageInfoData stageData, int failedDeliveryScore)
    {
        SetGoalScores(stageData);
        _failedDeliveryScore = failedDeliveryScore;
        _successfulMenuCount = 0;
        _successfulScore = 0;
        _failedDeliveryCount = 0;
        _expiredMenuCount = 0;
        _expiredRecipePenaltyScore = 0;
    }

    public void RecordSuccessfulMenu(KitchenMenuData menuData)
    {
        if (menuData == null)
        {
            return;
        }

        _successfulMenuCount++;
        _successfulScore += menuData.Score;
    }

    public void RecordFailedDelivery()
    {
        _failedDeliveryCount++;
    }

    public void RecordExpiredMenu(KitchenMenuData menuData)
    {
        if (menuData == null)
        {
            return;
        }

        _expiredRecipePenaltyScore += menuData.ExpiredPenaltyScore;
        _expiredMenuCount++;
    }

    void SetGoalScores(StageInfoData stageData)
    {
        var goalScores = stageData != null ? stageData.GoalScores : null;
        if (goalScores == null || goalScores.Length == 0)
        {
            _hasGoalScore = false;
            _goalScore = 0;
            _maxGoalScore = 0;
            return;
        }

        _hasGoalScore = true;
        _goalScore = goalScores[0];
        var maxGoalScore = goalScores[0];
        for (var i = 1; i < goalScores.Length; i++)
        {
            if (maxGoalScore < goalScores[i])
            {
                maxGoalScore = goalScores[i];
            }
        }

        _maxGoalScore = maxGoalScore;
    }
}

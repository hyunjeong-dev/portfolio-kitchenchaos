public readonly struct KitchenStageResultData
{
    public int StageLevel { get; }
    public int FinalScore { get; }
    public int SuccessfulMenuCount { get; }
    public int FailedDeliveryCount { get; }
    public int ExpiredMenuCount { get; }
    public int SuccessfulScore { get; }
    public int FailedScore { get; }
    public int Grade { get; }
    public bool IsGoalAchieved { get; }
    public bool IsValid => StageLevel > 0;
    public int FailedOrderCount => FailedDeliveryCount + ExpiredMenuCount;

    public KitchenStageResultData(int stageLevel, GameReportData gameReportData)
    {
        StageLevel = stageLevel;
        FinalScore = gameReportData.CurrentScore;
        SuccessfulMenuCount = gameReportData.SuccessfulMenuCount;
        FailedDeliveryCount = gameReportData.FailedDeliveryCount;
        ExpiredMenuCount = gameReportData.ExpiredMenuCount;
        SuccessfulScore = gameReportData.SuccessfulScore;
        FailedScore = gameReportData.FailedScore;
        Grade = StageLogic.GetStageGrade(stageLevel, FinalScore);
        IsGoalAchieved = gameReportData.IsGoalAchieved;
    }
}

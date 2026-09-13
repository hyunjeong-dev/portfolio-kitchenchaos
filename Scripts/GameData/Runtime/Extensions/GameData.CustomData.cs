namespace Generated
{
    public partial class GameData
    {
        partial void OnBeforeLoad()
        {
            ClearCustomData();
        }

        void ClearCustomData()
        {
            ClearStageInfoDataCustomCache();
            ClearIngredientInfoDataCustomCache();
            ClearIngredientActionInfoDataCustomCache();
        }
    }
}

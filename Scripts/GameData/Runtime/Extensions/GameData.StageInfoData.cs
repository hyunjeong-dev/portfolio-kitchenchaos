using System.Collections.Generic;

namespace Generated
{
    public partial class GameData
    {
        readonly Dictionary<int, StageInfoData> _stageInfoDataByLevel = new();

        public int MaxStageLevel { get; private set; }

        public partial class StageInfoData : ICustomLoader
        {
            public void OnLoaded(GameData gameData)
            {
                gameData.RegisterStageInfoData(this);
            }
        }

        public StageInfoData GetStageInfoDataByLevel(int level)
        {
            if (_stageInfoDataByLevel.TryGetValue(level, out var data))
            {
                return data;
            }

            return null;
        }

        public void CollectStageInfoData(List<StageInfoData> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (var pair in DTStageInfoData)
            {
                results.Add(pair.Value);
            }
        }

        void ClearStageInfoDataCustomCache()
        {
            _stageInfoDataByLevel.Clear();
            MaxStageLevel = 0;
        }

        void RegisterStageInfoData(StageInfoData data)
        {
            if (data == null)
            {
                return;
            }

            _stageInfoDataByLevel[data.Level] = data;
            if (data.Level > MaxStageLevel)
            {
                MaxStageLevel = data.Level;
            }
        }
    }
}

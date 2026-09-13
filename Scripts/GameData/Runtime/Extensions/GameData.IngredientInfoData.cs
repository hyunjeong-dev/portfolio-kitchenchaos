using System;
using System.Collections.Generic;

namespace Generated
{
    public partial class GameData
    {
        readonly Dictionary<string, IngredientInfoData> _ingredientInfoDataByCode =
            new(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, IngredientInfoData> _ingredientInfoDataByResourceSuffix =
            new(StringComparer.OrdinalIgnoreCase);

        public partial class IngredientInfoData : ICustomLoader
        {
            public string PrefabResourceSuffix
            {
                get
                {
                    if (string.IsNullOrEmpty(PrefabPath))
                    {
                        return string.Empty;
                    }

                    var separatorIndex = PrefabPath.LastIndexOf('/');
                    return separatorIndex >= 0 && separatorIndex < PrefabPath.Length - 1
                        ? PrefabPath.Substring(separatorIndex + 1)
                        : PrefabPath;
                }
            }

            public void OnLoaded(GameData gameData)
            {
                gameData.RegisterIngredientInfoData(this);
            }
        }

        public IngredientInfoData GetIngredientInfoDataByCode(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            return _ingredientInfoDataByCode.TryGetValue(code, out var data) ? data : null;
        }

        public IngredientInfoData GetIngredientInfoDataByResourceSuffix(string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
            {
                return null;
            }

            return _ingredientInfoDataByResourceSuffix.TryGetValue(suffix, out var data) ? data : null;
        }

        public void CollectIngredientInfoDataIds(List<int> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            foreach (var pair in DTIngredientInfoData)
            {
                var ingredientInfoData = pair.Value;
                if (ingredientInfoData == null || string.IsNullOrWhiteSpace(ingredientInfoData.Code))
                {
                    continue;
                }

                results.Add(ingredientInfoData.IngredientId);
            }
        }

        void ClearIngredientInfoDataCustomCache()
        {
            _ingredientInfoDataByCode.Clear();
            _ingredientInfoDataByResourceSuffix.Clear();
        }

        void RegisterIngredientInfoData(IngredientInfoData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Code))
            {
                return;
            }

            _ingredientInfoDataByCode[data.Code] = data;
            if (!string.IsNullOrEmpty(data.PrefabResourceSuffix))
            {
                _ingredientInfoDataByResourceSuffix[data.PrefabResourceSuffix] = data;
            }
        }
    }
}

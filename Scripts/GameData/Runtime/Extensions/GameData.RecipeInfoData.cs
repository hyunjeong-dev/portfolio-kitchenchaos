using System.Collections.Generic;

namespace Generated
{
    public partial class GameData
    {
        public partial class RecipeInfoData
        {
            const float DEFAULT_DURATION_SECONDS = 30f;

            public string IconPath => $"Kitchen/UI/Icons/Recipes/ui_recipe_{Code}";
            public string PrefabPath => $"Kitchen/Prefab/Recipes/m_recipe_{Code}";
            public float DurationSeconds => Duration > 0 ? Duration : DEFAULT_DURATION_SECONDS;
            public int ExpiredPenaltyScore => Score / 2;
            public int RecipeGroupDataId => int.TryParse(RecipeGroupId, out var recipeGroupId) ? recipeGroupId : 0;

            public global::KitchenPlateCombinationKey GetPlateCombinationKey()
            {
                return new(IngredientIds);
            }
        }

        void CollectRecipeGroupIds(int[] recipeIds, List<int> results)
        {
            if (recipeIds == null || results == null)
            {
                return;
            }

            for (var i = 0; i < recipeIds.Length; i++)
            {
                var recipeData = GetRecipeInfoData(recipeIds[i]);
                if (recipeData == null || recipeData.RecipeGroupDataId == 0)
                {
                    continue;
                }

                AddUniqueRecipeGroupId(results, recipeData.RecipeGroupDataId);
            }
        }

        public void CollectStageRecipeGroupIds(StageInfoData stageData, List<int> results)
        {
            if (stageData == null)
            {
                return;
            }

            CollectRecipeGroupIds(stageData.RecipeIds, results);
        }

        static void AddUniqueRecipeGroupId(List<int> results, int recipeGroupId)
        {
            for (var i = 0; i < results.Count; i++)
            {
                if (results[i] == recipeGroupId)
                {
                    return;
                }
            }

            results.Add(recipeGroupId);
        }
    }
}

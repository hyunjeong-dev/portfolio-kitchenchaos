using System;
using System.Collections.Generic;

namespace Generated
{
    public partial class GameData
    {
        readonly Dictionary<IngredientActionKey, IngredientActionInfoData> _ingredientActionInfoDataByKey =
            new();
        readonly Dictionary<int, List<IngredientActionInfoData>> _ingredientActionInfoDataByIngredientId =
            new();

        public partial class IngredientActionInfoData : ICustomLoader
        {
            GameData _gameData;

            public ActionInfoData ActionInfo => _gameData != null ? _gameData.GetActionInfoData(ResultAction) : null;
            public int ActionValue => ActionInfo != null ? ActionInfo.ActionValue : 0;
            public void OnLoaded(GameData gameData)
            {
                _gameData = gameData;
                gameData.RegisterIngredientActionInfoData(this);
            }
        }

        public IngredientActionInfoData GetIngredientActionInfoData(
            int ingredientId,
            int recipeGroupId,
            ActionType resultAction)
        {
            return _ingredientActionInfoDataByKey.TryGetValue(
                new IngredientActionKey(ingredientId, recipeGroupId, resultAction),
                out var data)
                ? data
                : null;
        }

        public void CollectIngredientActionInfoData(
            int ingredientId,
            List<IngredientActionInfoData> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (!_ingredientActionInfoDataByIngredientId.TryGetValue(ingredientId, out var actionInfoDataList))
            {
                return;
            }

            for (var i = 0; i < actionInfoDataList.Count; i++)
            {
                results.Add(actionInfoDataList[i]);
            }
        }

        public bool HasIngredientAction(
            int ingredientId,
            IReadOnlyList<int> recipeGroupIds,
            ActionType resultAction)
        {
            if (recipeGroupIds == null || resultAction == ActionType.None)
            {
                return false;
            }

            for (var i = 0; i < recipeGroupIds.Count; i++)
            {
                if (GetIngredientActionInfoData(ingredientId, recipeGroupIds[i], resultAction) != null)
                {
                    return true;
                }
            }

            return false;
        }

        void ClearIngredientActionInfoDataCustomCache()
        {
            _ingredientActionInfoDataByKey.Clear();
            _ingredientActionInfoDataByIngredientId.Clear();
        }

        void RegisterIngredientActionInfoData(IngredientActionInfoData data)
        {
            if (data == null ||
                data.IngredientId == 0 ||
                data.RecipeGroupIds == null ||
                data.ResultAction == ActionType.None)
            {
                return;
            }

            if (!_ingredientActionInfoDataByIngredientId.TryGetValue(data.IngredientId, out var actionInfoDataList))
            {
                actionInfoDataList = new();
                _ingredientActionInfoDataByIngredientId.Add(data.IngredientId, actionInfoDataList);
            }

            actionInfoDataList.Add(data);

            for (var i = 0; i < data.RecipeGroupIds.Length; i++)
            {
                var recipeGroupId = data.RecipeGroupIds[i];
                if (recipeGroupId == 0)
                {
                    continue;
                }

                _ingredientActionInfoDataByKey[
                    new IngredientActionKey(data.IngredientId, recipeGroupId, data.ResultAction)] = data;
            }
        }

        readonly struct IngredientActionKey : IEquatable<IngredientActionKey>
        {
            readonly int _ingredientId;
            readonly int _recipeGroupId;
            readonly ActionType _resultAction;

            public IngredientActionKey(int ingredientId, int recipeGroupId, ActionType resultAction)
            {
                _ingredientId = ingredientId;
                _recipeGroupId = recipeGroupId;
                _resultAction = resultAction;
            }

            public bool Equals(IngredientActionKey other)
            {
                return _ingredientId == other._ingredientId &&
                       _recipeGroupId == other._recipeGroupId &&
                       _resultAction == other._resultAction;
            }

            public override bool Equals(object obj)
            {
                return obj is IngredientActionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _ingredientId;
                    hashCode = (hashCode * 397) ^ _recipeGroupId;
                    hashCode = (hashCode * 397) ^ (int)_resultAction;
                    return hashCode;
                }
            }
        }
    }
}

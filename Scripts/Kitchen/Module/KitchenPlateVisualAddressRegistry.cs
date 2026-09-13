using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Generated.GameData;

public sealed class KitchenPlateVisualAddressRegistry
{
    readonly List<string> _candidatePrefabAddresses = new();
    readonly Dictionary<KitchenPlateCombinationKey, string> _prefabAddressByKey = new();
    readonly HashSet<KitchenPlateCombinationKey> _recipeVisualKeys = new();

    public async UniTask BuildAsync(
        StageInfoData stageData,
        CancellationToken cancellationToken)
    {
        Clear();

        if (stageData == null || stageData.RecipeIds == null)
        {
            return;
        }

        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            var recipeData = GameData.Instance.GetRecipeInfoData(stageData.RecipeIds[i]);
            if (recipeData == null || recipeData.IngredientIds == null || recipeData.IngredientIds.Length == 0)
            {
                continue;
            }

            await RegisterRecipeVisualAsync(recipeData, cancellationToken);
            await RegisterIngredientCombinationVisualsAsync(recipeData.IngredientIds, cancellationToken);
        }
    }

    public void Clear()
    {
        _candidatePrefabAddresses.Clear();
        _prefabAddressByKey.Clear();
        _recipeVisualKeys.Clear();
    }

    public bool TryGetPrefabAddress(KitchenPlateCombinationKey combinationKey, out string prefabAddress)
    {
        prefabAddress = string.Empty;
        return combinationKey.IsValid &&
               _prefabAddressByKey.TryGetValue(combinationKey, out prefabAddress) &&
               !string.IsNullOrEmpty(prefabAddress);
    }

    public void AddPrefabAddressesTo(List<string> prefabAddresses)
    {
        if (prefabAddresses == null)
        {
            return;
        }

        foreach (var pair in _prefabAddressByKey)
        {
            KitchenCollectionLogic.AddUniqueAddress(prefabAddresses, pair.Value);
        }
    }

    async UniTask RegisterRecipeVisualAsync(
        RecipeInfoData recipeData,
        CancellationToken cancellationToken)
    {
        var key = recipeData.GetPlateCombinationKey();
        if (!key.IsValid || _recipeVisualKeys.Contains(key))
        {
            return;
        }

        var prefabAddress = recipeData.PrefabPath;
        if (string.IsNullOrEmpty(prefabAddress))
        {
            return;
        }

        if (!await AssetManager.Instance.ContainsLocationAsync<GameObject>(prefabAddress, cancellationToken))
        {
            Debug.LogWarning(
                $"[KitchenPlateVisualAddressRegistry] Recipe visual prefab address not found. " +
                $"recipeId: {recipeData.RecipeId}, address: {prefabAddress}");
            return;
        }

        _prefabAddressByKey[key] = prefabAddress;
        _recipeVisualKeys.Add(key);
    }

    async UniTask RegisterIngredientCombinationVisualsAsync(
        int[] recipeIngredientIds,
        CancellationToken cancellationToken)
    {
        var ingredientCount = recipeIngredientIds.Length;
        if (ingredientCount <= 0)
        {
            return;
        }

        using var _ = ListPool<int>.GetList(out var ingredientIds);
        var maxMask = 1 << ingredientCount;
        for (var mask = 1; mask < maxMask; mask++)
        {
            ingredientIds.Clear();
            for (var i = 0; i < ingredientCount; i++)
            {
                if ((mask & (1 << i)) == 0)
                {
                    continue;
                }

                ingredientIds.Add(recipeIngredientIds[i]);
            }

            var key = new KitchenPlateCombinationKey(ingredientIds);
            if (!key.IsValid || _prefabAddressByKey.ContainsKey(key))
            {
                continue;
            }

            var prefabAddress = await FindFirstExistingPrefabAddressAsync(ingredientIds, cancellationToken);
            if (!string.IsNullOrEmpty(prefabAddress))
            {
                _prefabAddressByKey.Add(key, prefabAddress);
                continue;
            }

            if (_candidatePrefabAddresses.Count > 0)
            {
                Debug.LogWarning(
                    $"[KitchenPlateVisualAddressRegistry] Plate visual prefab address not found. " +
                    $"firstCandidate: {_candidatePrefabAddresses[0]}");
            }
        }
    }

    async UniTask<string> FindFirstExistingPrefabAddressAsync(
        IReadOnlyList<int> ingredientIds,
        CancellationToken cancellationToken)
    {
        _candidatePrefabAddresses.Clear();
        KitchenPlateVisualAddressBuilder.CollectPrefabAddressCandidates(ingredientIds, _candidatePrefabAddresses);

        for (var i = 0; i < _candidatePrefabAddresses.Count; i++)
        {
            var candidateAddress = _candidatePrefabAddresses[i];
            if (await AssetManager.Instance.ContainsLocationAsync<GameObject>(candidateAddress, cancellationToken))
            {
                return candidateAddress;
            }
        }

        return string.Empty;
    }
}

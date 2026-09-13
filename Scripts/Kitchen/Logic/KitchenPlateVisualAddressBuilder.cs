using System.Collections.Generic;
using System.Text;

public static class KitchenPlateVisualAddressBuilder
{
    const string PLATED_PREFAB_ADDRESS_ROOT = "Kitchen/Prefab/Ingredients/";
    const string PLATED_PREFAB_ADDRESS_PREFIX = "m_plated_";
    const string DEFAULT_VARIANT_SUFFIX = "_01";

    public static void CollectPrefabAddressCandidates(IReadOnlyList<int> ingredientIds, List<string> results)
    {
        if (ingredientIds == null || ingredientIds.Count == 0 || results == null)
        {
            return;
        }

        using var codeScope = ListPool<string>.GetList(out var ingredientCodes);
        if (!TryCollectIngredientCodes(ingredientIds, ingredientCodes))
        {
            return;
        }

        using var selectedScope = ListPool<string>.GetList(out var selectedCodes);
        using var usedIndexScope = ListPool<int>.GetList(out var usedIndexes);
        CollectPermutationAddresses(ingredientCodes, selectedCodes, usedIndexes, results);
    }

    static bool TryCollectIngredientCodes(IReadOnlyList<int> ingredientIds, List<string> results)
    {
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientInfoData = GameData.Instance.GetIngredientInfoData(ingredientIds[i]);
            if (ingredientInfoData == null || string.IsNullOrEmpty(ingredientInfoData.Code))
            {
                results.Clear();
                return false;
            }

            results.Add(ingredientInfoData.Code);
        }

        return true;
    }

    static void CollectPermutationAddresses(
        IReadOnlyList<string> ingredientCodes,
        List<string> selectedCodes,
        List<int> usedIndexes,
        List<string> results)
    {
        if (selectedCodes.Count == ingredientCodes.Count)
        {
            AddAddressVariants(selectedCodes, results);
            return;
        }

        for (var i = 0; i < ingredientCodes.Count; i++)
        {
            if (ContainsIndex(usedIndexes, i))
            {
                continue;
            }

            usedIndexes.Add(i);
            selectedCodes.Add(ingredientCodes[i]);

            CollectPermutationAddresses(ingredientCodes, selectedCodes, usedIndexes, results);

            selectedCodes.RemoveAt(selectedCodes.Count - 1);
            usedIndexes.RemoveAt(usedIndexes.Count - 1);
        }
    }

    static void AddAddressVariants(IReadOnlyList<string> ingredientCodes, List<string> results)
    {
        AddUnique(results, BuildPrefabAddress(ingredientCodes, string.Empty));
        AddUnique(results, BuildPrefabAddress(ingredientCodes, DEFAULT_VARIANT_SUFFIX));
    }

    static string BuildPrefabAddress(IReadOnlyList<string> ingredientCodes, string suffix)
    {
        var builder = new StringBuilder(PLATED_PREFAB_ADDRESS_ROOT.Length + PLATED_PREFAB_ADDRESS_PREFIX.Length + 32);
        builder.Append(PLATED_PREFAB_ADDRESS_ROOT);
        builder.Append(PLATED_PREFAB_ADDRESS_PREFIX);

        for (var i = 0; i < ingredientCodes.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('_');
            }

            builder.Append(ingredientCodes[i]);
        }

        builder.Append(suffix);
        return builder.ToString();
    }

    static bool ContainsIndex(List<int> indexes, int index)
    {
        for (var i = 0; i < indexes.Count; i++)
        {
            if (indexes[i] == index)
            {
                return true;
            }
        }

        return false;
    }

    static void AddUnique(List<string> results, string value)
    {
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i] == value)
            {
                return;
            }
        }

        results.Add(value);
    }
}

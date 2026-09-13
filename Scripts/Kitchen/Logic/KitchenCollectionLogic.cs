using System.Collections.Generic;

public static class KitchenCollectionLogic
{
    public static void AddUniqueIngredientIds(List<int> results, int[] ingredientIds)
    {
        if (ingredientIds == null || results == null)
        {
            return;
        }

        for (var i = 0; i < ingredientIds.Length; i++)
        {
            var ingredientId = ingredientIds[i];
            if (!Contains(results, ingredientId))
            {
                results.Add(ingredientId);
            }
        }
    }

    public static void CopyIngredientIds(IReadOnlyList<int> ingredientIds, List<int> results)
    {
        if (ingredientIds == null || results == null)
        {
            return;
        }

        for (var i = 0; i < ingredientIds.Count; i++)
        {
            results.Add(ingredientIds[i]);
        }
    }

    public static void AddUniqueAddress(List<string> addresses, string address)
    {
        if (string.IsNullOrEmpty(address) || addresses == null)
        {
            return;
        }

        if (!Contains(addresses, address))
        {
            addresses.Add(address);
        }
    }

    public static bool Contains(int[] values, int value)
    {
        if (values == null)
        {
            return false;
        }

        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    static bool Contains<T>(IReadOnlyList<T> values, T value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(values[i], value))
            {
                return true;
            }
        }

        return false;
    }
}

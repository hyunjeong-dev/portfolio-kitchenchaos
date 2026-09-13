using System;
using System.Collections.Generic;

public readonly struct KitchenPlateCombinationKey : IEquatable<KitchenPlateCombinationKey>
{
    const int MAX_INGREDIENT_COUNT = 6;

    readonly int _count;
    readonly int _id0;
    readonly int _id1;
    readonly int _id2;
    readonly int _id3;
    readonly int _id4;
    readonly int _id5;

    public KitchenPlateCombinationKey(IReadOnlyList<int> ingredientIds)
    {
        _count = 0;
        _id0 = 0;
        _id1 = 0;
        _id2 = 0;
        _id3 = 0;
        _id4 = 0;
        _id5 = 0;

        if (ingredientIds == null)
        {
            return;
        }

        var count = 0;
        var id0 = 0;
        var id1 = 0;
        var id2 = 0;
        var id3 = 0;
        var id4 = 0;
        var id5 = 0;

        var hasOverflow = false;
        for (var i = 0; i < ingredientIds.Count; i++)
        {
            var ingredientId = ingredientIds[i];
            if (ingredientId <= 0)
            {
                continue;
            }

            if (Contains(count, id0, id1, id2, id3, id4, id5, ingredientId))
            {
                continue;
            }

            if (count >= MAX_INGREDIENT_COUNT)
            {
                hasOverflow = true;
                break;
            }

            InsertSorted(ref count, ref id0, ref id1, ref id2, ref id3, ref id4, ref id5, ingredientId);
        }

        _count = hasOverflow ? 0 : count;
        _id0 = id0;
        _id1 = id1;
        _id2 = id2;
        _id3 = id3;
        _id4 = id4;
        _id5 = id5;
    }

    public int Count => _count;
    public bool IsValid => _count > 0;

    public bool Equals(KitchenPlateCombinationKey other)
    {
        return _count == other._count &&
               _id0 == other._id0 &&
               _id1 == other._id1 &&
               _id2 == other._id2 &&
               _id3 == other._id3 &&
               _id4 == other._id4 &&
               _id5 == other._id5;
    }

    public override bool Equals(object obj)
    {
        return obj is KitchenPlateCombinationKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = _count;
            hashCode = (hashCode * 397) ^ _id0;
            hashCode = (hashCode * 397) ^ _id1;
            hashCode = (hashCode * 397) ^ _id2;
            hashCode = (hashCode * 397) ^ _id3;
            hashCode = (hashCode * 397) ^ _id4;
            hashCode = (hashCode * 397) ^ _id5;
            return hashCode;
        }
    }

    static void InsertSorted(
        ref int count,
        ref int id0,
        ref int id1,
        ref int id2,
        ref int id3,
        ref int id4,
        ref int id5,
        int ingredientId)
    {
        if (Contains(count, id0, id1, id2, id3, id4, id5, ingredientId))
        {
            return;
        }

        var insertIndex = count;
        for (var i = 0; i < count; i++)
        {
            if (ingredientId < GetAt(i, id0, id1, id2, id3, id4, id5))
            {
                insertIndex = i;
                break;
            }
        }

        for (var i = count; i > insertIndex; i--)
        {
            SetAt(i, ref id0, ref id1, ref id2, ref id3, ref id4, ref id5, GetAt(i - 1, id0, id1, id2, id3, id4, id5));
        }

        SetAt(insertIndex, ref id0, ref id1, ref id2, ref id3, ref id4, ref id5, ingredientId);
        count++;
    }

    static bool Contains(int count, int id0, int id1, int id2, int id3, int id4, int id5, int ingredientId)
    {
        for (var i = 0; i < count; i++)
        {
            if (GetAt(i, id0, id1, id2, id3, id4, id5) == ingredientId)
            {
                return true;
            }
        }

        return false;
    }

    static int GetAt(int index, int id0, int id1, int id2, int id3, int id4, int id5)
    {
        switch (index)
        {
            case 0:
                return id0;
            case 1:
                return id1;
            case 2:
                return id2;
            case 3:
                return id3;
            case 4:
                return id4;
            case 5:
                return id5;
            default:
                return 0;
        }
    }

    static void SetAt(
        int index,
        ref int id0,
        ref int id1,
        ref int id2,
        ref int id3,
        ref int id4,
        ref int id5,
        int value)
    {
        switch (index)
        {
            case 0:
                id0 = value;
                break;
            case 1:
                id1 = value;
                break;
            case 2:
                id2 = value;
                break;
            case 3:
                id3 = value;
                break;
            case 4:
                id4 = value;
                break;
            case 5:
                id5 = value;
                break;
        }
    }
}

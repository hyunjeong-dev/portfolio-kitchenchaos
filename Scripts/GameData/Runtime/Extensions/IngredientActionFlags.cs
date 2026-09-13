using System;

namespace Generated
{
    [Flags]
    public enum IngredientActionFlags
    {
        None = 0,
        Cut = 1 << 0,
        PanFry = 1 << 1,
        Boil = 1 << 2,
        Plate = 1 << 3,
    }
}

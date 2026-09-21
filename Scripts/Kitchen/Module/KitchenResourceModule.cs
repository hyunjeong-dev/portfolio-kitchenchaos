using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Generated;
using UnityEngine;
using static Generated.GameData;

public sealed class KitchenResourceModule : KitchenGameModule
{
    public const string ASSET_SCOPE_NAME = nameof(KitchenResourceModule);

    const string DELIVERY_RESULT_UI_PREFAB_ADDRESS = "Kitchen/Prefab/UI/DeliveryResultUI";
    const string LEADERBOARD_UI_PREFAB_ADDRESS = "Kitchen/Prefab/UI/UILeaderboard";
    const string LEADERBOARD_CELL_UI_PREFAB_ADDRESS =
        "Kitchen/Prefab/UI/UILeaderboardCell";

    static readonly ToolPrefabAddressEntry[] ToolPrefabAddresses =
    {
        new(ActionType.PanFry, "Kitchen/Prefab/Tools/m_tool_fryingpan"),
        new(ActionType.Boil, "Kitchen/Prefab/Tools/m_tool_pot"),
    };

    readonly List<string> _preloadPrefabAddresses = new();
    readonly List<string> _preloadIconAddresses = new();
    readonly KitchenResourcePreloadAddressBuilder _preloadAddressBuilder = new();
    readonly KitchenPlateVisualAddressRegistry _plateVisualAddressRegistry = new();

    public override void OnRegister()
    {
        base.OnRegister();
    }

    public override async UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        _preloadAddressBuilder.Build(
            Context.CurrentStageData,
            _preloadPrefabAddresses,
            _preloadIconAddresses,
            DELIVERY_RESULT_UI_PREFAB_ADDRESS);
        KitchenCollectionLogic.AddUniqueAddress(
            _preloadPrefabAddresses,
            LEADERBOARD_UI_PREFAB_ADDRESS);
        KitchenCollectionLogic.AddUniqueAddress(
            _preloadPrefabAddresses,
            LEADERBOARD_CELL_UI_PREFAB_ADDRESS);

        await _plateVisualAddressRegistry.BuildAsync(Context.CurrentStageData, cancellationToken);
        _plateVisualAddressRegistry.AddPrefabAddressesTo(_preloadPrefabAddresses);
        AddToolPrefabAddresses(_preloadPrefabAddresses);

        await PreloadAsync(cancellationToken);
    }

    public override void OnUnregister()
    {
        _preloadPrefabAddresses.Clear();
        _preloadIconAddresses.Clear();
        _plateVisualAddressRegistry.Clear();
        AssetManager.Instance.ReleaseScope(ASSET_SCOPE_NAME);

        base.OnUnregister();
    }

    public GameObject LoadIngredientPrefab(IngredientInfoData ingredientInfoData, string prefabPath)
    {
        if (ingredientInfoData == null || string.IsNullOrEmpty(prefabPath))
        {
            return null;
        }

        return LoadPrefab(prefabPath);
    }

    public GameObject LoadDeliveryResultUIPrefab()
    {
        return LoadPrefab(DELIVERY_RESULT_UI_PREFAB_ADDRESS);
    }

    public GameObject LoadLeaderboardUIPrefab()
    {
        return LoadPrefab(LEADERBOARD_UI_PREFAB_ADDRESS);
    }

    public GameObject LoadToolPrefab(ActionType actionType)
    {
        if (!TryGetToolPrefabAddress(actionType, out var prefabAddress))
        {
            return null;
        }

        return LoadPrefab(prefabAddress);
    }

    public bool TryGetPlateVisualPrefabAddress(
        KitchenPlateCombinationKey combinationKey,
        out string prefabAddress)
    {
        return _plateVisualAddressRegistry.TryGetPrefabAddress(combinationKey, out prefabAddress);
    }

    public GameObject LoadPlateVisualPrefab(string prefabAddress)
    {
        return string.IsNullOrEmpty(prefabAddress) ? null : LoadPrefab(prefabAddress);
    }

    public bool TryGetPreloadedRecipeIcon(
        RecipeInfoData recipeData,
        out Sprite sprite)
    {
        sprite = null;
        return recipeData != null &&
               !string.IsNullOrEmpty(recipeData.IconPath) &&
               TryGetPreloadedIcon(recipeData.IconPath, out sprite);
    }

    public bool TryGetPreloadedIngredientIcon(
        IngredientInfoData ingredientData,
        out Sprite sprite)
    {
        sprite = null;
        return ingredientData != null &&
               !string.IsNullOrEmpty(ingredientData.IconPath) &&
               TryGetPreloadedIcon(ingredientData.IconPath, out sprite);
    }

    public Sprite LoadIngredientSpriteIcon(IngredientInfoData ingredientData)
    {
        var spriteIconAddress = GetIngredientSpriteIconAddress(ingredientData);
        if (string.IsNullOrEmpty(spriteIconAddress))
        {
            return null;
        }

        return TryGetPreloadedIcon(spriteIconAddress, out var sprite) ? sprite : null;
    }

    async UniTask PreloadAsync(CancellationToken cancellationToken)
    {
        await AssetManager.Instance.PreloadAsync<GameObject>(
            _preloadPrefabAddresses,
            ASSET_SCOPE_NAME,
            cancellationToken);

        await AssetManager.Instance.PreloadAsync<Sprite>(
            _preloadIconAddresses,
            ASSET_SCOPE_NAME,
            cancellationToken);
    }

    GameObject LoadPrefab(string address)
    {
        return AssetManager.Instance.Load<GameObject>(
            address,
            ASSET_SCOPE_NAME);
    }

    bool TryGetPreloadedIcon(string address, out Sprite sprite)
    {
        var isLoaded = AssetManager.Instance.TryGetLoaded(
            address,
            ASSET_SCOPE_NAME,
            out sprite);
        if (!isLoaded)
        {
            Debug.LogWarning($"[KitchenResourceModule] Preloaded icon is not found. address: {address}");
        }

        return isLoaded;
    }

    string GetIngredientSpriteIconAddress(IngredientInfoData ingredientInfoData)
    {
        return ingredientInfoData != null ? ingredientInfoData.SpritePath : string.Empty;
    }

    static bool TryGetToolPrefabAddress(ActionType actionType, out string prefabAddress)
    {
        for (var i = 0; i < ToolPrefabAddresses.Length; i++)
        {
            var entry = ToolPrefabAddresses[i];
            if (entry.ActionType == actionType)
            {
                prefabAddress = entry.PrefabAddress;
                return !string.IsNullOrEmpty(prefabAddress);
            }
        }

        prefabAddress = string.Empty;
        return false;
    }

    static void AddToolPrefabAddresses(List<string> prefabAddresses)
    {
        if (prefabAddresses == null)
        {
            return;
        }

        for (var i = 0; i < ToolPrefabAddresses.Length; i++)
        {
            KitchenCollectionLogic.AddUniqueAddress(prefabAddresses, ToolPrefabAddresses[i].PrefabAddress);
        }
    }

    readonly struct ToolPrefabAddressEntry
    {
        public readonly ActionType ActionType;
        public readonly string PrefabAddress;

        public ToolPrefabAddressEntry(ActionType actionType, string prefabAddress)
        {
            ActionType = actionType;
            PrefabAddress = prefabAddress;
        }
    }
}

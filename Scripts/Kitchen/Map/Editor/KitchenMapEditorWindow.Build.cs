using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class KitchenMapEditorWindow
{
    List<KitchenMapCounterEntry> CreateCounterEntriesFromOpenScene()
    {
        var entries = new List<KitchenMapCounterEntry>();
        var root = KitchenMapSceneBuilder.ResolveGridRoot();
        var counters = FindObjectsByType<BaseCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (var i = 0; i < counters.Length; i++)
        {
            var counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            var gridPosition = ResolveCounterGridPosition(root, counter);
            if (!ContainsGridPosition(gridPosition))
            {
                Debug.LogWarning(
                    $"[MapEditor] Counter is out of map bounds. name: {counter.name}, position: {gridPosition}, size: {_width}x{_height}");
            }

            entries.Add(new KitchenMapCounterEntry(
                KitchenMapSceneBuilder.ResolveCounterType(counter),
                KitchenMapSceneBuilder.ResolveDirection(counter.transform),
                gridPosition,
                counter is ContainerCounter containerCounter ? ResolveContainerIngredientCode(containerCounter) : null));
        }

        return entries;
    }

    void BuildScene()
    {
        RefreshRequiredCounterStatuses();

        if (!TryValidateBuild(out var entries))
        {
            return;
        }

        var hasPlayerRootLocalPosition = KitchenMapSceneBuilder.TryGetOpenScenePlayerRootLocalPosition(
            out var playerRootLocalPosition);
        bool built;
        _isBuildingScene = true;
        try
        {
            built = KitchenMapSceneBuilder.Build(
                _stageLevel,
                _width,
                _height,
                DefaultOffset,
                DEFAULT_CELL_SIZE,
                entries,
                hasPlayerRootLocalPosition,
                playerRootLocalPosition);
        }
        finally
        {
            _isBuildingScene = false;
        }

        if (!built)
        {
            return;
        }

        SyncSettingsFromOpenScene();
        RefreshRequiredCounterStatusesIfVisible();
        RepaintSceneAndWindow();
    }

}

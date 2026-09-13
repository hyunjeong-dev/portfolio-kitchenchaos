using System.Collections.Generic;
using UnityEngine;

public sealed class KitchenDeliveryModule : KitchenGameModule
{
    const float SPAWN_RECIPE_TIMER_MAX = 4f;
    const int FAILED_DELIVERY_SCORE = 5;

    public KitchenDeliveryModuleEvents Events { get; } = new();
    public IReadOnlyList<KitchenMenuData> WaitingMenus => _waitingMenus;
    public GameReportData GameReportData => _gameReportData;
    public override bool CanUpdate => Context.IsGamePlaying;

    [ModuleRef] KitchenPlateModule _plateModule;

    readonly List<KitchenMenuData> _waitingMenus = new();
    readonly List<int> _recipeIds = new();
    readonly GameReportData _gameReportData = new();
    float _spawnRecipeTimer;
    int _waitingMenusMax;

    public override void OnRegister()
    {
        base.OnRegister();

        _recipeIds.Clear();

        var stageData = Context.CurrentStageData;
        _gameReportData.Reset(stageData, FAILED_DELIVERY_SCORE);

        if (stageData == null || stageData.RecipeIds == null || stageData.RecipeIds.Length == 0)
        {
            Debug.LogError("[KitchenDeliveryModule] Stage recipe ids are empty.");
            return;
        }

        if (stageData.MaxMenuCount <= 0)
        {
            Debug.LogError("[KitchenDeliveryModule] Stage max menu count is invalid.");
            return;
        }

        _waitingMenusMax = stageData.MaxMenuCount;

        for (var i = 0; i < stageData.RecipeIds.Length; i++)
        {
            _recipeIds.Add(stageData.RecipeIds[i]);
        }
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        var deltaTime = Time.deltaTime;
        TickWaitingMenuTimers(deltaTime);
        TickRecipeSpawn(deltaTime);
    }

    public override void OnUnregister()
    {
        _waitingMenus.Clear();
        _recipeIds.Clear();
        _spawnRecipeTimer = 0f;
        _waitingMenusMax = 0;
        _gameReportData.Reset(null, FAILED_DELIVERY_SCORE);

        base.OnUnregister();
    }

    public bool DeliverRecipe(PlateObject plateObject)
    {
        if (plateObject == null)
        {
            RecordFailedDelivery();
            return false;
        }

        if (_plateModule == null || !_plateModule.TryResolveRecipe(plateObject, out var recipeInfoData))
        {
            RecordFailedDelivery();
            return false;
        }

        var plateCombinationKey = recipeInfoData.GetPlateCombinationKey();
        var waitingMenuIndex = FindFirstWaitingMenuIndex(plateCombinationKey);
        if (waitingMenuIndex < 0)
        {
            RecordFailedDelivery();
            return false;
        }

        var waitingMenu = _waitingMenus[waitingMenuIndex];
        _gameReportData.RecordSuccessfulMenu(waitingMenu);
        _waitingMenus.RemoveAt(waitingMenuIndex);

        Events.InvokeMenuCompleted();
        Events.InvokeScoreChanged(_gameReportData);
        return true;
    }

    void TickWaitingMenuTimers(float deltaTime)
    {
        if (deltaTime <= 0f || _waitingMenus.Count == 0)
        {
            return;
        }

        var hasExpiredMenu = false;
        for (var i = _waitingMenus.Count - 1; i >= 0; i--)
        {
            var waitingMenu = _waitingMenus[i];
            if (waitingMenu == null)
            {
                _waitingMenus.RemoveAt(i);
                hasExpiredMenu = true;
                continue;
            }

            if (!waitingMenu.Tick(deltaTime))
            {
                continue;
            }

            _gameReportData.RecordExpiredMenu(waitingMenu);
            _waitingMenus.RemoveAt(i);
            hasExpiredMenu = true;
            Events.InvokeMenuExpired(waitingMenu);
        }

        if (!hasExpiredMenu)
        {
            return;
        }

        Events.InvokeScoreChanged(_gameReportData);
    }

    void TickRecipeSpawn(float deltaTime)
    {
        if (_recipeIds.Count == 0 || _waitingMenusMax <= 0)
        {
            return;
        }

        _spawnRecipeTimer -= deltaTime;
        if (_spawnRecipeTimer > 0f)
        {
            return;
        }

        _spawnRecipeTimer = SPAWN_RECIPE_TIMER_MAX;

        if (_waitingMenus.Count >= _waitingMenusMax)
        {
            return;
        }

        var recipeIndex = Random.Range(0, _recipeIds.Count);
        var recipeData = GameData.Instance.GetRecipeInfoData(_recipeIds[recipeIndex]);
        if (recipeData == null)
        {
            Debug.LogError($"[KitchenDeliveryModule] Recipe data is missing. recipeId: {_recipeIds[recipeIndex]}");
            return;
        }

        var menuData = new KitchenMenuData(recipeData);
        _waitingMenus.Add(menuData);

        Events.InvokeMenuSpawned(menuData);
    }

    void RecordFailedDelivery()
    {
        _gameReportData.RecordFailedDelivery();
        Events.InvokeScoreChanged(_gameReportData);
    }

    int FindFirstWaitingMenuIndex(KitchenPlateCombinationKey plateCombinationKey)
    {
        for (var i = 0; i < _waitingMenus.Count; i++)
        {
            var waitingMenu = _waitingMenus[i];
            if (waitingMenu != null && waitingMenu.CombinationKey.Equals(plateCombinationKey))
            {
                return i;
            }
        }

        return -1;
    }
}

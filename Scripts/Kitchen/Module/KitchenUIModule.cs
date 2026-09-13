using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class KitchenUIModule : KitchenGameModule
{
    [ModuleRef] KitchenTimerModule _timerModule;
    [ModuleRef] KitchenDeliveryModule _deliveryModule;
    [ModuleRef] KitchenResourceModule _resourceModule;
    [ModuleRef] KitchenInputModule _inputModule;

    GameObject _gameUIInstance;
    UIGameResult _gameResultUI;
    UIGamePause _gamePauseUI;
    UIGameTimer _gameTimerUI;
    UIGameScore _gameScoreUI;
    UIGameStartCountdown _gameStartCountdownUI;
    UIGameOptions _optionsUI;
    UIRecipeList _recipeListUI;
    UIGameHud _gameHudUI;
    DeliveryCounter[] _deliveryCounters;

    public override void OnRegister()
    {
        base.OnRegister();

        CreateGameUI();
        CacheUIReferences();
        CacheDeliveryCounters();
    }

    public override UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        ApplyPreloadedUIState();

        return UniTask.CompletedTask;
    }

    public override void OnBegin()
    {
        base.OnBegin();

        Context.OnStateChanged += OnStateChanged;
        Context.OnGamePaused += OnGamePaused;
        Context.OnGameUnpaused += OnGameUnpaused;

        _gamePauseUI?.Initialize();
        _gameResultUI?.Initialize();
        _optionsUI?.Initialize(Context);
        _recipeListUI?.Initialize(Context, Context.CurrentStageData.MaxMenuCount);
        _gameHudUI?.Initialize(OnClickPauseButton);
        InitializeDeliveryCounters();
        BindDeliveryModuleEvents();

        InitializeUIState();
        ApplyUIState(Context.CurrentState);
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (Context.IsCountdownToStartActive)
        {
            _gameStartCountdownUI?.UpdateTime(_timerModule.CountdownToStartTimer);
            return;
        }

        if (Context.IsGamePlaying)
        {
            _gameTimerUI?.UpdateTime(_timerModule.GamePlayingTimerNormalized, _timerModule.GamePlayingTimeRemaining);
        }
    }

    public override void OnEnd()
    {
        Context.OnStateChanged -= OnStateChanged;
        Context.OnGamePaused -= OnGamePaused;
        Context.OnGameUnpaused -= OnGameUnpaused;

        UnbindDeliveryModuleEvents();
        _gameHudUI?.Uninitialize();
        _optionsUI?.Uninitialize();
        _gameResultUI?.Uninitialize();
        _gamePauseUI?.Uninitialize();

        base.OnEnd();
    }

    public override void OnUnregister()
    {
        ClearUIReferences();

        if (_gameUIInstance != null)
        {
            Object.Destroy(_gameUIInstance);
            _gameUIInstance = null;
        }

        base.OnUnregister();
    }

    void CreateGameUI()
    {
        if (_gameUIInstance != null)
        {
            return;
        }

        var gameUIPrefab = Context.GameUIPrefab;
        if (gameUIPrefab == null)
        {
            Debug.LogError("[KitchenUIModule] GameUI prefab is not assigned.");
            return;
        }

        _gameUIInstance = Object.Instantiate(gameUIPrefab, Context.transform, false);
        _gameUIInstance.name = gameUIPrefab.name;
    }

    void CacheUIReferences()
    {
        if (_gameUIInstance == null)
        {
            return;
        }

        _gamePauseUI = _gameUIInstance.GetComponentInChildren<UIGamePause>(true);
        _gameTimerUI = _gameUIInstance.GetComponentInChildren<UIGameTimer>(true);
        _gameScoreUI = _gameUIInstance.GetComponentInChildren<UIGameScore>(true);
        _gameStartCountdownUI = _gameUIInstance.GetComponentInChildren<UIGameStartCountdown>(true);
        _optionsUI = _gameUIInstance.GetComponentInChildren<UIGameOptions>(true);
        _recipeListUI = _gameUIInstance.GetComponentInChildren<UIRecipeList>(true);
        _gameResultUI = _gameUIInstance.GetComponentInChildren<UIGameResult>(true);
        _gameHudUI = _gameUIInstance.GetComponentInChildren<UIGameHud>(true);
    }

    void CacheDeliveryCounters()
    {
        _deliveryCounters = Context.GetComponentsInChildren<DeliveryCounter>(true);
        if (_deliveryCounters == null || _deliveryCounters.Length == 0)
        {
            Debug.LogError("[KitchenUIModule] DeliveryCounter is not found.");
        }
    }

    void ClearUIReferences()
    {
        _gameResultUI = null;
        _gamePauseUI = null;
        _gameTimerUI = null;
        _gameScoreUI = null;
        _gameStartCountdownUI = null;
        _optionsUI = null;
        _recipeListUI = null;
        _gameHudUI = null;
        _deliveryCounters = null;
    }

    void InitializeUIState()
    {
        _gameTimerUI?.InitializeTime(_timerModule.GamePlayingTimeMax);
        _gameStartCountdownUI?.InitializeTime(_timerModule.CountdownToStartTimer);
        _gameScoreUI?.UpdateScore(_deliveryModule.GameReportData);
        UpdateRecipeListUI(_deliveryModule.WaitingMenus);
    }

    void ApplyPreloadedUIState()
    {
        HideAllUI();
    }

    void ApplyUIState(KitchenGameState state)
    {
        HideAllUI();

        switch (state)
        {
            case KitchenGameState.CountdownToStart:
                _gameStartCountdownUI?.Show();
                break;
            case KitchenGameState.GamePlaying:
                ShowPlayingUI();
                break;
            case KitchenGameState.GameOver:
                _gameResultUI?.Show(Context);
                break;
        }
    }

    void HideMainUI()
    {
        _gameStartCountdownUI?.Hide();
        _gameTimerUI?.Hide();
        _gameScoreUI?.Hide();
        _gameHudUI?.HidePauseButton();
        HideRecipeListUI();
        _gameResultUI?.Hide();
    }

    void HideAllUI()
    {
        HideMainUI();
        _gamePauseUI?.Hide();
        _optionsUI?.Hide();
    }

    void ShowPlayingUI()
    {
        _gameTimerUI?.Show();
        _gameScoreUI?.Show();
        _gameHudUI?.ShowPauseButton();
        ShowRecipeListUI();
    }

    void InitializeDeliveryCounters()
    {
        if (_deliveryCounters == null)
        {
            return;
        }

        var deliveryResultUIPrefab = _resourceModule.LoadDeliveryResultUIPrefab();
        if (deliveryResultUIPrefab == null)
        {
            Debug.LogError("[KitchenUIModule] DeliveryResultUI prefab load failed.");
            return;
        }

        var uiCamera = GetUICamera();
        for (var i = 0; i < _deliveryCounters.Length; i++)
        {
            _deliveryCounters[i]?.Initialize(deliveryResultUIPrefab, uiCamera);
        }
    }

    void OnStateChanged(KitchenGameStateChangedEvent stateChangedEvent)
    {
        ApplyUIState(stateChangedEvent.CurrentState);
    }

    void OnGamePaused()
    {
        _gameHudUI?.HidePauseButton();
        _gamePauseUI?.Show(Context, _optionsUI);
    }

    void OnGameUnpaused()
    {
        _gamePauseUI?.Hide();
        _optionsUI?.Hide();

        if (Context.IsGamePlaying)
        {
            _gameHudUI?.ShowPauseButton();
        }
    }

    void OnClickPauseButton()
    {
        if (!Context.IsGamePlaying)
        {
            return;
        }

        Context.TogglePauseGame();
    }

    void BindDeliveryModuleEvents()
    {
        _deliveryModule.Events.OnMenuSpawned += OnMenuSpawned;
        _deliveryModule.Events.OnMenuCompleted += OnMenuCompleted;
        _deliveryModule.Events.OnMenuExpired += OnMenuExpired;
        _deliveryModule.Events.OnScoreChanged += OnScoreChanged;
    }

    void UnbindDeliveryModuleEvents()
    {
        _deliveryModule.Events.OnMenuSpawned -= OnMenuSpawned;
        _deliveryModule.Events.OnMenuCompleted -= OnMenuCompleted;
        _deliveryModule.Events.OnMenuExpired -= OnMenuExpired;
        _deliveryModule.Events.OnScoreChanged -= OnScoreChanged;
    }

    void OnMenuSpawned(KitchenMenuData menuData)
    {
        UpdateRecipeList();
    }

    void OnMenuCompleted()
    {
        UpdateRecipeList();
    }

    void OnMenuExpired(KitchenMenuData menuData)
    {
        if (_recipeListUI == null)
        {
            return;
        }

        _recipeListUI.PlayExpiredMenu(menuData, _deliveryModule.WaitingMenus);
    }

    void UpdateRecipeList()
    {
        UpdateRecipeListUI(_deliveryModule.WaitingMenus);
    }

    void UpdateRecipeListUI(IReadOnlyList<KitchenMenuData> waitingMenus)
    {
        _recipeListUI?.UpdateMenus(waitingMenus);
    }

    void ShowRecipeListUI()
    {
        _recipeListUI?.Show();
    }

    void HideRecipeListUI()
    {
        _recipeListUI?.Hide();
    }

    void OnScoreChanged(GameReportData gameReportData)
    {
        _gameScoreUI?.UpdateScore(gameReportData);
    }

    Camera GetUICamera()
    {
        if (CameraManager.Instance == null)
        {
            return Camera.main;
        }

        return CameraManager.Instance.GetTopCamera(CameraManager.SortingType.UI) ??
               CameraManager.Instance.GetTopCamera();
    }
}

using System;
using System.Threading;
using Cinemachine;
using Cysharp.Threading.Tasks;
using UnityEngine;
using static Generated.GameData;

public sealed partial class KitchenGameContext : MonoBehaviour
{
    const KitchenGameState PLAYABLE_STATE_MASK = KitchenGameState.GamePlaying;

    const KitchenGameState MODULE_UPDATE_STATE_MASK =
        KitchenGameState.WaitingToStart |
        KitchenGameState.CountdownToStart |
        KitchenGameState.GamePlaying;

    const int DEFAULT_STAGE_LEVEL = 1;

    public static KitchenGameContext Instance { get; private set; }

    public KitchenGameEvents Events { get; } = new();

    public event Action<KitchenGameStateChangedEvent> OnStateChanged
    {
        add => Events.OnStateChanged += value;
        remove => Events.OnStateChanged -= value;
    }

    public event Action OnGamePaused
    {
        add => Events.OnGamePaused += value;
        remove => Events.OnGamePaused -= value;
    }

    public event Action OnGameUnpaused
    {
        add => Events.OnGameUnpaused += value;
        remove => Events.OnGameUnpaused -= value;
    }

    public KitchenGameState CurrentState => _stateMachine != null ? _stateMachine.CurrentState : KitchenGameState.WaitingToStart;
    public bool IsGamePlaying => HasState(KitchenGameState.GamePlaying);
    public bool IsCountdownToStartActive => HasState(KitchenGameState.CountdownToStart);
    public bool IsGamePaused => _pauseModule != null && _pauseModule.IsGamePaused;
    public bool IsPlayable => HasState(PLAYABLE_STATE_MASK) && !IsGamePaused;
    public int StageLevel => _stageLevel;
    public int NextStageLevel => _stageLevel + 1;
    public bool HasNextStage => GameData.Instance.GetStageInfoDataByLevel(NextStageLevel) != null;
    public StageInfoData CurrentStageData => GameData.Instance.GetStageInfoDataByLevel(_stageLevel);
    public KitchenStageResultData StageResult { get; private set; }

    [SerializeField] int _stageLevel = DEFAULT_STAGE_LEVEL;
    [SerializeField] GameObject _gameUIPrefab;
    [SerializeField] CinemachineVirtualCamera _virtualCamera;
    [SerializeField] Transform _cameraTargetRoot;
    [SerializeField] Transform _playerRoot;

    KitchenTimerModule _timerModule;
    KitchenPauseModule _pauseModule;
    KitchenInputModule _inputModule;
    KitchenGameStateMachine _stateMachine;
    bool _isInitialized;

    public GameObject GameUIPrefab => _gameUIPrefab;
    public CinemachineVirtualCamera VirtualCamera => _virtualCamera;
    public Transform CameraTargetRoot => _cameraTargetRoot;
    public Transform PlayerRoot => _playerRoot;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[KitchenGameContext] Duplicate instance detected.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        UpdateGameState();

        if (!HasState(MODULE_UPDATE_STATE_MASK))
        {
            return;
        }

        for (var i = 0; i < _modules.Count; i++)
        {
            var module = _modules[i];
            if (!module.IsReady || !module.CanUpdate)
            {
                continue;
            }

            module.OnUpdate();
        }
    }

    void UpdateGameState()
    {
        switch (CurrentState)
        {
            case KitchenGameState.WaitingToStart:
                break;
            case KitchenGameState.CountdownToStart:
                if (_timerModule.TickCountdown(Time.deltaTime))
                {
                    _timerModule.StartPlayingTimer();
                    StartPlaying();
                }

                break;
            case KitchenGameState.GamePlaying:
                if (_timerModule.TickGamePlaying(Time.deltaTime))
                {
                    GameOver();
                }

                break;
            case KitchenGameState.GameOver:
                break;
        }
    }

    void OnDestroy()
    {
        Uninitialize();
    }

    public void Initialize()
    {
        InitializeAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
    }

    public async UniTask InitializeAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        _stateMachine = new(KitchenGameState.WaitingToStart);
        StageResult = default;

        PreBuildModules();
        await PrepareModulesAsync(cancellationToken);

        _timerModule = GetModule<KitchenTimerModule>();
        _pauseModule = GetModule<KitchenPauseModule>();
        _inputModule = GetModule<KitchenInputModule>();
        BindPauseModuleEvents();
        BindInputModuleEvents();

        BeginModules();
        PostProcessModules();
        _isInitialized = true;

        RequestBeginCountdown();
    }

    public void Uninitialize()
    {
        if (!_isInitialized)
        {
            if (Instance == this)
            {
                Instance = null;
            }

            return;
        }

        _isInitialized = false;
        ResetState();

        UnbindPauseModuleEvents();
        UnbindInputModuleEvents();
        ResetModules();

        _timerModule = null;
        _pauseModule = null;
        _inputModule = null;
        _stateMachine = null;
        StageResult = default;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RequestBeginCountdown()
    {
        if (HasState(KitchenGameState.WaitingToStart))
        {
            BeginCountdown();
        }
    }

    public void TogglePauseGame()
    {
        _pauseModule.TogglePause();
    }

    public bool HasState(KitchenGameState stateMask)
    {
        return _stateMachine != null && _stateMachine.HasState(stateMask);
    }

    void BeginCountdown()
    {
        var previousState = CurrentState;
        if (_stateMachine != null && _stateMachine.BeginCountdown())
        {
            Events.InvokeStateChanged(previousState, CurrentState);
        }
    }

    void StartPlaying()
    {
        var previousState = CurrentState;
        if (_stateMachine != null && _stateMachine.StartPlaying())
        {
            Events.InvokeStateChanged(previousState, CurrentState);
        }
    }

    void GameOver()
    {
        var previousState = CurrentState;
        if (_stateMachine != null && _stateMachine.GameOver())
        {
            var hasStageResult = TryFinalizeStageResult();
            Events.InvokeStateChanged(previousState, CurrentState);

            if (hasStageResult)
            {
                Events.InvokeStageResultFinalized(StageResult);
            }
        }
    }

    bool TryFinalizeStageResult()
    {
        var deliveryModule = GetModule<KitchenDeliveryModule>();
        var gameReportData = deliveryModule != null ? deliveryModule.GameReportData : null;
        if (gameReportData == null)
        {
            Debug.LogError("[KitchenGameContext] Stage result could not be finalized.");
            return false;
        }

        StageResult = new KitchenStageResultData(StageLevel, gameReportData);
        SaveDataManager.Instance.RecordStageResult(
            StageResult.StageLevel,
            StageResult.IsGoalAchieved,
            StageResult.FinalScore);
        return true;
    }

    void ResetState()
    {
        var previousState = CurrentState;
        if (_stateMachine != null && _stateMachine.Reset())
        {
            Events.InvokeStateChanged(previousState, CurrentState);
        }
    }

    void BindPauseModuleEvents()
    {
        _pauseModule.OnGamePaused += KitchenPauseModule_OnGamePaused;
        _pauseModule.OnGameUnpaused += KitchenPauseModule_OnGameUnpaused;
    }

    void UnbindPauseModuleEvents()
    {
        _pauseModule.OnGamePaused -= KitchenPauseModule_OnGamePaused;
        _pauseModule.OnGameUnpaused -= KitchenPauseModule_OnGameUnpaused;
    }

    void BindInputModuleEvents()
    {
        _inputModule.Events.OnInteract += KitchenInputModule_OnInteract;
        _inputModule.Events.OnPause += KitchenInputModule_OnPause;
    }

    void UnbindInputModuleEvents()
    {
        _inputModule.Events.OnInteract -= KitchenInputModule_OnInteract;
        _inputModule.Events.OnPause -= KitchenInputModule_OnPause;
    }

    void KitchenPauseModule_OnGamePaused()
    {
        Events.InvokeGamePaused();
    }

    void KitchenPauseModule_OnGameUnpaused()
    {
        Events.InvokeGameUnpaused();
    }

    void KitchenInputModule_OnInteract()
    {
        RequestBeginCountdown();
    }

    void KitchenInputModule_OnPause()
    {
        TogglePauseGame();
    }
}

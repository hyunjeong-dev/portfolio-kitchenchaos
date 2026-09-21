using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class MainMenuScene : SingletonMonoBehaviourNotCreate<MainMenuScene>
{
    [SerializeField] UIMainMenu uiMainMenu;
    [SerializeField] UIStageList uiStageList;
    [SerializeField] UISaveConflictView _saveConflictView;
    
    bool _isInitialized;
    bool _hasStartedInitialization;
    UniTask _initializationTask;

    async void Start()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            await InitializeAsync();
            Initialize();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Debug.LogError($"[MainMenuScene] Initialize failed.\n{exception}");
        }
    }

    public override UniTask InitializeAsync()
    {
        // Start와 SceneChangeManager가 동시에 진입해도 같은 선택/초기화 작업을 기다린다.
        if (!_hasStartedInitialization)
        {
            _hasStartedInitialization = true;
            _initializationTask = InitializeSceneAsync().Preserve();
        }

        return _initializationTask;
    }

    async UniTask InitializeSceneAsync()
    {
        Time.timeScale = 1f;
        uiMainMenu.gameObject.SetActive(false);
        uiStageList.gameObject.SetActive(false);
        await base.InitializeAsync();
        await InitializeManagersAsync();
        await InitializeGameDataAsync();
        this.GetCancellationTokenOnDestroy().ThrowIfCancellationRequested();
    }

    public override void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        Time.timeScale = 1f;
        
        uiMainMenu.gameObject.SetActive(true);
        uiMainMenu.Build();
        uiStageList.Build();
    }

    public override void Uninitialize()
    {
        _isInitialized = false;

        base.Uninitialize();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }

    async UniTask InitializeManagersAsync()
    {
        var managerInitializer = new ManagerInitializer();
        await managerInitializer.InitializeAsync(this.GetCancellationTokenOnDestroy(), ChooseSaveAsync);
    }

    UniTask<SaveConflictChoice> ChooseSaveAsync(SaveConflictData conflict, CancellationToken cancellationToken)
    {
        if (_saveConflictView != null)
        {
            return _saveConflictView.ChooseAsync(conflict, cancellationToken);
        }

        Debug.LogWarning("[MainMenuScene] Save conflict UI is not assigned. Local remains active; Cloud writes are blocked.");
        return UniTask.FromResult(SaveConflictChoice.Defer);
    }

    async UniTask InitializeGameDataAsync()
    {
        await GameData.Instance.EnsureReadyAsync(this.GetCancellationTokenOnDestroy());
    }
}

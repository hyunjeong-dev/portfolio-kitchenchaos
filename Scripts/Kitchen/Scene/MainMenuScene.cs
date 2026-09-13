using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class MainMenuScene : SingletonMonoBehaviourNotCreate<MainMenuScene>
{
    [SerializeField] UIMainMenu uiMainMenu;
    [SerializeField] UIStageList uiStageList;
    
    bool _isInitialized;

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

    public override async UniTask InitializeAsync()
    {
        await base.InitializeAsync();
        await InitializeManagersAsync();
        await InitializeGameDataAsync();
    }

    public override void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        Time.timeScale = 1f;
        
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
        await managerInitializer.InitializeAsync();
    }

    async UniTask InitializeGameDataAsync()
    {
        await GameData.Instance.EnsureReadyAsync(CancellationTokenSource.Token);
    }
}

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class GameScene : SingletonMonoBehaviourNotCreate<GameScene>
{
    [SerializeField] KitchenGameContext _kitchenGameContext;
    bool _isInitialized;

    static bool IsSceneTransitionInProgress
    {
        get
        {
            var sceneChangeManager = FindFirstObjectByType<SceneChangeManager>();
            return sceneChangeManager != null && sceneChangeManager.IsBusy;
        }
    }

    async void Start()
    {
        if (_isInitialized || IsSceneTransitionInProgress)
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
    }

    public override async UniTask InitializeAsync()
    {
        await base.InitializeAsync();
        await InitializeGameDataAsync();
        await InitializeGameContextAsync();
    }

    public override void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
    }

    async UniTask InitializeGameContextAsync()
    {
        if (_kitchenGameContext == null)
        {
            Debug.LogError("[GameScene] KitchenGameContext is not assigned.");
            return;
        }

        await _kitchenGameContext.InitializeAsync(CancellationTokenSource.Token);
    }

    public override void Uninitialize()
    {
        ReleaseGameContext();

        _isInitialized = false;

        base.Uninitialize();
    }

    public override void OnDestroy()
    {
        Uninitialize();

        base.OnDestroy();
    }

    async UniTask InitializeGameDataAsync()
    {
        await GameData.Instance.EnsureReadyAsync(CancellationTokenSource.Token);
    }

    void ReleaseGameContext()
    {
        if (_kitchenGameContext == null)
        {
            return;
        }

        _kitchenGameContext.Uninitialize();
    }
}

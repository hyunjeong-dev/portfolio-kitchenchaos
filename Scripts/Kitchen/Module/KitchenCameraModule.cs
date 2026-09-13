using System.Threading;
using Cinemachine;
using Cysharp.Threading.Tasks;
using UnityEngine;

public sealed class KitchenCameraModule : KitchenGameModule
{
    [ModuleRef] KitchenPlayerModule _playerModule;

    KitchenStageCamera _stageCamera;
    bool _isRuntimeStageCameraCreated;

    public override bool CanUpdate => false;

    public override UniTask PrepareAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ClearCameraReferences();
        InitializeStageCamera();

        return UniTask.CompletedTask;
    }

    public override void OnBegin()
    {
        Context.OnStateChanged += OnStateChanged;
        _stageCamera?.SetFollowEnabled(Context.IsPlayable);

        base.OnBegin();
    }

    public override void OnEnd()
    {
        Context.OnStateChanged -= OnStateChanged;
        _stageCamera?.SetFollowEnabled(false);
        ClearCameraReferences();

        base.OnEnd();
    }

    public override void OnUnregister()
    {
        ClearCameraReferences();

        base.OnUnregister();
    }

    void InitializeStageCamera()
    {
        var targetRoot = Context.CameraTargetRoot;
        if (targetRoot == null)
        {
            Debug.LogWarning("[KitchenCameraModule] Camera target root is not assigned.");
            return;
        }

        var gameCamera = ResolveGameCamera();
        var virtualCamera = ResolveVirtualCamera();
        var cameraTransform = virtualCamera != null ? virtualCamera.transform : gameCamera != null ? gameCamera.transform : null;
        if (gameCamera == null || cameraTransform == null)
        {
            Debug.LogWarning("[KitchenCameraModule] Game camera is not assigned.");
            return;
        }

        _stageCamera = ResolveStageCamera(gameCamera, cameraTransform);
        _stageCamera.Initialize(
            gameCamera,
            cameraTransform,
            _playerModule != null ? _playerModule.PlayerTransform : null,
            targetRoot);
    }

    Camera ResolveGameCamera()
    {
        var scene = Context.gameObject.scene;
        var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera fallbackCamera = null;

        for (var i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            if (camera.CompareTag("MainCamera"))
            {
                return camera;
            }

            if (fallbackCamera == null)
            {
                fallbackCamera = camera;
            }
        }

        return fallbackCamera;
    }

    CinemachineVirtualCamera ResolveVirtualCamera()
    {
        if (Context.VirtualCamera != null)
        {
            return Context.VirtualCamera;
        }

        var scene = Context.gameObject.scene;
        var cameras = Object.FindObjectsByType<CinemachineVirtualCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CinemachineVirtualCamera result = null;

        for (var i = 0; i < cameras.Length; i++)
        {
            var camera = cameras[i];
            if (camera == null || camera.gameObject.scene != scene)
            {
                continue;
            }

            if (result == null || camera.Priority > result.Priority)
            {
                result = camera;
            }
        }

        return result;
    }

    KitchenStageCamera ResolveStageCamera(Camera gameCamera, Transform cameraTransform)
    {
        var stageCamera = cameraTransform.GetComponent<KitchenStageCamera>();
        if (stageCamera != null)
        {
            return stageCamera;
        }

        stageCamera = gameCamera.GetComponent<KitchenStageCamera>();
        if (stageCamera != null)
        {
            return stageCamera;
        }

        stageCamera = Context.GetComponentInChildren<KitchenStageCamera>(true);
        if (stageCamera != null)
        {
            return stageCamera;
        }

        _isRuntimeStageCameraCreated = true;
        return cameraTransform.gameObject.AddComponent<KitchenStageCamera>();
    }

    void OnStateChanged(KitchenGameStateChangedEvent stateChangedEvent)
    {
        _stageCamera?.SetFollowEnabled(Context.IsPlayable);
    }

    void ClearCameraReferences()
    {
        if (_isRuntimeStageCameraCreated && _stageCamera != null)
        {
            Object.Destroy(_stageCamera);
        }

        _stageCamera = null;
        _isRuntimeStageCameraCreated = false;
    }
}

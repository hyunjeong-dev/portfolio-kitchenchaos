using UnityEngine;

public class DeliveryCounter : BaseCounter
{
    UIDeliveryResult _uiDeliveryResult;
    KitchenDeliveryModule _deliveryModule;

    protected override void OnInitialize(KitchenGameContext context)
    {
        _deliveryModule = context != null ? context.GetModule<KitchenDeliveryModule>() : null;
    }

    protected override void OnUninitialize()
    {
        _deliveryModule = null;
    }

    public void Initialize(GameObject deliveryResultUIPrefab, Camera uiCamera)
    {
        CreateDeliveryResultUI(deliveryResultUIPrefab);

        if (_uiDeliveryResult != null)
        {
            _uiDeliveryResult.Initialize(uiCamera);
        }
    }

    void CreateDeliveryResultUI(GameObject deliveryResultUIPrefab)
    {
        if (_uiDeliveryResult != null)
        {
            return;
        }

        if (deliveryResultUIPrefab == null)
        {
            Debug.LogError("[DeliveryCounter] DeliveryResultUI prefab is not loaded.", this);
            return;
        }

        var instance = Instantiate(deliveryResultUIPrefab, transform, false);
        instance.name = deliveryResultUIPrefab.name;

        if (!instance.TryGetComponent(out _uiDeliveryResult))
        {
            Debug.LogError("[DeliveryCounter] DeliveryResultUI prefab root must have UIDeliveryResult.", instance);
        }
    }

    public override void Interact(PlayerBehaviour player)
    {
        if (player.HasHoldable)
        {
            if (player.CurrentHoldable is PlateObject plateObject)
            {
                // Only accepts Plates

                if (_deliveryModule == null)
                {
                    Debug.LogError("[DeliveryCounter] KitchenDeliveryModule is not found.");
                    return;
                }

                var isDeliverySucceeded = _deliveryModule.DeliverRecipe(plateObject);
                SoundManager.Instance.PlaySound(
                    isDeliverySucceeded ? SoundKeys.DELIVERY_SUCCESS : SoundKeys.DELIVERY_FAIL,
                    transform.position);
                ShowDeliveryResult(isDeliverySucceeded);

                player.CurrentHoldable.Release();
            }
        }
    }

    void ShowDeliveryResult(bool isDeliverySucceeded)
    {
        if (isDeliverySucceeded)
        {
            _uiDeliveryResult?.ShowSuccess();
            return;
        }

        _uiDeliveryResult?.ShowFailed();
    }
}

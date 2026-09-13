using UnityEngine;

public class SelectedCounterVisual : MonoBehaviour
{

    [SerializeField] BaseCounter baseCounter;
    [SerializeField] GameObject[] visualGameObjectArray;

    SelectorModule _selectorModule;

    void Start()
    {
        if (baseCounter == null)
        {
            Hide();
            return;
        }

        baseCounter.OnInitialized += BaseCounter_OnInitialized;
        baseCounter.OnUninitialized += BaseCounter_OnUninitialized;

        if (baseCounter.TryGetContext(out var context))
        {
            BaseCounter_OnInitialized(context);
            return;
        }

        Hide();
    }

    void OnDestroy()
    {
        if (baseCounter != null)
        {
            baseCounter.OnInitialized -= BaseCounter_OnInitialized;
            baseCounter.OnUninitialized -= BaseCounter_OnUninitialized;
        }

        UnbindSelectorModule();
    }

    void BaseCounter_OnInitialized(KitchenGameContext context)
    {
        UnbindSelectorModule();

        _selectorModule = context != null ? context.GetModule<SelectorModule>() : null;
        if (_selectorModule == null)
        {
            Hide();
            return;
        }

        _selectorModule.Events.OnSelectedCounterChanged += OnSelectedCounterChanged;
        Refresh(_selectorModule.CurrentCounter);
    }

    void BaseCounter_OnUninitialized()
    {
        UnbindSelectorModule();
        Hide();
    }

    void UnbindSelectorModule()
    {
        if (_selectorModule != null)
        {
            _selectorModule.Events.OnSelectedCounterChanged -= OnSelectedCounterChanged;
        }

        _selectorModule = null;
    }

    void OnSelectedCounterChanged(BaseCounter selectedCounter)
    {
        Refresh(selectedCounter);
    }

    void Refresh(BaseCounter selectedCounter)
    {
        if (selectedCounter == baseCounter)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    void Show()
    {
        foreach (GameObject visualGameObject in visualGameObjectArray)
        {
            visualGameObject.SetActive(true);
        }
    }

    void Hide()
    {
        foreach (GameObject visualGameObject in visualGameObjectArray)
        {
            visualGameObject.SetActive(false);
        }
    }

}

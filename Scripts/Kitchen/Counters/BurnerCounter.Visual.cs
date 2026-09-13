using UnityEngine;

public partial class BurnerCounter
{
    [SerializeField] GameObject _cookingOnGameObject;

    void InitializeVisual()
    {
        Refresh();
    }

    void Refresh()
    {
        var isCooking = IsCookingVisualActive;

        SetActiveOnObject(isCooking);

        if (_progressBar != null)
        {
            _progressBar.SetBurnWarningActive(IsCookingCompletedVisualActive);
        }
    }

    void SetActiveOnObject(bool value)
    {
        if (_cookingOnGameObject != null)
        {
            _cookingOnGameObject.SetActive(value);
        }
    }

}

using UnityEngine;

public sealed class UIBurnerWarning : MonoBehaviour
{
    public void SetActive(bool active)
    {
        if (gameObject.activeSelf != active)
        {
            gameObject.SetActive(active);
        }
    }
}

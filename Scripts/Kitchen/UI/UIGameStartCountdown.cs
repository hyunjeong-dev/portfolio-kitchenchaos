using TMPro;
using UnityEngine;

public class UIGameStartCountdown : MonoBehaviour
{

    static readonly int AnimatorHash = Animator.StringToHash("NumberPopup");

    [SerializeField] TextMeshProUGUI countdownText;

    Animator animator;
    int previousCountdownNumber;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void InitializeTime(float countdownTimer)
    {
        previousCountdownNumber = Mathf.CeilToInt(countdownTimer);
        countdownText.SetText("{0}", previousCountdownNumber);
    }

    public void UpdateTime(float countdownTimer)
    {
        var countdownNumber = Mathf.CeilToInt(countdownTimer);
        if (previousCountdownNumber == countdownNumber)
        {
            return;
        }

        countdownText.SetText("{0}", countdownNumber);

        previousCountdownNumber = countdownNumber;
        animator.SetTrigger(AnimatorHash);
        SoundManager.Instance.PlaySound(SoundKeys.WARNING, Vector3.zero);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIDeliveryResult : MonoBehaviour
{

    static readonly int AnimatorHash = Animator.StringToHash("Popup");

    [SerializeField] Image backgroundImage;
    [SerializeField] Image iconImage;
    [SerializeField] TextMeshProUGUI messageText;
    [SerializeField] Color successColor;
    [SerializeField] Color failedColor;
    [SerializeField] Sprite successSprite;
    [SerializeField] Sprite failedSprite;

    Animator animator;
    CanvasGroup _canvasGroup;
    GraphicRaycaster _graphicRaycaster;

    void Awake()
    {
        animator = GetComponent<Animator>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _graphicRaycaster = GetComponent<GraphicRaycaster>();
    }

    public void Initialize(Camera uiCamera)
    {
        var canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = uiCamera;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        if (_graphicRaycaster != null)
        {
            _graphicRaycaster.enabled = false;
        }

        gameObject.SetActive(false);
    }

    public void ShowFailed()
    {
        gameObject.SetActive(true);
        animator.SetTrigger(AnimatorHash);
        backgroundImage.color = failedColor;
        iconImage.sprite = failedSprite;
        messageText.text = "DELIVERY\nFAILED";
    }

    public void ShowSuccess()
    {
        gameObject.SetActive(true);
        animator.SetTrigger(AnimatorHash);
        backgroundImage.color = successColor;
        iconImage.sprite = successSprite;
        messageText.text = "DELIVERY\nSUCCESS";
    }

}

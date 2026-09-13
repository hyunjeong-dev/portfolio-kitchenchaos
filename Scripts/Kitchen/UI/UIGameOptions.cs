using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGameOptions : MonoBehaviour
{

    [SerializeField] Button soundEffectsButton;
    [SerializeField] Button musicButton;
    [SerializeField] Button closeButton;
    [SerializeField] TextMeshProUGUI soundEffectsText;
    [SerializeField] TextMeshProUGUI musicText;
    [SerializeField] TextMeshProUGUI moveUpText;
    [SerializeField] TextMeshProUGUI moveDownText;
    [SerializeField] TextMeshProUGUI moveLeftText;
    [SerializeField] TextMeshProUGUI moveRightText;
    [SerializeField] TextMeshProUGUI interactText;
    [SerializeField] TextMeshProUGUI interactAlternateText;
    [SerializeField] TextMeshProUGUI pauseText;
    [SerializeField] TextMeshProUGUI gamepadInteractText;
    [SerializeField] TextMeshProUGUI gamepadInteractAlternateText;
    [SerializeField] TextMeshProUGUI gamepadPauseText;

    KitchenInputModule _inputModule;
    Action _onCloseButtonAction;

    void Awake()
    {
        soundEffectsButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.ChangeSoundEffectsVolume();
            UpdateVisual();
        });
        musicButton.onClick.AddListener(() =>
        {
            SoundManager.Instance.ChangeMusicVolume();
            UpdateVisual();
        });
        closeButton.onClick.AddListener(() =>
        {
            Hide();
            _onCloseButtonAction?.Invoke();
        });
    }

    void Start()
    {
        UpdateVisual();
        Hide();
    }

    public void Initialize(KitchenGameContext context)
    {
        _inputModule = context.GetModule<KitchenInputModule>();
        UpdateVisual();
    }

    public void Uninitialize()
    {
        _inputModule = null;
        _onCloseButtonAction = null;
    }

    void UpdateVisual()
    {
        soundEffectsText.text = "Sound Effects: " + Mathf.Round(SoundManager.Instance.SoundEffectsVolume * 10f);
        musicText.text = "Music: " + Mathf.Round(SoundManager.Instance.MusicVolume * 10f);

        if (_inputModule == null)
        {
            return;
        }

        moveUpText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.MoveUp);
        moveDownText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.MoveDown);
        moveLeftText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.MoveLeft);
        moveRightText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.MoveRight);
        interactText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.Interact);
        interactAlternateText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.InteractAlternate);
        pauseText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.Pause);
        gamepadInteractText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.GamepadInteract);
        gamepadInteractAlternateText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.GamepadInteractAlternate);
        gamepadPauseText.text = _inputModule.GetBindingText(KitchenInputModule.Binding.GamepadPause);
    }

    public void Show(Action onCloseButtonAction)
    {
        _onCloseButtonAction = onCloseButtonAction;

        gameObject.SetActive(true);

        soundEffectsButton.Select();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

}

using Cysharp.Threading.Tasks;
using UnityEngine;

public class UIGamePause : MonoBehaviour
{
    [Header("Button")]
    [SerializeField] PButton backButton;
    [SerializeField] PButton retryButton;
    [SerializeField] PButton mainMenuButton;
    [SerializeField] PButton optionsButton;

    KitchenGameContext _context;
    UIGameOptions _optionsUI;
    bool _isClickLocked;

    public void Initialize()
    {
        if (backButton != null)
        {
            backButton.OnClick = OnClick;
        }

        if (retryButton != null)
        {
            retryButton.OnClick = OnClick;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.OnClick = OnClick;
        }

        if (optionsButton != null)
        {
            optionsButton.OnClick = OnClick;
        }
    }

    public void Uninitialize()
    {
        if (backButton != null)
        {
            backButton.OnClick = null;
        }

        if (retryButton != null)
        {
            retryButton.OnClick = null;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.OnClick = null;
        }

        if (optionsButton != null)
        {
            optionsButton.OnClick = null;
        }

        _context = null;
        _optionsUI = null;
        _isClickLocked = false;
    }

    public void Show(KitchenGameContext context, UIGameOptions optionsUI)
    {
        if (context == null)
        {
            return;
        }

        _context = context;
        _optionsUI = optionsUI;
        _isClickLocked = false;
        SetButtonsInteractable(true);

        gameObject.SetActive(true);

        if (backButton != null)
        {
            backButton.Select();
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void OnClick(PButton target)
    {
        if (_isClickLocked)
        {
            return;
        }

        if (target == backButton)
        {
            ResumeGame();
        }
        else if (target == retryButton)
        {
            LockButtons();
            RetryCurrentStage();
        }
        else if (target == mainMenuButton)
        {
            LockButtons();
            GoToMainMenu();
        }
        else if (target == optionsButton)
        {
            ShowOptions();
        }
    }

    void ResumeGame()
    {
        if (_context == null)
        {
            Debug.LogError("[UIGamePause] KitchenGameContext is not found.");
            return;
        }

        _context.TogglePauseGame();
    }

    void RetryCurrentStage()
    {
        if (_context == null)
        {
            Debug.LogError("[UIGamePause] KitchenGameContext is not found.");
            return;
        }

        var sceneChangeManager = SceneChangeManager.Instance;
        if (sceneChangeManager == null)
        {
            Debug.LogError("[UIGamePause] SceneChangeManager is not found.");
            return;
        }

        sceneChangeManager.SwitchGameScene(_context.StageLevel, true).Forget(Debug.LogException);
    }

    void GoToMainMenu()
    {
        var sceneChangeManager = SceneChangeManager.Instance;
        if (sceneChangeManager == null)
        {
            Debug.LogError("[UIGamePause] SceneChangeManager is not found.");
            return;
        }

        sceneChangeManager.SwitchScene(SceneChangeManager.SceneType.MainMenuScene).Forget(Debug.LogException);
    }

    void ShowOptions()
    {
        if (_optionsUI == null)
        {
            Debug.LogError("[UIGamePause] UIGameOptions is not found.");
            return;
        }

        Hide();
        _optionsUI.Show(ShowPause);
    }

    void ShowPause()
    {
        Show(_context, _optionsUI);
    }

    void LockButtons()
    {
        _isClickLocked = true;
        SetButtonsInteractable(false);
    }

    void SetButtonsInteractable(bool value)
    {
        if (backButton != null)
        {
            backButton.interactable = value;
        }

        if (retryButton != null)
        {
            retryButton.interactable = value;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.interactable = value;
        }

        if (optionsButton != null)
        {
            optionsButton.interactable = value;
        }
    }
}

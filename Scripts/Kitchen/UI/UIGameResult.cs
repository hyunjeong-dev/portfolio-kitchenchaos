using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

public sealed class UIGameResult : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI deliveredLabelText;
    [SerializeField] TextMeshProUGUI deliveredAmountText;
    [SerializeField] TextMeshProUGUI failedLabelText;
    [SerializeField] TextMeshProUGUI failedAmountText;
    [SerializeField] TextMeshProUGUI totalLabelText;
    [SerializeField] TextMeshProUGUI totalAmountText;
    [SerializeField] UILevelStar uiLevelStar;

    [Header("Button")]
    [SerializeField] PButton nextButton;
    [SerializeField] PButton retryButton;
    [SerializeField] PButton mainMenuButton;

    KitchenGameContext _context;
    int _nextStageLevel;
    bool _hasNextStage;
    bool _isClickLocked;

    public void Initialize()
    {
        if (nextButton != null)
        {
            nextButton.OnClick = OnClick;
        }

        if (retryButton != null)
        {
            retryButton.OnClick = OnClick;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.OnClick = OnClick;
        }
    }

    public void Uninitialize()
    {
        if (nextButton != null)
        {
            nextButton.OnClick = null;
        }

        if (retryButton != null)
        {
            retryButton.OnClick = null;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.OnClick = null;
        }

        _context = null;
        _nextStageLevel = 0;
        _hasNextStage = false;
        _isClickLocked = false;
    }

    public void Show(KitchenGameContext context)
    {
        if (context == null)
        {
            return;
        }

        _context = context;

        var deliveryModule = context.GetModule<KitchenDeliveryModule>();
        if (deliveryModule == null) return;

        var gameReportData = deliveryModule.GameReportData;
        var currentScore = gameReportData.CurrentScore;
        var successfulMenuCount = gameReportData.SuccessfulMenuCount;
        var successfulScore = gameReportData.SuccessfulScore;
        var failedOrderCount = gameReportData.FailedDeliveryCount + gameReportData.ExpiredMenuCount;
        var failedScore = gameReportData.FailedScore;

        var isSuccess = gameReportData.IsGoalAchieved;
        titleText.SetText(isSuccess ? "스테이지 {0} 성공!" : "스테이지 {0} 실패!", context.StageLevel);
        titleText.color = isSuccess ? new Color(0.55f, 1f, 0.23f, 1f) : new Color(1f, 0.32f, 0.24f, 1f);
        deliveredLabelText.SetText("배달된 주문 x {0}", successfulMenuCount);
        deliveredAmountText.SetText("{0}", successfulScore);
        failedLabelText.SetText("실패한 주문 x {0}", failedOrderCount);
        failedAmountText.SetText("{0}", failedScore);
        totalLabelText.SetText("합계");
        totalAmountText.SetText("{0}", currentScore);

        var grade = StageLogic.GetStageGrade(context.StageLevel, currentScore);
        SetLevelStar(grade);

        _isClickLocked = false;
        SetButtonsInteractable(true);

        _nextStageLevel = context.NextStageLevel;
        _hasNextStage = isSuccess && context.HasNextStage;
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(_hasNextStage);
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(true);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.gameObject.SetActive(true);
        }

        SetActive(true);
    }

    void OnClick(PButton target)
    {
        if (_isClickLocked)
        {
            return;
        }

        if (target == nextButton)
        {
            LockButtons();
            PlayNextStage();
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
    }

    public void Hide()
    {
        SetActive(false);
    }
    
    public void SetActive(bool value)
    {
        if (root != null)
        {
            root.SetActive(value);
        }
        
        gameObject.SetActive(value);
    }

    void LockButtons()
    {
        _isClickLocked = true;
        SetButtonsInteractable(false);
    }

    void SetButtonsInteractable(bool value)
    {
        if (nextButton != null)
        {
            nextButton.interactable = value;
        }

        if (retryButton != null)
        {
            retryButton.interactable = value;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.interactable = value;
        }
    }

    void SetLevelStar(int grade)
    {
        if (uiLevelStar != null)
        {
            uiLevelStar.gameObject.SetActive(true);
            uiLevelStar.Build(grade);
        }
    }

    void PlayNextStage()
    {
        if (_context == null)
        {
            Debug.LogError("[UIGameResult] KitchenGameContext is not found.");
            return;
        }

        if (!_hasNextStage)
        {
            Debug.LogWarning($"[UIGameResult] Next stage is not found. stageLevel: {_nextStageLevel}");
            return;
        }

        SwitchGameScene(_nextStageLevel, false);
    }

    void RetryCurrentStage()
    {
        if (_context == null)
        {
            Debug.LogError("[UIGameResult] KitchenGameContext is not found.");
            return;
        }

        SwitchGameScene(_context.StageLevel, true);
    }

    void GoToMainMenu()
    {
        var sceneChangeManager = SceneChangeManager.Instance;
        if (sceneChangeManager == null)
        {
            Debug.LogError("[UIGameResult] SceneChangeManager is not found.");
            return;
        }

        sceneChangeManager.SwitchScene(SceneChangeManager.SceneType.MainMenuScene).Forget(Debug.LogException);
    }

    void SwitchGameScene(int stageLevel, bool forceReload)
    {
        var sceneChangeManager = SceneChangeManager.Instance;
        if (sceneChangeManager == null)
        {
            Debug.LogError("[UIGameResult] SceneChangeManager is not found.");
            return;
        }

        sceneChangeManager.SwitchGameScene(stageLevel, forceReload).Forget(Debug.LogException);
    }
}

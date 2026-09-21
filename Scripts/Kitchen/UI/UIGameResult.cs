using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using TMPro;
using UnityEngine;
using static Generated.GameData;

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
    [SerializeField] PButton leaderboardButton;
    [SerializeField] UILeaderboard leaderboardView;

    readonly List<StageInfoData> _stageInfoDataList = new();
    readonly List<int> _leaderboardStageLevels = new();

    KitchenGameContext _context;
    IPlatformService _platformService;
    int _resultStageLevel;
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

        if (leaderboardButton != null)
        {
            leaderboardButton.OnClick = OnClick;
        }

        BindPlatformService();
        InitializeLeaderboardView();
        RefreshLeaderboardButton();
    }

    public void SetLeaderboardView(UILeaderboard value)
    {
        leaderboardView = value;
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

        if (leaderboardButton != null)
        {
            leaderboardButton.OnClick = null;
        }

        UnbindPlatformService();
        leaderboardView?.Uninitialize();

        _context = null;
        _resultStageLevel = 0;
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
        if (BindPlatformService())
        {
            InitializeLeaderboardView();
        }

        var stageResult = context.StageResult;
        if (!stageResult.IsValid)
        {
            Debug.LogError("[UIGameResult] Finalized stage result is not available.");
            return;
        }

        var isSuccess = stageResult.IsGoalAchieved;
        _resultStageLevel = stageResult.StageLevel;
        titleText.SetText(isSuccess ? "스테이지 {0} 성공!" : "스테이지 {0} 실패!", stageResult.StageLevel);
        titleText.color = isSuccess ? new Color(0.55f, 1f, 0.23f, 1f) : new Color(1f, 0.32f, 0.24f, 1f);
        deliveredLabelText.SetText("배달된 주문 x {0}", stageResult.SuccessfulMenuCount);
        deliveredAmountText.SetText("{0}", stageResult.SuccessfulScore);
        failedLabelText.SetText("실패한 주문 x {0}", stageResult.FailedOrderCount);
        failedAmountText.SetText("{0}", stageResult.FailedScore);
        totalLabelText.SetText("합계");
        totalAmountText.SetText("{0}", stageResult.FinalScore);

        SetLevelStar(stageResult.Grade);

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

        else if (target == leaderboardButton)
        {
            OpenLeaderboard(_resultStageLevel);
        }
    }

    public void OpenLeaderboard(int stageLevel)
    {
        if (leaderboardView == null || stageLevel <= 0)
        {
            return;
        }

        if (root != null) root.SetActive(false);
        leaderboardView.Open(stageLevel);
    }

    public void Hide()
    {
        leaderboardView?.Close(false);
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

        if (leaderboardButton != null)
        {
            RefreshLeaderboardButton(value);
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

    void InitializeLeaderboardView()
    {
        if (leaderboardView == null)
        {
            return;
        }

        _stageInfoDataList.Clear();
        _leaderboardStageLevels.Clear();
        if (GameData.Instance != null)
        {
            GameData.Instance.CollectStageInfoData(_stageInfoDataList);
            for (var i = 0; i < _stageInfoDataList.Count; i++)
            {
                var stageLevel = _stageInfoDataList[i].Level;
                if (stageLevel > 0)
                {
                    _leaderboardStageLevels.Add(stageLevel);
                }
            }
        }

        var leaderboardService = _platformService != null
            ? _platformService.Leaderboards
            : new UnavailablePlatformLeaderboardService("Platform manager is unavailable.");
        var currentUserName = _platformService != null
            ? _platformService.User.CurrentUser.DisplayName
            : string.Empty;
        leaderboardView.Initialize(
            leaderboardService,
            _leaderboardStageLevels,
            currentUserName,
            OnLeaderboardClosed);
    }

    bool BindPlatformService()
    {
        var platformManager = PlatformManager.Instance;
        var platformService = platformManager != null ? platformManager.Service : null;
        if (ReferenceEquals(_platformService, platformService))
        {
            return false;
        }

        UnbindPlatformService();
        _platformService = platformService;
        if (_platformService != null)
        {
            _platformService.OnStateChanged += OnPlatformStateChanged;
        }

        return true;
    }

    void UnbindPlatformService()
    {
        if (_platformService != null)
        {
            _platformService.OnStateChanged -= OnPlatformStateChanged;
            _platformService = null;
        }
    }

    void OnPlatformStateChanged(PlatformState state)
    {
        if (state != PlatformState.Ready &&
            leaderboardView != null &&
            leaderboardView.State != UILeaderboardState.Closed)
        {
            leaderboardView.Close();
        }

        RefreshLeaderboardButton();
    }

    void RefreshLeaderboardButton(bool allowInteraction = true)
    {
        if (leaderboardButton == null)
        {
            return;
        }

        var platformManager = PlatformManager.Instance;
        var isAvailable = leaderboardView != null &&
                          platformManager != null &&
                          platformManager.IsLeaderboardAvailable;
        leaderboardButton.gameObject.SetActive(isAvailable);
        leaderboardButton.interactable = allowInteraction && !_isClickLocked && isAvailable;
    }

    void OnLeaderboardClosed()
    {
        if (root != null) root.SetActive(true);
    }
}

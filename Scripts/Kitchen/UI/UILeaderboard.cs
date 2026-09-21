using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Common.Platform;
using TMPro;
using UnityEngine;

public sealed partial class UILeaderboard : MonoBehaviour
{
    const int GLOBAL_START_RANK = 1;
    const int GLOBAL_END_RANK = 10;

    public UILeaderboardState State { get; private set; } = UILeaderboardState.Closed;
    public int SelectedStageLevel { get; private set; }
    public int DisplayedStageLevel { get; private set; }
    public int DisplayedEntryCount { get; private set; }
    public bool HasMyRank { get; private set; }

    [SerializeField] GameObject _root;
    [SerializeField] GameObject _contentObject;
    [SerializeField] TMP_Text _stageText;
    [SerializeField] RectTransform _rankingRoot;
    [SerializeField] UILeaderboardCell cellAsset;
    [SerializeField] UILeaderboardCell myRankCell;
    [SerializeField] PButton _backButton;
    [SerializeField] GameObject _loadingObject;
    [SerializeField] GameObject _emptyObject;
    [SerializeField] GameObject _unavailableObject;
    [SerializeField] GameObject _errorObject;
    [SerializeField] TMP_Text _errorText;

    readonly List<UILeaderboardCell> _entries = new();
    readonly List<int> _stageLevels = new();

    IPlatformLeaderboardService _leaderboardService;
    CancellationTokenSource _lifetimeCancellationSource;
    CancellationTokenSource _displayCancellationSource;
    Task _nativeRequestTail = Task.CompletedTask;
    Action _onBack;
    string _currentUserName = "Current User";
    int _requestVersion;
    bool _isInitialized;

    public void Initialize(
        IPlatformLeaderboardService leaderboardService,
        IReadOnlyList<int> stageLevels,
        string currentUserName,
        Action onBack)
    {
        if (_isInitialized)
        {
            Uninitialize();
        }

        _leaderboardService = leaderboardService;
        _currentUserName = string.IsNullOrWhiteSpace(currentUserName)
            ? "Current User"
            : currentUserName;
        _onBack = onBack;
        _lifetimeCancellationSource = new CancellationTokenSource();
        _nativeRequestTail = Task.CompletedTask;

        _stageLevels.Clear();
        if (stageLevels != null)
        {
            for (var i = 0; i < stageLevels.Count; i++)
            {
                var stageLevel = stageLevels[i];
                if (stageLevel > 0 && !_stageLevels.Contains(stageLevel))
                {
                    _stageLevels.Add(stageLevel);
                }
            }
        }
        _stageLevels.Sort();

        EnsureEntryPool(GLOBAL_END_RANK - GLOBAL_START_RANK + 1);
        if (_backButton != null) _backButton.OnClick += OnClickBack;
        _isInitialized = true;
        Close(false);
    }

    public void Uninitialize()
    {
        CancelDisplayRequest();
        _lifetimeCancellationSource?.Cancel();
        _lifetimeCancellationSource?.Dispose();
        _lifetimeCancellationSource = null;

        if (_backButton != null) _backButton.OnClick -= OnClickBack;

        _leaderboardService = null;
        _onBack = null;
        _nativeRequestTail = Task.CompletedTask;
        _isInitialized = false;
        SetState(UILeaderboardState.Closed);
    }

    public void Open(int stageLevel)
    {
        if (!_isInitialized)
        {
            return;
        }

        if (_root != null)
        {
            _root.SetActive(true);
            _root.transform.SetAsLastSibling();
        }
        else
        {
            gameObject.SetActive(true);
        }

        var targetStageLevel = ResolveStageLevel(stageLevel);
        if (targetStageLevel <= 0)
        {
            SetState(UILeaderboardState.Empty);
            return;
        }

        SelectStage(targetStageLevel);
    }

    public void SelectStage(int stageLevel)
    {
        if (!_isInitialized || !_stageLevels.Contains(stageLevel))
        {
            return;
        }

        SelectedStageLevel = stageLevel;
        ClearDisplayedData();

        CancelDisplayRequest();
        _displayCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            _lifetimeCancellationSource.Token);
        var displayToken = _displayCancellationSource.Token;
        var requestVersion = ++_requestVersion;
        SetState(UILeaderboardState.Loading);

        if (_leaderboardService == null || !_leaderboardService.IsAvailable)
        {
            SetState(UILeaderboardState.Unavailable);
            return;
        }

        var previousRequest = _nativeRequestTail;
        var nativeRequest = LoadPageQueuedAsync(
            previousRequest,
            stageLevel,
            _lifetimeCancellationSource.Token,
            displayToken);
        _nativeRequestTail = nativeRequest;
        ApplyPageAsync(nativeRequest, stageLevel, requestVersion, displayToken)
            .Forget(Debug.LogException);
    }

    public void Close(bool notify = true)
    {
        CancelDisplayRequest();
        ++_requestVersion;
        SetState(UILeaderboardState.Closed);
        if (_root != null) _root.SetActive(false);
        else gameObject.SetActive(false);
        if (notify) _onBack?.Invoke();
    }

    async Task<LeaderboardPageResult> LoadPageQueuedAsync(
        Task previousRequest,
        int stageLevel,
        CancellationToken lifetimeCancellationToken,
        CancellationToken displayCancellationToken)
    {
        try
        {
            await previousRequest;
        }
        catch (Exception)
        {
            // 이전 요청 실패는 다음 Stage 조회를 막지 않는다.
        }

        if (lifetimeCancellationToken.IsCancellationRequested ||
            displayCancellationToken.IsCancellationRequested)
        {
            return LeaderboardPageResult.Cancelled(stageLevel);
        }

        var entries = await _leaderboardService.GetStageEntriesAsync(
            stageLevel,
            GLOBAL_START_RANK,
            GLOBAL_END_RANK,
            lifetimeCancellationToken);
        if (!entries.IsSuccess)
        {
            return LeaderboardPageResult.Failure(stageLevel, entries.Operation);
        }

        if (displayCancellationToken.IsCancellationRequested)
        {
            return LeaderboardPageResult.Cancelled(stageLevel);
        }

        var myRank = await _leaderboardService.GetCurrentUserEntryAsync(
            stageLevel,
            lifetimeCancellationToken);
        return new LeaderboardPageResult(stageLevel, entries, myRank);
    }

    async UniTask ApplyPageAsync(
        Task<LeaderboardPageResult> nativeRequest,
        int stageLevel,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        LeaderboardPageResult page;
        try
        {
            page = await nativeRequest.AsUniTask().AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            if (CanApply(stageLevel, requestVersion))
            {
                SetError(exception.Message);
            }
            return;
        }

        if (!CanApply(stageLevel, requestVersion))
        {
            return;
        }

        if (!page.Entries.IsSuccess)
        {
            ApplyFailure(page.Entries.Operation);
            return;
        }

        if (!page.MyRank.IsSuccess)
        {
            ApplyFailure(page.MyRank.Operation);
            return;
        }

        DisplayedStageLevel = page.StageLevel;
        DisplayedEntryCount = page.Entries.Entries.Count;
        RenderEntries(page.Entries.Entries);
        RenderMyRank(page.MyRank);
        SetState(DisplayedEntryCount == 0
            ? UILeaderboardState.Empty
            : UILeaderboardState.Success);
    }

    void ApplyFailure(PlatformOperationResult operation)
    {
        if (operation.Status == PlatformOperationStatus.Cancelled)
        {
            return;
        }

        if (operation.Status == PlatformOperationStatus.Unavailable)
        {
            SetState(UILeaderboardState.Unavailable);
            return;
        }

        SetError(operation.ErrorMessage);
    }

    void RenderEntries(IReadOnlyList<PlatformLeaderboardEntry> entries)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            var entryView = _entries[i];
            if (i < entries.Count)
            {
                var entry = entries[i];
                entryView.SetData(entry.Rank, entry.UserName, entry.Score);
            }
            else
            {
                entryView.gameObject.SetActive(false);
            }
        }
    }

    void RenderMyRank(PlatformLeaderboardEntryResult result)
    {
        HasMyRank = result.HasEntry;
        if (myRankCell == null)
        {
            return;
        }

        if (result.HasEntry)
        {
            var entry = result.Entry;
            myRankCell.SetData(entry.Rank, entry.UserName, entry.Score);
            return;
        }

        myRankCell.SetNoRecord(_currentUserName);
    }

    void SetError(string message)
    {
        if (_errorText != null)
        {
            _errorText.text = string.IsNullOrWhiteSpace(message)
                ? "Leaderboard data could not be loaded."
                : message;
        }
        SetState(UILeaderboardState.Error);
    }

    bool CanApply(int stageLevel, int requestVersion)
    {
        return _isInitialized &&
               State != UILeaderboardState.Closed &&
               SelectedStageLevel == stageLevel &&
               _requestVersion == requestVersion;
    }

    int ResolveStageLevel(int requestedStageLevel)
    {
        if (_stageLevels.Contains(requestedStageLevel))
        {
            return requestedStageLevel;
        }
        return _stageLevels.Count > 0 ? _stageLevels[0] : 0;
    }

    void SetState(UILeaderboardState state)
    {
        State = state;
        var showContent = state == UILeaderboardState.Success || state == UILeaderboardState.Empty;
        if (_contentObject != null) _contentObject.SetActive(showContent);
        if (_loadingObject != null) _loadingObject.SetActive(state == UILeaderboardState.Loading);
        if (_emptyObject != null) _emptyObject.SetActive(state == UILeaderboardState.Empty);
        if (_unavailableObject != null)
            _unavailableObject.SetActive(state == UILeaderboardState.Unavailable);
        if (_errorObject != null) _errorObject.SetActive(state == UILeaderboardState.Error);
        
        if(_stageText) _stageText.text = $"Stage {DisplayedStageLevel}";
    }

    void ClearDisplayedData()
    {
        DisplayedStageLevel = 0;
        DisplayedEntryCount = 0;
        HasMyRank = false;
        for (var i = 0; i < _entries.Count; i++)
        {
            _entries[i].gameObject.SetActive(false);
        }
        myRankCell?.SetNoRecord(_currentUserName);
        
        _stageText.text = string.Empty;
    }

    void CancelDisplayRequest()
    {
        _displayCancellationSource?.Cancel();
        _displayCancellationSource?.Dispose();
        _displayCancellationSource = null;
    }

    void OnClickBack(PButton button)
    {
        Close();
    }

    void OnDestroy()
    {
        Uninitialize();
    }

    readonly struct LeaderboardPageResult
    {
        public int StageLevel { get; }
        public PlatformLeaderboardEntriesResult Entries { get; }
        public PlatformLeaderboardEntryResult MyRank { get; }

        public LeaderboardPageResult(
            int stageLevel,
            PlatformLeaderboardEntriesResult entries,
            PlatformLeaderboardEntryResult myRank)
        {
            StageLevel = stageLevel;
            Entries = entries;
            MyRank = myRank;
        }

        public static LeaderboardPageResult Failure(
            int stageLevel,
            PlatformOperationResult operation)
        {
            return new LeaderboardPageResult(
                stageLevel,
                PlatformLeaderboardEntriesResult.Failure(stageLevel, operation),
                PlatformLeaderboardEntryResult.Failure(stageLevel, operation));
        }

        public static LeaderboardPageResult Cancelled(int stageLevel)
        {
            return Failure(stageLevel, PlatformOperationResult.Cancelled());
        }
    }
}

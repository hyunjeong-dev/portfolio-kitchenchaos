using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class UISaveConflictView : MonoBehaviour
{
    [SerializeField] TMP_Text _localSummary;
    [SerializeField] TMP_Text _cloudSummary;
    [SerializeField] PButton _localButton;
    [SerializeField] PButton _cloudButton;

    UniTaskCompletionSource<SaveConflictChoice> _choiceCompletionSource;

    void Awake()
    {
        if (_localButton != null) _localButton.OnClick += OnLocalSelected;
        if (_cloudButton != null) _cloudButton.OnClick += OnCloudSelected;
    }

    public async UniTask<SaveConflictChoice> ChooseAsync(
        SaveConflictData conflict, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _choiceCompletionSource != null)
        {
            return SaveConflictChoice.Defer;
        }

        if (_localSummary == null || _cloudSummary == null || _localButton == null || _cloudButton == null)
        {
            Debug.LogWarning("[UISaveConflictView] Missing UI references. Save selection deferred.");
            return SaveConflictChoice.Defer;
        }

        var completionSource = new UniTaskCompletionSource<SaveConflictChoice>();
        _choiceCompletionSource = completionSource;
        _localSummary.text = FormatSummary(conflict.Local);
        _cloudSummary.text = FormatSummary(conflict.Cloud);
        _localButton.interactable = true;
        _cloudButton.interactable = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);

        try
        {
            return await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SaveConflictChoice.Defer;
        }
        finally
        {
            if (_choiceCompletionSource == completionSource)
            {
                CompleteChoice(SaveConflictChoice.Defer);
            }
        }
    }

    void OnLocalSelected(PButton button) => CompleteChoice(SaveConflictChoice.Local);
    void OnCloudSelected(PButton button) => CompleteChoice(SaveConflictChoice.Cloud);

    void CompleteChoice(SaveConflictChoice choice)
    {
        var completionSource = _choiceCompletionSource;
        _choiceCompletionSource = null;
        gameObject.SetActive(false);
        completionSource?.TrySetResult(choice);
    }

    void OnDisable()
    {
        var completionSource = _choiceCompletionSource;
        _choiceCompletionSource = null;
        completionSource?.TrySetResult(SaveConflictChoice.Defer);
    }

    void OnDestroy()
    {
        if (_localButton != null) _localButton.OnClick -= OnLocalSelected;
        if (_cloudButton != null) _cloudButton.OnClick -= OnCloudSelected;
        OnDisable();
    }

    static string FormatSummary(SaveProgressData data)
    {
        var stage = data.BestStageLevel > 0 ? $"Stage {data.BestStageLevel}" : "클리어 기록 없음";
        var savedAt = "저장 기록 없음";
        if (data.UpdatedAt > 0)
        {
            try
            {
                savedAt = DateTimeOffset.FromUnixTimeSeconds(data.UpdatedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (ArgumentOutOfRangeException)
            {
                savedAt = "저장 시각 확인 불가";
            }
        }

        return $"최고 클리어\n{stage}\n\n해당 Stage 최고 점수\n{data.BestStageScore:N0}\n\n현재 Stage {data.CurrentStageLevel}\n마지막 저장\n{savedAt}";
    }
}

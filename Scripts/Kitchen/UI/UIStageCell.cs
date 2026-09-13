using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using TMPro;
using System.Threading;
using static Generated.GameData;

public class UIStageCell : MonoBehaviour
{
    public bool IsSelected => _isSelected;
    
    [SerializeField] PButton button;

    [SerializeField] TextMeshProUGUI stageLevelText;
    [SerializeField] Image stageImage;
    
    [SerializeField] UILevelStar uiLevelStar;
    [SerializeField] GameObject lockObject;
    [SerializeField] GameObject selectedObject;

    StageInfoData _stageInfoData;
    string _illustrationPath;
    
    bool _isClickLocked;
    bool _isSelected;
    float _clickLockEndTime;

    public void Build(StageInfoData stageInfoData)
    {
        _stageInfoData = stageInfoData;
        _isClickLocked = false;
        _clickLockEndTime = 0f;

        SetStageImageAsync(
                _stageInfoData.IllustrationPath,
                this.GetCancellationTokenOnDestroy())
            .Forget(Debug.LogException);
        
        var stageLevel = _stageInfoData.Level;
        SetStageLevel(stageLevel);

        var isUnlocked = StageLogic.IsUnlockStage(stageLevel);
        var grade = StageLogic.GetStageGrade(stageLevel);
        SetLevelStar(grade, isUnlocked);
        
        var nextLevel = StageLogic.GetNextStageLevel(StageLogic.CurrentStageLevel);
        SetSelected(stageInfoData.Level == nextLevel);
        
        SetLocked(!isUnlocked);
        SetButton(isUnlocked);
    }

    void SetStageLevel(int stageLevel)
    {
        if (stageLevelText == null)
        {
            return;
        }

        stageLevelText.SetText($"Stage {stageLevel}");
    }

    async UniTask SetStageImageAsync(string illustrationPath, CancellationToken cancellationToken)
    {
        if (stageImage == null)
        {
            return;
        }

        _illustrationPath = illustrationPath;
        stageImage.sprite = null;
        if (string.IsNullOrEmpty(illustrationPath))
        {
            stageImage.gameObject.SetActive(false);
            return;
        }

        var sprite = await AssetManager.Instance.LoadAsync<Sprite>(
            illustrationPath,
            AssetCacheScope.Feature,
            cancellationToken);
        if (_illustrationPath != illustrationPath)
        {
            return;
        }

        stageImage.sprite = sprite;
        stageImage.gameObject.SetActive(sprite != null);
    }

    void SetLevelStar(int grade, bool isUnlcked)
    {
        if (uiLevelStar != null)
        {
            uiLevelStar.gameObject.SetActive(isUnlcked);
            
            if (isUnlcked)
            {
                uiLevelStar.Build(grade);   
            }
        }
    }

    void SetLocked(bool value)
    {
        if (lockObject != null)
        {
            lockObject.SetActive(value);
        }
    }

    void SetSelected(bool value)
    {
        _isSelected = value;
        
        if (selectedObject != null)
        {
            selectedObject.SetActive(value);
        }
    }

    void SetButton(bool value)
    {
        if (button != null)
        {
            button.enabled = value;
            button.interactable = value;
            button.OnClick = OnClick;
        }
    }

    public void SetClickLockEndTime(float time)
    {
        _clickLockEndTime = time;
    }

    void OnClick(PButton target)
    {
        if (_isClickLocked || Time.unscaledTime < _clickLockEndTime)
            return;

        var level = _stageInfoData.Level; 
        if(!StageLogic.IsUnlockStage(level))
            return;

        _isClickLocked = true;
        SceneChangeManager.Instance.SwitchGameScene(level).Forget(Debug.LogException);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static Generated.GameData;

public class UIStageList : MonoBehaviour
{
    const float OPEN_CLICK_LOCK_SECONDS = 0.15f;

    [SerializeField] RectTransform root;
    [SerializeField] UIStageCell cellAsset;
    [SerializeField] ScrollRect scrollRect;
    [SerializeField] HorizontalLayoutGroup horizontalLayoutGroup;

    readonly List<UIStageCell> _cells = new();
    readonly List<StageInfoData> _stageInfoDataList = new();

    void OnEnable()
    {
        LockCellsForOpenInput();
    }

    public void Build()
    {
        _cells.Clear();
        _stageInfoDataList.Clear();
        GameData.Instance.CollectStageInfoData(_stageInfoDataList);

        var targetIndex = -1;
        for (var i = 0; i < _stageInfoDataList.Count; i++)
        {
            var stageInfoData = _stageInfoDataList[i];
            var cell = Instantiate(cellAsset, root);
            cell.Build(stageInfoData);

            if (cell.IsSelected)
            {
                targetIndex = _cells.Count;
            }

            _cells.Add(cell);
        }

        MoveToCenter(targetIndex);
        LockCellsForOpenInput();
    }

    void LockCellsForOpenInput()
    {
        var clickLockEndTime = Time.unscaledTime + OPEN_CLICK_LOCK_SECONDS;
        for (var i = 0; i < _cells.Count; i++)
        {
            var cell = _cells[i];
            if (cell != null)
            {
                cell.SetClickLockEndTime(clickLockEndTime);
            }
        }
    }

    void MoveToCenter(int targetIndex)
    {
        if (targetIndex < 0 || root == null || scrollRect == null || scrollRect.viewport == null)
        {
            return;
        }

        var cellSpacing = horizontalLayoutGroup.spacing;
        var cellRectTransform = cellAsset.transform as RectTransform;
        var cellWidth = cellRectTransform != null ? cellRectTransform.rect.width : 0f;
        var contentWidth = _cells.Count * cellWidth + Mathf.Max(0, _cells.Count - 1) * cellSpacing;
        var viewportWidth = scrollRect.viewport.rect.width;
        if (contentWidth <= 0f || viewportWidth <= 0f)
        {
            return;
        }

        var targetCenterX = targetIndex * (cellWidth + cellSpacing) + cellWidth * 0.5f;
        var minPositionX = Mathf.Min(0f, viewportWidth - contentWidth);
        var positionX = Mathf.Clamp(viewportWidth * 0.5f - targetCenterX, minPositionX, 0f);
        root.anchoredPosition = new Vector2(positionX, root.anchoredPosition.y);

        scrollRect.StopMovement();
    }
}

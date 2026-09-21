using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UILeaderboardStageButton : MonoBehaviour
{
    public int StageLevel { get; private set; }

    PButton _button;
    TMP_Text _label;
    Graphic _targetGraphic;
    Action<int> _onSelected;
    Color _normalColor;

    public void Initialize(int stageLevel, Action<int> onSelected)
    {
        StageLevel = stageLevel;
        _onSelected = onSelected;
        _button = GetComponent<PButton>();
        _label = GetComponentInChildren<TMP_Text>(true);
        _targetGraphic = _button != null ? _button.targetGraphic : GetComponent<Graphic>();
        _normalColor = _targetGraphic != null ? _targetGraphic.color : Color.white;

        if (_label != null) _label.SetText("Stage {0}", stageLevel);
        if (_button != null) _button.OnClick += OnClick;
    }

    public void SetSelected(bool selected)
    {
        if (_targetGraphic != null)
        {
            _targetGraphic.color = selected
                ? new Color(0.16f, 0.48f, 0.2f, 1f)
                : _normalColor;
        }
    }

    void OnClick(PButton button)
    {
        _onSelected?.Invoke(StageLevel);
    }

    void OnDestroy()
    {
        if (_button != null) _button.OnClick -= OnClick;
        _onSelected = null;
    }
}

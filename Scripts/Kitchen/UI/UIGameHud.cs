using System;
using UnityEngine;

public sealed class UIGameHud : MonoBehaviour
{
    [SerializeField] PButton _pauseButton;

    Action _onPauseClicked;
    bool _isInitialized;

    public void Initialize(Action onPauseClicked)
    {
        if (_isInitialized)
        {
            Uninitialize();
        }

        _onPauseClicked = onPauseClicked;

        if (_pauseButton != null)
        {
            _pauseButton.OnClick += OnClickPauseButton;
        }

        _isInitialized = true;
    }

    public void Uninitialize()
    {
        if (_pauseButton != null)
        {
            _pauseButton.OnClick -= OnClickPauseButton;
        }

        _onPauseClicked = null;
        _isInitialized = false;
    }

    public void ShowPauseButton()
    {
        SetPauseButtonVisible(true);
    }

    public void HidePauseButton()
    {
        SetPauseButtonVisible(false);
    }

    void OnDestroy()
    {
        Uninitialize();
    }

    void OnClickPauseButton(PButton target)
    {
        _onPauseClicked?.Invoke();
    }

    void SetPauseButtonVisible(bool value)
    {
        if (_pauseButton != null)
        {
            _pauseButton.gameObject.SetActive(value);
        }
    }
}

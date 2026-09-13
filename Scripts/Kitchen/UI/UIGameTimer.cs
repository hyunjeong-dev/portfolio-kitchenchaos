using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIGameTimer : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _timeText;
    [SerializeField] Image _timeBar;

    int _currentTotalSeconds = -1;

    public void InitializeTime(float timeRemaining)
    {
        _currentTotalSeconds = -1;
        UpdateTime(0f, timeRemaining);
    }

    public void UpdateTime(float timerNormalized, float timeRemaining)
    {
        if (_timeBar != null)
        {
            _timeBar.fillAmount = Mathf.Clamp01(1f - timerNormalized);
        }

        if (_timeText != null)
        {
            var totalSeconds = Mathf.CeilToInt(timeRemaining);
            if (_currentTotalSeconds == totalSeconds)
            {
                return;
            }

            _currentTotalSeconds = totalSeconds;
            var minutes = totalSeconds / 60;
            var seconds = totalSeconds % 60;
            _timeText.SetText("{0:00}:{1:00}", minutes, seconds);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class UIGameScore : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _scoreText;
    [SerializeField] TextMeshProUGUI _scoreBarText;
    [SerializeField] Image _scoreBar;

    public void UpdateScore(GameReportData gameReportData)
    {
        if (gameReportData == null)
        {
            SetScore(0, 0);
            return;
        }

        SetScore(gameReportData.CurrentScore, gameReportData.MaxGoalScore);
    }

    void SetScore(int currentScore, int goalScore)
    {
        if (_scoreText != null)
        {
            _scoreText.SetText("{0}", currentScore);
        }

        if (_scoreBar != null)
        {
            _scoreBar.fillAmount = goalScore > 0 ? Mathf.Clamp01((float)currentScore / goalScore) : 0f;
        }

        if (_scoreBarText != null)
        {
            _scoreBarText.SetText("{0} / {1}", currentScore, goalScore);
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

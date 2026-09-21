using TMPro;
using UnityEngine;

public sealed class UILeaderboardCell : MonoBehaviour
{
    public int Rank { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public int Score { get; private set; }
    public bool HasRecord { get; private set; }

    [SerializeField] TMP_Text _rankText;
    [SerializeField] TMP_Text _userNameText;
    [SerializeField] TMP_Text _scoreText;

    public void SetData(int rank, string userName, int score)
    {
        Rank = rank;
        UserName = userName ?? string.Empty;
        Score = score;
        HasRecord = rank > 0;

        _rankText?.SetText("{0}", rank);
        if (_userNameText != null) _userNameText.text = UserName;
        if (_scoreText != null) _scoreText.text = score.ToString("N0");
        gameObject.SetActive(true);
    }

    public void SetNoRecord(string userName)
    {
        Rank = 0;
        UserName = userName ?? string.Empty;
        Score = 0;
        HasRecord = false;

        if (_rankText != null) _rankText.text = "-";
        if (_userNameText != null) _userNameText.text = UserName;
        if (_scoreText != null) _scoreText.text = "-";
        gameObject.SetActive(true);
    }

}

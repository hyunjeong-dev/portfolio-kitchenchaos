using UnityEngine;
using UnityEngine.UI;

public sealed partial class UILeaderboard
{
    static readonly Color _rowColor = new(0.94f, 0.97f, 0.98f, 0.94f);
    static readonly Color _alternateRowColor = new(0.82f, 0.89f, 0.92f, 0.94f);


    void EnsureEntryPool(int count)
    {
        if (cellAsset == null || _rankingRoot == null)
        {
            return;
        }

        while (_entries.Count < count)
        {
            var entry = Instantiate(cellAsset, _rankingRoot, false);
            entry.name = $"Entry{_entries.Count + 1}";
            var image = entry.GetComponent<Image>();
            if (image != null)
            {
                image.color = _entries.Count % 2 == 0
                    ? _rowColor
                    : _alternateRowColor;
            }
            entry.gameObject.SetActive(false);
            _entries.Add(entry);
        }
    }
}

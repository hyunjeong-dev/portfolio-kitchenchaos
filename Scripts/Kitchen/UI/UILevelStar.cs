using UnityEngine;

public sealed class UILevelStar : MonoBehaviour
{
    [SerializeField] int _maxGrade = 3;
    [SerializeField] GUISkinSetterComponent[] _stars;

    public void Build(int grade)
    {
        var current = Mathf.Clamp(grade, 0, _maxGrade);
        for (var i = 0; i < _stars.Length; i++)
        {
            _stars[i].SetMode(i < current ? 1 : 0);
        }
    }
}

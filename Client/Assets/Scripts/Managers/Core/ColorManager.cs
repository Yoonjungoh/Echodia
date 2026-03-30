using UnityEngine;

public class ColorManager
{
    // 등급별 색상
    private static readonly Color NormalColor = new Color(0.78f, 0.78f, 0.78f, 1f);  // 회색
    private static readonly Color RareColor = new Color(0.25f, 0.55f, 1.00f, 1f);  // 파란색
    private static readonly Color EpicColor = new Color(0.65f, 0.25f, 1.00f, 1f);  // 보라색

    public Color GetGradeColor(ItemGrade grade)
    {
        return grade switch
        {
            ItemGrade.Rare => RareColor,
            ItemGrade.Epic => EpicColor,
            _ => NormalColor,
        };
    }
}

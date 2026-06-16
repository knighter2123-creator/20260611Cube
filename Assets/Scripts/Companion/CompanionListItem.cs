using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CompanionListItem : MonoBehaviour
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI fragmentText;

    private static readonly Color ColorNormal    = Color.white;
    private static readonly Color ColorRare      = new Color(0.6f, 0.2f, 0.8f);
    private static readonly Color ColorEpic      = new Color(1f, 0.4f, 0.7f);
    private static readonly Color ColorLegendary = new Color(0.6f, 1f, 0.4f);

    public void Setup(CompanionData data)
    {
        if (data.icon != null)
            iconImage.sprite = data.icon;

        nameText.text  = data.companionName;
        nameText.color = GetGradeColor(data.grade); // ✅ 등급 색상 적용

        int fragmentCount = CompanionFragment.Instance?.GetFragment(data) ?? 0;
        fragmentText.text = $"조각 : {fragmentCount}";
    }

    private Color GetGradeColor(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => ColorNormal,
            CompanionGrade.Rare      => ColorRare,
            CompanionGrade.Epic      => ColorEpic,
            CompanionGrade.Legendary => ColorLegendary,
            _                        => ColorNormal
        };
    }
}
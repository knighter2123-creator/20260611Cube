using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GachaResultItem : MonoBehaviour
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI resultText;

    // ✅ 등급별 색상
    private static readonly Color ColorNormal    = Color.white;
    private static readonly Color ColorRare      = new Color(0.6f, 0.2f, 0.8f); // 보라
    private static readonly Color ColorEpic      = new Color(1f, 0.4f, 0.7f);   // 분홍
    private static readonly Color ColorLegendary = new Color(0.6f, 1f, 0.4f);   // 연두

    public void Setup(GachaSystem.GachaResult result)
    {
        if (result.data.icon != null)
            iconImage.sprite = result.data.icon;

        nameText.text  = result.data.companionName;
        nameText.color = GetGradeColor(result.data.grade); // ✅ 등급 색상 적용

        if (result.isDuplicate)
        {
            resultText.text  = $"조각 +{result.fragmentGain}";
            resultText.color = Color.gray;
        }
        else
        {
            resultText.text  = "신규 획득!";
            resultText.color = Color.yellow;
        }
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
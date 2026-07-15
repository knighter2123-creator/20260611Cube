using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GachaResultItem : MonoBehaviour
{
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("등장 연출")]
    [SerializeField] private float appearDuration = 0.25f;

    // 등급별 색상
    private static readonly Color ColorNormal    = Color.white;
    private static readonly Color ColorRare      = new Color(0.6f, 0.2f, 0.8f); // 보라
    private static readonly Color ColorEpic      = new Color(1f, 0.4f, 0.7f);   // 분홍
    private static readonly Color ColorLegendary = new Color(0.6f, 1f, 0.4f);   // 연두

    // ✅ 매니저가 등급 기반 화면 흔들림을 판단하도록 노출
    public CompanionGrade Grade { get; private set; }

    public void Setup(GachaSystem.GachaResult result)
    {
        Grade = result.data.grade;

        if (result.data.icon != null)
            iconImage.sprite = result.data.icon;

        nameText.text  = result.data.companionName;
        nameText.color = GetGradeColor(result.data.grade);

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

        // ✅ 등장 전까지 숨김 (1프레임 풀사이즈 깜빡임 방지)
        transform.localScale = Vector3.zero;
    }

    // ✅ 매니저가 순서대로 호출. 자기 자신이 코루틴을 굴려서 파괴 시 자동 정리됨.
    public void PlayAppear()
    {
        StartCoroutine(AppearRoutine());
    }

    private IEnumerator AppearRoutine()
    {
        float t = 0f;
        while (t < appearDuration)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.one * EaseOutBack(t / appearDuration);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    // 끝에서 살짝 튀어나오는 탄성 이징
    private float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
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
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이어 레벨 / 경험치 HUD
/// Canvas 위에 배치하고 LevelUpManager의 이벤트를 구독합니다.
/// </summary>
public class PlayerExpUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private TextMeshProUGUI levelText;   // "Lv. 5"
    [SerializeField] private TextMeshProUGUI expText;     // "120 / 300"
    [SerializeField] private Slider          expSlider;   // 경험치 바

    // 구독한 매니저. 해제·표시 모두 Instance 가 아니라 이 대상을 씁니다
    // (매니저가 재생성되면 Instance 가 바뀌어도 옛 구독을 정확히 풀 수 있게).
    private LevelUpManager bound;

    void Awake()
    {
        if (expSlider != null)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
        }
    }

    void Start() => TryBind();

    void Update()
    {
        // LevelUpManager 가 나중에 만들어지거나 재생성돼도 붙을 때까지 재시도
        // (파괴된 매니저는 Unity 식 == null 로 걸리므로 재생성도 여기서 처리됩니다)
        if (bound == null) TryBind();
    }

    void OnDestroy() => Unbind();

    private void TryBind()
    {
        LevelUpManager lm = LevelUpManager.Instance;
        if (lm == null || lm == bound) return;

        Unbind();
        bound = lm;
        bound.OnExpChanged   += OnExpChanged;
        bound.OnLevelUp      += OnLevelChanged;
        bound.OnStatRestored += OnLevelChanged;   // 복원 시에도 UI는 갱신되어야 함

        Refresh();
    }

    private void Unbind()
    {
        if (bound != null)
        {
            bound.OnExpChanged   -= OnExpChanged;
            bound.OnLevelUp      -= OnLevelChanged;
            bound.OnStatRestored -= OnLevelChanged;
        }
        bound = null;
    }

    private void OnExpChanged(long _)  => Refresh();
    private void OnLevelChanged(int _) => Refresh();

    private void Refresh()
    {
        if (bound == null) return;

        long currentExp = bound.CurrentExp;
        long maxExp     = bound.MaxExp;

        if (levelText != null)
            levelText.text = $"Lv. {bound.CurrentLevel}";

        if (expText != null)
            expText.text = $"{currentExp} / {maxExp}";

        if (expSlider != null)
            expSlider.value = maxExp > 0 ? (float)currentExp / maxExp : 0f;
    }
}

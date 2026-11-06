using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>플레이어 레벨·경험치 HUD — LevelUpManager 이벤트를 구독해 갱신 (매니저가 재생성돼도 다시 붙음).</summary>
public class PlayerExpUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private TextMeshProUGUI levelText;   // "Lv. 5"
    [SerializeField] private TextMeshProUGUI expText;     // "120 / 300"
    [SerializeField] private Slider          expSlider;   // 경험치 바

    // 구독한 매니저 (해제·표시 모두 이 대상 — 재생성돼도 옛 구독을 정확히 풂)
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
        // 매니저가 나중에 생기거나 재생성돼도 붙을 때까지 재시도 (파괴는 Unity == null 로 걸림)
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

        // 경험치는 적을 잡을 때마다 바뀌므로 문자열을 만들지 않는 SetNumber(s) 를 씁니다 (TmpTextExtensions 참고)
        levelText.SetNumber("Lv. {0}", bound.CurrentLevel);
        expText.SetNumbers("{0} / {1}", currentExp, maxExp);

        if (expSlider != null)
            expSlider.value = maxExp > 0 ? (float)currentExp / maxExp : 0f;
    }
}

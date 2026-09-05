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

    private LevelUpManager bound;

    void Start() => TryBind();

    void Update()
    {
        // LevelUpManager 가 나중에 만들어져도 붙을 때까지 재시도
        if (bound == null) TryBind();
    }

    void OnDestroy() => Unbind();

    private void TryBind()
    {
        if (bound != null) return;
        if (LevelUpManager.Instance == null) return;

        bound = LevelUpManager.Instance;
        bound.OnExpChanged   += OnExpChanged;
        bound.OnLevelUp      += OnLevelUp;
        bound.OnStatRestored += OnLevelUp;   // 복원 시에도 UI는 갱신되어야 함

        Refresh();
    }

    private void Unbind()
    {
        if (bound == null) return;

        bound.OnExpChanged   -= OnExpChanged;
        bound.OnLevelUp      -= OnLevelUp;
        bound.OnStatRestored -= OnLevelUp;
        bound = null;
    }

    private void OnExpChanged(long currentExp) => Refresh();
    private void OnLevelUp(int newLevel)       => Refresh();

    private void Refresh()
    {
        if (LevelUpManager.Instance == null) return;

        int  level      = LevelUpManager.Instance.CurrentLevel;
        long currentExp = LevelUpManager.Instance.CurrentExp;
        long maxExp     = LevelUpManager.Instance.MaxExp;

        if (levelText != null)
            levelText.text = $"Lv. {level}";

        if (expText != null)
            expText.text = $"{currentExp} / {maxExp}";

        if (expSlider != null)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = 1f;
            expSlider.value    = maxExp > 0 ? (float)currentExp / maxExp : 0f;
        }
    }
}
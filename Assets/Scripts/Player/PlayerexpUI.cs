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

    void Start()
    {
        if (LevelUpManager.Instance == null)
        {
            Debug.LogWarning("[PlayerExpUI] LevelUpManager.Instance가 없습니다.");
            return;
        }

        // OnExpChanged : Action<int> — 현재 exp 전달
        LevelUpManager.Instance.OnExpChanged += OnExpChanged;
        // OnLevelUp    : Action<int> — 새 레벨 전달
        LevelUpManager.Instance.OnLevelUp    += OnLevelUp;

        Refresh();
    }

    void OnDestroy()
    {
        if (LevelUpManager.Instance == null) return;
        LevelUpManager.Instance.OnExpChanged -= OnExpChanged;
        LevelUpManager.Instance.OnLevelUp    -= OnLevelUp;
    }

    private void OnExpChanged(long currentExp) => Refresh();
    private void OnLevelUp(int newLevel)      => Refresh();

    private void Refresh()
    {
        int level      = LevelUpManager.Instance.CurrentLevel;
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
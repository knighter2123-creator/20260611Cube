using UnityEngine;
using TMPro;

/// <summary>
/// HUD의 Currency 텍스트 표시 전담.
/// 실제 Currency 데이터는 LevelUpManager(→ Stat.Currency)가 보관합니다.
/// 이 클래스는 OnCurrencyChanged 이벤트를 받아 텍스트만 갱신합니다.
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("HUD Currency 텍스트")]
    public TextMeshProUGUI currencyText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.OnCurrencyChanged += UpdateUI;
    }

    void OnDisable()
    {
        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.OnCurrencyChanged -= UpdateUI;
    }

    void Start()
    {
        // 초기 표시
        if (LevelUpManager.Instance != null)
            UpdateUI(LevelUpManager.Instance.CurrentCurrency);
    }

    // ── 내부 ──────────────────────────────────────
    private void UpdateUI(int amount)
    {
        if (currencyText != null)
            currencyText.text = FormatNumber(amount);
    }

    private string FormatNumber(int money)
    {
        if (money < 1000) return money.ToString();
        string[] units = { "", "K", "M", "G", "T" };
        int i = 0; double d = money;
        while (d >= 1000 && i < units.Length - 1) { d /= 1000; i++; }
        return d.ToString("F1") + units[i];
    }
}
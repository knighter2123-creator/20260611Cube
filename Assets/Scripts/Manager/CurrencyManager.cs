using System;
using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("HUD Currency 텍스트")]
    public TextMeshProUGUI currencyText;
    public TextMeshProUGUI gemText;       // ✅ 보석 UI 텍스트

    public event Action<int> OnGoldChanged;
    public event Action<int> OnGemChanged; // ✅ 보석 변경 이벤트

    private int gold = 0;
    private int gem  = 0;                 // ✅ 보석 필드

    public int Gold => gold;
    public int Gem  => gem;               // ✅ 보석 프로퍼티

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        UpdateGoldUI();
        UpdateGemUI();
    }

    // ── 골드 ───────────────────────────────────────

    public void AddGold(int amount)
    {
        gold += amount;
        UpdateGoldUI();
        OnGoldChanged?.Invoke(gold);
    }

    public bool SpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        UpdateGoldUI();
        OnGoldChanged?.Invoke(gold);
        return true;
    }

    // ── 보석 ───────────────────────────────────────

    public void AddGem(int amount)
    {
        gem += amount;
        UpdateGemUI();
        OnGemChanged?.Invoke(gem);
        Debug.Log($"[CurrencyManager] 보석 +{amount} | 현재 보석: {gem}");
    }

    public bool SpendGem(int amount)
    {
        if (gem < amount)
        {
            Debug.Log("[CurrencyManager] 보석이 부족합니다.");
            return false;
        }
        gem -= amount;
        UpdateGemUI();
        OnGemChanged?.Invoke(gem);
        return true;
    }

    // ── UI ─────────────────────────────────────────

    private void UpdateGoldUI()
    {
        if (currencyText != null)
            currencyText.text = "Gold : " + FormatNumber(gold);
    }

    private void UpdateGemUI()
    {
        if (gemText != null)
            gemText.text = "Gem : " + FormatNumber(gem);
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
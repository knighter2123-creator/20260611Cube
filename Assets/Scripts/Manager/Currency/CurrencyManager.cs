using System;
using Manager.currency;
using UnityEngine;
using TMPro;

public partial class CurrencyManager : MonoBehaviour
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
        // DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (SaveManager.Instance != null)
            ApplyFrom(SaveManager.Instance.Current);   // 저장된 골드/젬 복원 + UI 갱신
        else
        {
            UpdateGoldUI();
            UpdateGemUI();
        }
    }

    // ── 골드 ───────────────────────────────────────

    public void AddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount);
        UpdateGoldUI();
        OnGoldChanged?.Invoke(gold);
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || gold < amount) return false;
        AddGold(-amount);
        return true;
    }

    // ── 보석 ───────────────────────────────────────

    public void AddGem(int amount)
    {
        if (amount <= 0) return;
        gem += amount;
        UpdateGemUI();
        OnGemChanged?.Invoke(gem);
        Debug.Log($"[CurrencyManager] 보석 +{amount} | 현재 보석: {gem}");
    }

    public bool SpendGem(int amount)
    {
        if (amount <= 0 || gem < amount)
        {
            Debug.Log("[CurrencyManager] 보석이 부족합니다.");
            return false;
        }
        gem -= amount;                 // ★ 직접 차감
        UpdateGemUI();
        OnGemChanged?.Invoke(gem);
        Debug.Log($"[CurrencyManager] 보석 -{amount} | 현재 보석: {gem}");
        return true;
    }
    
    // === 통화 종류로 일반화 (ShopManager가 사용) ===
    public void AddCurrency(CurrencyType type, int amount)
    {
        switch (type)
        {
            case CurrencyType.Gold: AddGold(amount); break;
            case CurrencyType.Gem:  AddGem(amount);  break;
            // Cash는 외부 IAP에서 처리, 인게임 재화 아님
        }
    }
    
    public bool TrySpendCurrency(CurrencyType type, int amount)
    {
        switch (type)
        {
            case CurrencyType.Gold: return SpendGold(amount);
            case CurrencyType.Gem:  return SpendGem(amount);
            default: return false;
        }
    }

    // ── UI ─────────────────────────────────────────

    private void UpdateGoldUI()
    {
        if (currencyText != null)
            currencyText.text = FormatNumber(gold);
    }

    private void UpdateGemUI()
    {
        if (gemText != null)
            gemText.text = FormatNumber(gem);
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
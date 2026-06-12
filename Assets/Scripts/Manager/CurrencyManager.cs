using System;
using UnityEngine;
using TMPro;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    [Header("HUD Currency 텍스트")]
    public TextMeshProUGUI currencyText;
    public event Action<int> OnGoldChanged;
    
    private int gold = 0;
    public int Gold => gold;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start() => UpdateUI();

    public void AddGold(int amount)
    {
        gold += amount;
        UpdateUI();
        OnGoldChanged?.Invoke(gold);  // 추가
    }

    /// <summary>골드 소비. 성공 여부 반환 (LevelUpManager에서 호출)</summary>
    public bool SpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        UpdateUI();
        OnGoldChanged?.Invoke(gold);  // 추가
        return true;
    }

    private void UpdateUI()
    {
        if (currencyText != null)
            currencyText.text = "Gold : " + FormatNumber(gold);
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
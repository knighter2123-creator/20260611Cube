using System;
using Manager.currency;
using UnityEngine;

public partial class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    public event Action<int> OnGoldChanged;
    public event Action<int> OnGemChanged;

    private int gold = 0;
    private int gem  = 0;

    public int Gold => gold;
    public int Gem  => gem;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (SaveManager.Instance != null)
            ApplyFrom(SaveManager.Instance.Current);
    }

    // ── 골드 ──
    public void AddGold(int amount)
    {
        gold = Mathf.Max(0, gold + amount);
        OnGoldChanged?.Invoke(gold);     // UI는 이벤트로 갱신
    }

    public bool SpendGold(int amount)
    {
        if (amount <= 0 || gold < amount) return false;
        AddGold(-amount);
        return true;
    }

    // ── 보석 ──
    public void AddGem(int amount)
    {
        if (amount <= 0) return;
        gem += amount;
        OnGemChanged?.Invoke(gem);
        Debug.Log($"[CurrencyManager] 보석 +{amount} | 현재: {gem}");
    }

    public bool SpendGem(int amount)
    {
        if (amount <= 0 || gem < amount)
        {
            Debug.Log("[CurrencyManager] 보석이 부족합니다.");
            return false;
        }
        gem -= amount;
        OnGemChanged?.Invoke(gem);
        Debug.Log($"[CurrencyManager] 보석 -{amount} | 현재: {gem}");
        return true;
    }

    public void AddCurrency(CurrencyType type, int amount)
    {
        switch (type)
        {
            case CurrencyType.Gold: AddGold(amount); break;
            case CurrencyType.Gem:  AddGem(amount);  break;
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
}
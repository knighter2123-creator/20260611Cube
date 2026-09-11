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

    void OnDestroy()
    {
        // ★ 추가된 부분
        //   static 필드가 파괴된 오브젝트를 계속 붙잡고 있으면
        //   "null은 아닌데 쓸 수는 없는" 좀비 상태가 됩니다.
        //   유니티는 파괴된 오브젝트에 == null 을 true로 만들어주지만,
        //   그건 UnityEngine.Object 의 연산자 오버로딩 덕분이고
        //   ?. 같은 C# 문법은 그 오버로딩을 타지 않아 예외가 날 수 있습니다.
        //
        //   Instance == this 로 검사하는 이유는, 중복 인스턴스가 스스로 지워질 때
        //   살아 있는 진짜 Instance까지 null로 만들면 안 되기 때문입니다.
        if (Instance == this) Instance = null;
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
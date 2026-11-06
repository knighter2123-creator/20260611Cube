using System;
using Manager.currency;
using UnityEngine;

/// <summary>골드·보석 보유량 관리(소환권은 GachaTicket 에 위임) — 변경 시 이벤트로 UI 갱신, 더하기는 long 으로 계산해 int 범위로 고정(넘침 방지).</summary>
public partial class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance;

    public event Action<int> OnGoldChanged;
    public event Action<int> OnGemChanged;

    private int gold = 0;
    private int gem  = 0;

    public int Gold => gold;
    public int Gem  => gem;

    [Header("디버그")]
    [Tooltip("중복 제거·복원을 콘솔에 찍습니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logLifecycle = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 중복 제거는 정상 — 기존 루트까지 파괴돼 Instance 가 null 로 남는 경우를 잡으려고 로그
            if (logLifecycle)
                GameLog.Info($"[Currency] 중복 → 자기 제거. id={GetInstanceID()}, " +
                          $"기존 id={Instance.GetInstanceID()}, 씬={gameObject.scene.name}", this);

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        // static 이 파괴된 오브젝트를 붙잡지 않게 (?. 는 Unity 가짜 null 을 못 거름)
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        EnsureLoaded();
    }

    /// <summary>세이브에서 아직 안 읽었으면 읽음(여러 번 안전) — Start 는 일생에 한 번뿐이라 어긋나도 HUD 등이 늦게 깨울 수 있게 공개.</summary>
    public void EnsureLoaded()
    {
        if (loaded) return;

        if (SaveManager.Instance == null)
        {
            Debug.LogError("[Currency] SaveManager 가 없어 재화를 복원하지 못했습니다. " +
                           "Script Execution Order 에서 SaveManager 를 -100 으로 지정하세요.", this);
            return;
        }

        ApplyFrom(SaveManager.Instance.Current);

        if (logLifecycle)
            GameLog.Info($"[Currency] 복원 완료 — Gold {gold} / Gem {gem}. id={GetInstanceID()}", this);
    }

    // ── 골드 ──

    /// <summary>골드 증감(음수=차감), 결과는 0~int.MaxValue — int 덧셈은 넘치면 음수가 돼 골드가 0 으로 증발하므로 long 으로 계산 후 고정 (세이브 호환 위해 타입은 int 유지).</summary>
    public void AddGold(int amount)
    {
        long next = (long)gold + amount;   // ★ (long)(gold + amount) 는 int 덧셈이 먼저 넘쳐서 소용없음

        if (next > int.MaxValue) next = int.MaxValue;   // 위로 넘치면 최댓값에 멈춤
        if (next < 0)            next = 0;              // 아래로는 0 (기존 동작 그대로)

        gold = (int)next;
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

        // 골드와 같은 넘침 방어 (넘쳐 음수가 되면 영영 아무것도 못 삼)
        long next = (long)gem + amount;
        gem = next > int.MaxValue ? int.MaxValue : (int)next;

        OnGemChanged?.Invoke(gem);
        GameLog.Info($"[CurrencyManager] 보석 +{amount} | 현재: {gem}");
    }

    public bool SpendGem(int amount)
    {
        if (amount <= 0 || gem < amount)
        {
            GameLog.Info("[CurrencyManager] 보석이 부족합니다.");
            return false;
        }
        gem -= amount;
        OnGemChanged?.Invoke(gem);
        GameLog.Info($"[CurrencyManager] 보석 -{amount} | 현재: {gem}");
        return true;
    }

    // 재화 종류로 지급하는 공용 입구 (소환권은 GachaTicket 이 세이브에 직접 보관 — 넘겨주기만)
    public void AddCurrency(CurrencyType type, int amount)
    {
        switch (type)
        {
            case CurrencyType.Gold:        AddGold(amount);         break;
            case CurrencyType.Gem:         AddGem(amount);          break;
            case CurrencyType.GachaTicket: GachaTicket.Add(amount); break;
        }
    }

    // 재화 종류로 보유량을 확인하는 공용 입구 (차감 전에 버튼 상태/부족 안내를 정할 때).
    public bool Has(CurrencyType type, int amount)
    {
        if (amount <= 0) return true;
        switch (type)
        {
            case CurrencyType.Gold:        return gold >= amount;
            case CurrencyType.Gem:         return gem  >= amount;
            case CurrencyType.GachaTicket: return GachaTicket.Count >= amount;
            default: return false;
        }
    }

    public bool TrySpendCurrency(CurrencyType type, int amount)
    {
        switch (type)
        {
            case CurrencyType.Gold:        return SpendGold(amount);
            case CurrencyType.Gem:         return SpendGem(amount);
            case CurrencyType.GachaTicket: return GachaTicket.TrySpend(amount);
            default: return false;
        }
    }
}
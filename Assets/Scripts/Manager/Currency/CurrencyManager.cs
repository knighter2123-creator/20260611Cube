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

    [Header("디버그")]
    [Tooltip("인스턴스 생성/파괴/복원을 콘솔에 찍습니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logLifecycle = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // ★ 여기서 죽는 건 정상입니다. 문제는 "그 다음"입니다.
            //   이 오브젝트가 속한 새 Managers 루트가 통째로 제거되는 대신
            //   기존 루트가 제거되는 경우, 나(새 것)는 이미 자살했고
            //   기존 것도 파괴되어 Instance 가 null 로 남습니다.
            //   그 상태를 잡아내려고 아래 로그를 답니다.
            if (logLifecycle)
                Debug.Log($"[Currency] 중복 → 자기 제거. id={GetInstanceID()}, " +
                          $"기존 id={Instance.GetInstanceID()}, 씬={gameObject.scene.name}", this);

            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (logLifecycle)
            Debug.Log($"[Currency] Instance 등록. id={GetInstanceID()}, 씬={gameObject.scene.name}", this);
    }

    void OnDestroy()
    {
        // static 이 파괴된 오브젝트를 붙잡지 않게 정리.
        // ?. 는 유니티의 가짜 null 처리를 타지 않으므로 이 정리가 필요하다.
        if (Instance == this)
        {
            Instance = null;

            // ★ 이 로그가 찍히면 "살아 있어야 할 매니저가 죽었다"는 뜻입니다.
            //   Login → Main → Login 왕복 중에 이게 보이면 그게 범인입니다.
            if (logLifecycle)
                Debug.LogWarning($"[Currency] 살아 있던 Instance 가 파괴되었습니다. id={GetInstanceID()}. " +
                                 "이후 재화는 저장도 표시도 되지 않습니다.", this);
        }
    }

    void Start()
    {
        EnsureLoaded();
    }

    /// <summary>
    /// 세이브에서 아직 안 읽었으면 읽는다. 여러 번 불러도 안전하다.
    ///
    /// [왜 Start 에만 두지 않는가]
    ///   Start 는 이 오브젝트의 일생에 딱 한 번만 돕니다.
    ///   DontDestroyOnLoad 로 살아남는 매니저라면 앱 실행당 한 번뿐이라는 뜻이고,
    ///   그 한 번이 어긋나면 복구할 기회가 없습니다.
    ///   밖에서도 부를 수 있게 열어두면 HUD 나 다른 매니저가 늦게 깨워줄 수 있습니다.
    /// </summary>
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
            Debug.Log($"[Currency] 복원 완료 — Gold {gold} / Gem {gem}. id={GetInstanceID()}", this);
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
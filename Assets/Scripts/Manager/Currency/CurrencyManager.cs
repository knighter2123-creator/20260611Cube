using System;
using Manager.currency;
using UnityEngine;

/// <summary>
/// ★ 이번 수정: AddGold / AddGem 의 int 넘침(overflow) 방어. 나머지는 원본 그대로입니다.
/// </summary>
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

    /// <summary>
    /// 골드를 더하거나(양수) 뺍니다(음수). 결과는 0 ~ int.MaxValue 로 고정됩니다.
    ///
    /// ═══ ★ 이번에 고친 버그 — 넘치면 골드가 0이 되던 문제 ═══════════════
    ///
    /// 기존 코드:
    ///     gold = Mathf.Max(0, gold + amount);
    ///
    /// int 의 최댓값은 2,147,483,647 (약 21억) 입니다. 이걸 넘기면 C# 은 에러를 내지 않고
    /// **음수로 한 바퀴 돌아갑니다.** 이걸 오버플로(overflow)라고 해요.
    ///
    ///     gold   = 2,000,000,000
    ///     amount =   500,000,000
    ///     gold + amount = -1,794,967,296   ← 25억이 아니라 음수!
    ///     Mathf.Max(0, 음수) = 0            ← 골드 전부 증발
    ///
    /// 음수를 막으려고 넣은 Mathf.Max 가 오히려 **넘침을 0으로 둔갑시켜** 숨겨버립니다.
    /// 에러도 경고도 없이 21억 가까이 모은 골드가 한순간에 사라지는 거죠.
    /// 치트로 1억씩 넣다 보면 바로 재현되고, 정상 플레이에서도 후반에 닿을 수 있습니다.
    ///
    /// [고친 방법] 계산만 long(약 922경까지 표현)으로 해서 넘치지 않게 한 뒤,
    ///            결과를 int 범위로 잘라 넣습니다. 이런 걸 '포화 덧셈'이라고 합니다 —
    ///            한계에 닿으면 넘어가지 않고 한계에 딱 붙어 멈춥니다.
    ///
    /// [세이브에 영향이 없는 이유] gold 는 여전히 int 입니다. 저장 형식이 그대로라
    ///            기존 세이브를 읽는 데 아무 문제가 없습니다.
    ///            (learnings.md 의 "int → long 타입 변경이 값을 0으로 만든 사고"를
    ///             피하려고 타입은 건드리지 않고 계산만 바꿨습니다)
    /// ══════════════════════════════════════════════════════════════════
    /// </summary>
    public void AddGold(int amount)
    {
        long next = (long)gold + amount;   // ★ (long) 을 먼저 붙여야 덧셈이 long 으로 됩니다

        // ─── (long)gold + amount 와 (long)(gold + amount) 는 다릅니다 (학습 포인트) ──
        //   (long)(gold + amount) 는 괄호 안의 int 덧셈이 **먼저** 넘친 다음
        //   그 망가진 값을 long 으로 바꿉니다. 아무 소용이 없어요.
        //   한쪽을 먼저 long 으로 바꿔야 덧셈 자체가 long 으로 계산됩니다.
        // ─────────────────────────────────────────────────────────────────

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

        // ★ 골드와 같은 넘침 방어.
        //   여기는 Mathf.Max 도 없어서, 넘치면 보석이 **음수**가 됩니다.
        //   그러면 SpendGem 의 'gem < amount' 가 항상 참이라 영영 아무것도 못 삽니다.
        long next = (long)gem + amount;
        gem = next > int.MaxValue ? int.MaxValue : (int)next;

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
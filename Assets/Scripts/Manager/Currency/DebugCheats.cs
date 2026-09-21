using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 개발용 치트 모음. **릴리즈 빌드에서는 스스로 사라집니다.**
///
/// [배치] LoginScene → Managers 루트 → 자식으로 빈 오브젝트 "DebugCheats" 만들고 이 컴포넌트 추가
///
/// ─── 왜 Managers 루트에 둬도 되는가? (학습 포인트) ─────────────────────
/// 바로 전에 "PlayerAwakeningVisual 은 씬을 넘기면 안 된다"고 말씀드렸는데,
/// 이건 반대로 씬을 넘겨도 됩니다. 기준은 하나입니다 —
///
///   **씬 오브젝트를 붙잡고 있는가?**
///
///   PlayerAwakeningVisual — Player 의 SpriteRenderer 를 붙잡음 → 씬 바뀌면 참조가 죽음 → 넘기면 안 됨
///   DebugCheats            — 붙잡는 게 없음. 키를 누를 때마다 Instance 를 새로 물어봄 → 넘겨도 안전
///
/// 씬을 넘기면 한 번 배치로 메인 씬, 각성 씬, 가챠 어디서든 치트가 동작합니다.
/// ────────────────────────────────────────────────────────────────────
///
/// ─── 왜 CurrencyManager 에 넣지 않았는가 ──────────────────────────────
///   · 치트가 출시 빌드에 섞이면 안 됩니다. 한 파일에 모아야 그 파일 하나만 막으면 됩니다.
///   · CurrencyManager 는 '골드를 들고 저장하는' 데이터 매니저입니다.
///     '키보드를 감시하는' 일까지 맡기면 책임이 섞입니다.
///   · 치트는 계속 늘어납니다 (경험치, 레벨, 각성 기록 …). 한 곳에 모아두면 목록이 한눈에 보입니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
public class DebugCheats : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  인스펙터 설정
    // ══════════════════════════════════════════════════════════════
    //
    // ★ 이 필드들은 #if 로 감싸지 않습니다.
    //   직렬화되는 필드를 빌드 조건에 따라 있다 없다 하게 만들면, 유니티가
    //   "A scripted object has a different serialization layout" 에러를 낼 수 있습니다.
    //   (에디터에서 저장한 필드 구성과 빌드의 필드 구성이 달라서 생기는 문제)
    //   숫자 몇 개라 릴리즈에 남아도 아무 해가 없으니, **필드는 두고 로직만 막습니다.**

    // 릴리즈 빌드에서는 아래 필드를 읽는 코드가 전부 빠지므로
    // "값을 넣어놓고 안 쓴다(CS0414)" 경고가 납니다. 의도된 상황이라 그 경고만 끕니다.
#pragma warning disable 0414
    [Header("골드")]
    [Tooltip("누르면 소액 추가. Shift 를 누른 채 누르면 대량 추가.")]
    [SerializeField] private Key goldKey      = Key.G;

    [Tooltip("누르면 골드를 0으로. '골드 부족' 상태(빨간 비용 / 버튼 비활성) 테스트용.")]
    [SerializeField] private Key clearGoldKey = Key.H;

    [SerializeField] private int smallGoldAmount = 10_000;        // 1만
    [SerializeField] private int largeGoldAmount = 100_000_000;   // 1억
#pragma warning restore 0414

    // ─── 숫자 사이의 _ 는 무엇인가? (학습 포인트) ─────────────────────
    // C# 7부터 숫자 리터럴 안에 _ 를 넣을 수 있습니다. 컴파일러는 무시합니다.
    // 100000000 과 100_000_000 은 완전히 같은 값인데, 후자는 0 개수를 세지 않아도
    // 1억인 게 보입니다. 큰 수를 다루는 방치형 게임에서 특히 유용해요.
    // ────────────────────────────────────────────────────────────────

    // 치트가 두 개 생기면 키 한 번에 골드가 두 번 들어갑니다. 하나만 남깁니다.
    private static DebugCheats instance;

    // ══════════════════════════════════════════════════════════════
    //  라이프사이클
    // ══════════════════════════════════════════════════════════════

    void Awake()
    {
        // ★ 1차 방어 — 실행 시점 검사.
        //   Debug.isDebugBuild 는 에디터와 Development Build 에서 true,
        //   출시(릴리즈) 빌드에서 false 입니다.
        //   릴리즈에서는 이 컴포넌트를 즉시 제거해 아무 일도 못 하게 합니다.
        if (!Debug.isDebugBuild)
        {
            Destroy(this);
            return;
        }

        if (instance != null && instance != this)
        {
            Debug.LogWarning("[치트] DebugCheats 가 두 개 있습니다. 나중에 생긴 것을 제거합니다.", this);
            Destroy(this);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    // ★ 2차 방어 — 컴파일 시점 제외.
    //   아래 코드는 릴리즈 빌드에서 **아예 컴파일되지 않습니다.**
    //   Awake 의 Destroy 만으로도 동작은 막히지만, 코드 자체가 빌드에 남으면
    //   누군가 디컴파일해서 켤 수 있습니다. 치트는 흔적조차 안 남기는 게 원칙입니다.
    //
    //   클래스 전체를 #if 로 감싸지 않은 이유: 그러면 릴리즈 빌드에서 이 컴포넌트가
    //   "Missing Script" 가 되어 경고가 뜹니다. 클래스는 남기고 알맹이만 뺍니다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    void Start()
    {
        // 어떤 키가 있는지 까먹지 않게 시작할 때 한 번 알려줍니다.
        Debug.Log($"[치트] 활성 — {goldKey}: 골드 +{smallGoldAmount:N0}  |  " +
                  $"Shift+{goldKey}: +{largeGoldAmount:N0}  |  {clearGoldKey}: 골드 0");
    }

    void Update()
    {
        // ★ 모바일 기기에는 키보드가 없어서 Keyboard.current 가 null 입니다.
        //   가드 없이 kb[...] 에 접근하면 매 프레임 NullReferenceException 이 납니다.
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // wasPressedThisFrame — '누른 그 프레임'에만 true.
        // isPressed 를 쓰면 누르고 있는 동안 매 프레임 골드가 들어가 순식간에 넘칩니다.
        // (learnings.md 의 "토글형 입력에는 wasPressedThisFrame" 그대로입니다)
        if (WasPressed(kb, goldKey))
        {
            // Shift 는 '누르고 있는가'를 봐야 하므로 여기는 isPressed 가 맞습니다.
            bool large = kb.shiftKey.isPressed;
            AddGold(large ? largeGoldAmount : smallGoldAmount);
        }

        if (WasPressed(kb, clearGoldKey))
            ClearGold();
    }

    /// <summary>
    /// ★ [수정] Key.None 방어.
    ///   인스펙터에서 키를 None 으로 두면 "이 치트는 끈다"는 뜻으로 읽히지만,
    ///   kb[Key.None] 은 내부 배열의 -1 번을 찾으려다 **매 프레임 예외**를 던집니다.
    ///   None 이면 그냥 '안 눌렸다'로 처리합니다.
    /// </summary>
    private static bool WasPressed(Keyboard kb, Key key)
    {
        return key != Key.None && kb[key].wasPressedThisFrame;
    }

    // ══════════════════════════════════════════════════════════════
    //  치트 본체
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 골드를 안전하게 추가합니다.
    /// </summary>
    private void AddGold(int amount)
    {
        CurrencyManager cm = GetReadyCurrency();
        if (cm == null) return;

        // ★ 넘침 방지 — 남은 여유만큼만 넣습니다.
        //   골드는 int 라서 약 21억(int.MaxValue)이 한계입니다.
        //   CurrencyManager.AddGold 에도 같은 방어를 넣었지만(이번 수정),
        //   치트는 큰 수를 반복해서 넣는 게 일이라 여기서도 한 번 더 막습니다.
        int room = int.MaxValue - cm.Gold;
        int add  = Mathf.Min(amount, room);

        if (add <= 0)
        {
            Debug.Log($"[치트] 골드가 이미 최대치입니다 ({cm.Gold:N0}).");
            return;
        }

        // ★ 반드시 공개 API 를 거칩니다.
        //   gold 필드를 직접 바꾸면 OnGoldChanged 이벤트가 안 나가서
        //   CurrencyHUD 와 강화창의 "비용 빨간색 / 버튼 비활성" 이 갱신되지 않습니다.
        //   스탯 강화를 디버깅하려는 거라, 이 이벤트가 정확히 나가는 게 핵심입니다.
        cm.AddGold(add);

        Debug.Log($"[치트] 골드 +{add:N0} → 현재 {cm.Gold:N0}");
    }

    /// <summary>
    /// 골드를 0으로 만듭니다. '골드 부족' 상태 테스트용.
    /// </summary>
    private void ClearGold()
    {
        CurrencyManager cm = GetReadyCurrency();
        if (cm == null) return;

        int before = cm.Gold;
        if (before <= 0)
        {
            Debug.Log("[치트] 골드가 이미 0입니다.");
            return;
        }

        // SetGold 같은 함수가 없으니 '가진 만큼 쓰기'로 0을 만듭니다.
        // 역시 공개 API 경유라 이벤트가 정상적으로 나갑니다.
        cm.SpendGold(before);

        Debug.Log($"[치트] 골드 0으로 (기존 {before:N0})");
    }

    /// <summary>
    /// 치트를 써도 되는 상태의 CurrencyManager 를 돌려줍니다. 아니면 null.
    ///
    /// ═══ ★ 왜 EnsureLoaded() 를 먼저 부르는가 (중요) ═══════════════════
    ///
    /// CurrencyManager 는 Awake 에서 생기고, 세이브 값은 Start 에서야 읽습니다.
    /// 그 사이에 치트로 골드를 넣으면 이렇게 됩니다.
    ///
    ///   치트: gold = 0 + 10,000 = 10,000
    ///   Start → ApplyFrom(세이브) → gold = 세이브값   ← 치트 골드가 덮여서 사라짐
    ///
    /// 게다가 불러오기 전(loaded == false)에는 CaptureTo 가 저장도 안 합니다.
    /// 에러 없이 "치트가 안 먹는다"는 증상만 나와서 원인 찾기가 어렵습니다.
    ///
    /// EnsureLoaded() 는 이미 불렀으면 아무것도 안 하는 안전한 함수라서,
    /// 치트 직전에 부르면 "세이브 먼저 읽고 → 그 위에 더하기" 순서가 보장됩니다.
    /// CurrencyManager 에 '밖에서도 부를 수 있게 열어둔다'고 적혀 있는 게 바로 이런 용도예요.
    /// ════════════════════════════════════════════════════════════════
    /// </summary>
    private CurrencyManager GetReadyCurrency()
    {
        CurrencyManager cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogWarning("[치트] CurrencyManager 를 찾을 수 없습니다. " +
                             "메인 씬을 직접 실행했다면 LoginScene 부터 실행하세요.");
            return null;
        }

        cm.EnsureLoaded();

        if (!cm.IsLoaded)
        {
            Debug.LogWarning("[치트] 재화가 아직 세이브에서 복원되지 않았습니다. 잠시 후 다시 시도하세요.");
            return null;
        }

        return cm;
    }

    // ══════════════════════════════════════════════════════════════
    //  인스펙터 우클릭 메뉴 — Device Simulator 대안
    // ══════════════════════════════════════════════════════════════
    //
    // Device Simulator 로 테스트할 때는 창 포커스에 따라 키 입력이
    // 기대대로 들어오지 않을 수 있습니다. 이 컴포넌트를 우클릭하면 같은 치트를 쓸 수 있습니다.
    // (Play 중에만 동작합니다)

    [ContextMenu("치트: 골드 추가 (소액)")]
    private void MenuAddSmall()  { if (Application.isPlaying) AddGold(smallGoldAmount); }

    [ContextMenu("치트: 골드 추가 (대량)")]
    private void MenuAddLarge()  { if (Application.isPlaying) AddGold(largeGoldAmount); }

    [ContextMenu("치트: 골드 0으로")]
    private void MenuClearGold() { if (Application.isPlaying) ClearGold(); }

#endif
}
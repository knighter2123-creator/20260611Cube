using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>개발용 치트(G: 골드·보석 추가, Shift+G 대량, H: 0으로) — 출시 빌드에선 Awake 에서 스스로 제거되고 본체 코드는 컴파일에서도 빠짐, 출시에서 막기 쉽게 한 파일에 모음.</summary>
// [배치] LoginScene Managers 루트 자식 — 씬 오브젝트를 붙잡지 않고 매번 Instance 를 물어서 씬을 넘겨도 안전
public class DebugCheats : MonoBehaviour
{
    // ── 인스펙터 설정 ── 직렬화 필드를 #if 로 감싸면 serialization layout 에러가 나서 필드는 두고 로직만 막음 (릴리즈의 CS0414 는 의도라 끔)
#pragma warning disable 0414
    [Header("골드 · 보석")]
    [Tooltip("누르면 골드와 보석을 같은 수량만큼 소액 추가. Shift 를 누른 채 누르면 대량 추가.")]
    [SerializeField] private Key goldKey      = Key.G;

    [Tooltip("누르면 골드와 보석을 0으로. '재화 부족' 상태(빨간 비용 / 버튼 비활성) 테스트용.")]
    [SerializeField] private Key clearGoldKey = Key.H;

    // 골드와 보석에 같은 수량을 씁니다 (필드 이름은 인스펙터 값 유지를 위해 그대로)
    [SerializeField] private int smallGoldAmount = 10_000;        // 1만
    [SerializeField] private int largeGoldAmount = 100_000_000;   // 1억
#pragma warning restore 0414

    // 숫자 속 _ 는 자릿수 구분용(컴파일러가 무시) — 100_000_000 = 1억

    // 치트가 두 개면 키 한 번에 두 번 들어가므로 하나만 남김
    private static DebugCheats instance;

    // ── 라이프사이클 ──

    void Awake()
    {
        // 1차 방어: 출시 빌드(isDebugBuild=false)면 즉시 제거
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

    // 2차 방어: 본체는 릴리즈에서 컴파일 제외(디컴파일로 켜지 못하게) — 클래스째 빼면 Missing Script 가 되어 알맹이만 뺌
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    void Update()
    {
        // 모바일엔 키보드가 없어 null — 가드 없으면 매 프레임 예외
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // 누른 프레임에만 true (isPressed 면 누르는 동안 매 프레임 들어감)
        if (WasPressed(kb, goldKey))
        {
            // Shift 는 '누르고 있는가'라 isPressed
            bool large = kb.shiftKey.isPressed;
            AddGoldAndGem(large ? largeGoldAmount : smallGoldAmount);
        }

        if (WasPressed(kb, clearGoldKey))
        {
            ClearGold();
            ClearGem();
        }
    }

    /// <summary>키를 누른 프레임인가 — Key.None(치트 끔)은 kb[None] 이 매 프레임 예외를 던지므로 '안 눌림'으로 처리.</summary>
    private static bool WasPressed(Keyboard kb, Key key)
    {
        return key != Key.None && kb[key].wasPressedThisFrame;
    }

    // ── 치트 본체 ──

    /// <summary>골드와 보석을 같은 수량만큼 추가합니다.</summary>
    private void AddGoldAndGem(int amount)
    {
        AddGold(amount);
        AddGem(amount);
    }

    /// <summary>보석을 남은 여유만큼만 추가 (AddGold 와 같은 방식).</summary>
    private void AddGem(int amount)
    {
        CurrencyManager cm = GetReadyCurrency();
        if (cm == null) return;

        int room = int.MaxValue - cm.Gem;
        int add  = Mathf.Min(amount, room);

        if (add <= 0)
        {
            Debug.Log($"[치트] 보석이 이미 최대치입니다 ({cm.Gem:N0}).");
            return;
        }

        // 공개 API 경유 — OnGemChanged 가 나가야 HUD 가 갱신됩니다.
        cm.AddGem(add);

        Debug.Log($"[치트] 보석 +{add:N0} → 현재 {cm.Gem:N0}");
    }

    /// <summary>골드를 남은 여유(int.MaxValue 까지)만큼만 추가 — 치트는 큰 수를 반복해서 넣으니 한 번 더 막음.</summary>
    private void AddGold(int amount)
    {
        CurrencyManager cm = GetReadyCurrency();
        if (cm == null) return;

        int room = int.MaxValue - cm.Gold;
        int add  = Mathf.Min(amount, room);

        if (add <= 0)
        {
            Debug.Log($"[치트] 골드가 이미 최대치입니다 ({cm.Gold:N0}).");
            return;
        }

        // 공개 API 경유 — OnGoldChanged 가 나가야 HUD·강화창 비용 표시가 갱신됨
        cm.AddGold(add);

        Debug.Log($"[치트] 골드 +{add:N0} → 현재 {cm.Gold:N0}");
    }

    /// <summary>골드를 0으로 ('골드 부족' 테스트용).</summary>
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

        // SetGold 가 없어 '가진 만큼 쓰기'로 0 (공개 API 라 이벤트도 나감)
        cm.SpendGold(before);

        Debug.Log($"[치트] 골드 0으로 (기존 {before:N0})");
    }

    /// <summary>보석을 0으로 (ClearGold 와 같은 방식).</summary>
    private void ClearGem()
    {
        CurrencyManager cm = GetReadyCurrency();
        if (cm == null) return;

        int before = cm.Gem;
        if (before <= 0)
        {
            Debug.Log("[치트] 보석이 이미 0입니다.");
            return;
        }

        cm.SpendGem(before);

        Debug.Log($"[치트] 보석 0으로 (기존 {before:N0})");
    }

    /// <summary>치트를 써도 되는 CurrencyManager(아니면 null) — EnsureLoaded 를 먼저 불러 세이브를 읽은 위에 더함 (안 그러면 Start 의 복원이 치트 값을 덮음).</summary>
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

    // ── 인스펙터 우클릭 메뉴 — Device Simulator 에서 키 입력이 안 들어올 때 대안 (Play 중에만) ──

    [ContextMenu("치트: 골드·보석 추가 (소액)")]
    private void MenuAddSmall()  { if (Application.isPlaying) AddGoldAndGem(smallGoldAmount); }

    [ContextMenu("치트: 골드·보석 추가 (대량)")]
    private void MenuAddLarge()  { if (Application.isPlaying) AddGoldAndGem(largeGoldAmount); }

    [ContextMenu("치트: 골드·보석 0으로")]
    private void MenuClearGold() { if (Application.isPlaying) { ClearGold(); ClearGem(); } }

#endif
}
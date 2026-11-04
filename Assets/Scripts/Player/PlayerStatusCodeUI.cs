using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 스탯 창 — **코드 생성 버전.** 이 파일은 상태 · 보이기/숨기기 · 갱신을 담당합니다.
///
/// 항상 TabWindow 의 한 탭으로 씁니다 (열고 닫기·Dim·✕ 는 TabWindow 가 가짐).
///   ★ 이 컴포넌트는 창 '밖'의 항상 켜져 있는 오브젝트에 두고, Build Parent 에 탭 내용(Status_Content)을 연결하세요.
///   그러면 패널 · 텍스트를 전부 스크립트가 그 아래에 만듭니다.
///
/// ─────────────────────────────────────────────────────────────
/// [partial 로 두 파일에 나눈 기준 — AugmentSelectUI 와 같은 방식]
///
///   PlayerStatusCodeUI.cs        ← 지금 이 파일. 언제 열리고 무엇을 보여주는가
///   PlayerStatusCodeUI.Build.cs  ← 어떻게 생겼는가
///
/// 나누는 기준은 "이 코드가 언제 바뀌는가?" 하나입니다.
///   · 열리는 조건이 바뀔 때  → 이 파일
///   · 디자인이 바뀔 때       → Build.cs
///
/// 한 파일에 다 넣으면, "패널을 언제 닫을지" 고치려고 파일을 열었을 때
/// 둥근 사각형 텍스처를 픽셀 단위로 그리는 코드가 눈앞을 계속 지나갑니다.
/// partial 은 컴파일하면 완전히 같은 하나의 클래스라 **성능 차이는 0** 입니다.
///
/// ★ 제약 하나 — Update()/Awake()/Start() 는 클래스당 하나만 둘 수 있습니다.
///   partial 파일마다 하나씩 둘 수 없어서, 여기(코어)에만 두고
///   Build.cs 의 함수를 불러 씁니다.
/// ─────────────────────────────────────────────────────────────
/// </summary>
public partial class PlayerStatusCodeUI : MonoBehaviour, ITabPage
{
    // ══════════════════════════════════════════════════════════
    //  인스펙터 설정
    // ══════════════════════════════════════════════════════════
    [Header("생성 위치")]
    [Tooltip("이 RectTransform 아래에 UI 를 만듭니다 (탭 창의 Status_Content).\n" +
             "비워두면 씬의 첫 Canvas 아래에 만들어 탭과 무관하게 화면을 덮으니 꼭 연결하세요.")]
    [SerializeField] private RectTransform buildParent;

    [Tooltip("체크하면 Start 에서 UI 를 만듭니다. 끄면 처음 열 때 만듭니다(메모리 절약)")]
    [SerializeField] private bool buildOnStart = true;

    [Header("표시 설정")]
    [SerializeField] private PlayerStatusText.Style style = new PlayerStatusText.Style();

    // ══════════════════════════════════════════════════════════
    //  런타임 상태
    // ══════════════════════════════════════════════════════════
    private bool isOpen;

    // ★ bool 플래그가 아니라 '구독한 인스턴스' 를 들고 비교합니다.
    //   이 프로젝트의 매니저들은 씬 왕복 중 파괴 후 재생성되는 경우가 있어서,
    //   bool 로 관리하면 옛 인스턴스를 붙잡은 채 새 매니저의 이벤트를 영영 못 받습니다.
    //   CurrencyHUD 가 'Instance 가 아니라 bound 를 쓰는 이유' 로 적어둔 것과 같은 이야기입니다.
    private LevelUpManager   boundLm;
    private AugmentManager   boundAm;
    private CompanionManager boundCm;   // ★ 시너지 — 동료를 배치/회수하면 공격력(시너지 배율)이 바뀝니다

    /// <summary>패널이 열려 있는가.</summary>
    public bool IsOpen => isOpen;

    // 씬 전환으로 Player 가 새로 생기면 참조가 갈리므로 캐시하지 않고 매번 읽습니다.
    private PlayerStat Stat => Player.Instance != null ? Player.Instance.stat : null;

    // ══════════════════════════════════════════════════════════
    //  라이프사이클
    // ══════════════════════════════════════════════════════════

    private void Start()
    {
        // ★ Awake 가 아니라 Start 에서 하는 이유
        //   매니저들은 Awake 에서 Instance 를 세팅합니다. 같은 Awake 단계에서 접근하면
        //   실행 순서에 따라 Instance 가 아직 null 일 수 있습니다.
        //   Start 는 모든 Awake 가 끝난 뒤에 불리므로 안전합니다.

        if (buildOnStart) EnsureBuilt();   // → Build.cs
        TrySubscribe();
    }

    // ══════════════════════════════════════════════════════════
    //  ITabPage — TabWindow 가 부른다
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 이 탭이 선택됐다.
    /// ★ Open() 을 그대로 재사용합니다. 아직 안 만들었으면 여기서 만들고(EnsureBuilt),
    ///   구독을 확인하고, 전체를 다시 읽습니다. '열 때 무조건 다시 읽는다'는 원칙이
    ///   탭 전환에도 그대로 적용됩니다.
    /// </summary>
    public void OnTabShow() => Open();

    /// <summary>
    /// 다른 탭으로 넘어갔다.
    /// ★ isOpen 을 내려두는 게 핵심입니다. 이 스크립트는 구독을 OnDestroy 까지 유지하므로,
    ///   isOpen 이 true 로 남아 있으면 보이지도 않는 창을 위해 강화할 때마다 문자열을 조립합니다.
    /// </summary>
    public void OnTabHide() => Close();

    private void OnDestroy() => Unsubscribe();

    // ══════════════════════════════════════════════════════════
    //  이벤트 구독
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 두 매니저 각각에 대해 "지금 붙어 있어야 할 인스턴스" 와 비교해 갈아탑니다.
    /// 여러 번 불러도 안전하고, 탭을 열 때마다 불리므로 매니저가 재생성돼도 자동으로 복구됩니다.
    ///
    /// ★ 예전 코드의 문제
    ///   플래그 하나(subscribed)로 두 매니저를 함께 관리했습니다.
    ///
    ///     if (lm == null &amp;&amp; am == null) return;   // 둘 다 없을 때만 포기
    ///     ...
    ///     subscribed = true;                     // 하나만 붙어도 '완료'
    ///
    ///   LevelUpManager 는 있는데 AugmentManager 가 아직 없는 순간에 이게 돌면,
    ///   subscribed 가 true 로 굳어 **증강 구독을 영원히 못 합니다.**
    ///   증강 카드를 먹어도 열어둔 스탯창의 숫자가 안 바뀝니다. 에러는 나지 않고요.
    ///   구독 대상이 둘이면 상태도 둘이어야 합니다.
    /// </summary>
    private void TrySubscribe()
    {
        var lm = LevelUpManager.Instance;
        if (lm != boundLm)
        {
            UnbindLevelUp();
            BindLevelUp(lm);
        }

        var am = AugmentManager.Instance;
        if (am != boundAm)
        {
            UnbindAugment();
            BindAugment(am);
        }

        var cm = CompanionManager.Instance;
        if (cm != boundCm)
        {
            UnbindCompanion();
            BindCompanion(cm);
        }
    }

    /// <summary>
    /// ★ Instance 가 아니라 boundLm / boundAm 에서 해제합니다.
    ///   구독한 대상과 해제할 대상은 반드시 같아야 합니다. 그 사이에 Instance 가
    ///   다른 객체로 바뀌었다면, Instance 에서 빼는 건 엉뚱한 곳을 건드리는 것이고
    ///   원래 구독은 그대로 남아 누수가 됩니다.
    /// </summary>
    private void Unsubscribe()
    {
        UnbindLevelUp();
        UnbindAugment();
        UnbindCompanion();
    }

    // 구독(+=) 과 해제(-=) 목록을 한 쌍의 함수에만 둡니다.
    // 이벤트를 하나 추가할 때 두 함수만 나란히 고치면 되고, 한쪽만 고치는 실수가 줄어듭니다.

    private void BindLevelUp(LevelUpManager lm)
    {
        boundLm = lm;
        if (boundLm == null) return;

        boundLm.OnLevelUp      += HandleLevelChanged;
        boundLm.OnExpChanged   += HandleExpChanged;
        boundLm.OnStatRestored += HandleLevelChanged;
        boundLm.OnStatUpgraded += HandleStatUpgraded;
    }

    private void UnbindLevelUp()
    {
        if (boundLm != null)
        {
            boundLm.OnLevelUp      -= HandleLevelChanged;
            boundLm.OnExpChanged   -= HandleExpChanged;
            boundLm.OnStatRestored -= HandleLevelChanged;
            boundLm.OnStatUpgraded -= HandleStatUpgraded;
        }
        boundLm = null;
    }

    private void BindAugment(AugmentManager am)
    {
        boundAm = am;
        if (boundAm != null) boundAm.OnChanged += RefreshIfOpen;
    }

    private void UnbindAugment()
    {
        if (boundAm != null) boundAm.OnChanged -= RefreshIfOpen;
        boundAm = null;
    }

    private void BindCompanion(CompanionManager cm)
    {
        boundCm = cm;
        if (boundCm != null) boundCm.OnSynergyChanged += HandleSynergyChanged;
    }

    private void UnbindCompanion()
    {
        if (boundCm != null) boundCm.OnSynergyChanged -= HandleSynergyChanged;
        boundCm = null;
    }

    // 이벤트마다 시그니처가 달라서 얇은 래퍼를 하나씩 둡니다.
    // 람다(_ => Refresh())로 걸면 짧지만, 람다는 -= 로 해제할 수 없어 구독이 영원히 남습니다.
    private void HandleLevelChanged(int _)                     => RefreshIfOpen();
    private void HandleExpChanged(long _)                      => RefreshIfOpen();
    private void HandleStatUpgraded(LevelUpManager.StatType _) => RefreshIfOpen();
    private void HandleSynergyChanged(SynergyResult _)         => RefreshIfOpen();

    private void RefreshIfOpen()
    {
        if (isOpen) Refresh();
    }

    // ══════════════════════════════════════════════════════════
    //  보이기 / 숨기기 (탭 전환 — 연출 없이 즉시)
    // ══════════════════════════════════════════════════════════

    public void Open()
    {
        // ★ Start() 보다 먼저 불릴 수 있습니다 — TabWindow 는 탭 내용을 켠 '직후' OnTabShow 를 부르는데,
        //   Start 는 그 프레임 뒤쪽에 돕니다. 그래서 여기서도 만들기·구독을 확인합니다 (둘 다 여러 번 불러도 안전).
        EnsureBuilt();    // → Build.cs. 아직 안 만들었으면 지금 만듭니다
        TrySubscribe();   // 매니저가 없었거나 교체된 경우를 대비

        isOpen = true;
        if (panelRoot != null) panelRoot.SetActive(true);

        // ★ 이벤트를 놓쳤더라도 열 때 무조건 전체를 다시 읽습니다.
        //   "이벤트로만 갱신" 하는 UI 는 이벤트 하나가 빠지면 조용히 거짓 정보를 보여줍니다.
        Refresh();
    }

    public void Close()
    {
        isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════
    //  갱신 — 문자열은 PlayerStatusText 가 만들고, 여기서는 넣기만 합니다
    // ══════════════════════════════════════════════════════════

    /// <summary>화면의 모든 수치를 PlayerStat 에서 다시 읽어 갱신합니다.</summary>
    public void Refresh()
    {
        if (!built) return;

        PlayerStat s = Stat;   // null 이어도 됩니다. PlayerStatusText 가 "-" 를 돌려줍니다

        SetText(levelText, PlayerStatusText.Level(s));
        SetText(expText,   PlayerStatusText.Exp(s));

        if (expFillImage != null)
            expFillImage.fillAmount = PlayerStatusText.ExpRatio(s);

        Apply(damageTotal,      damageDetail,      PlayerStatusText.DamageTotal(s),      PlayerStatusText.DamageDetail(s, style));
        Apply(critDamageTotal,  critDamageDetail,  PlayerStatusText.CritDamageTotal(s),  PlayerStatusText.CritDamageDetail(s, style));
        Apply(attackSpeedTotal, attackSpeedDetail, PlayerStatusText.AttackSpeedTotal(s), PlayerStatusText.AttackSpeedDetail(s, style));
        Apply(critChanceTotal,  critChanceDetail,  PlayerStatusText.CritChanceTotal(s),  PlayerStatusText.CritChanceDetail(s, style));
    }

    private void Apply(TMP_Text total, TMP_Text detail, string totalValue, string detailValue)
    {
        SetText(total, totalValue);

        if (detail != null)
        {
            detail.text  = detailValue;
            detail.color = style.detailColor;
        }
    }

    private static void SetText(TMP_Text label, string value)
    {
        if (label != null) label.text = value;
    }

    // ══════════════════════════════════════════════════════════
    //  에디터 테스트
    // ══════════════════════════════════════════════════════════
    [ContextMenu("테스트: 스탯창 열기")]
    private void TestOpen() => Open();

    [ContextMenu("테스트: 지금 값으로 갱신")]
    private void TestRefresh() => Refresh();
}
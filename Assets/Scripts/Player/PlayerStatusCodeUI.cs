using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>플레이어 스탯 창(TabWindow 의 탭, UI 는 코드 생성) — 이 파일은 열기/닫기·매니저 구독·갱신, 모양은 .Build.cs, 문자열은 PlayerStatusText.</summary>
// [붙이는 위치] 창 '밖'의 항상 켜진 오브젝트, Build Parent 에 탭 내용(Status_Content) 연결 / Start 등 생명주기 함수는 이 파일에만 (partial 은 하나만 가능)
public partial class PlayerStatusCodeUI : MonoBehaviour, ITabPage
{
    // ── 인스펙터 설정 ──
    [Header("생성 위치")]
    [Tooltip("이 RectTransform 아래에 UI 를 만듭니다 (탭 창의 Status_Content).\n" +
             "비워두면 씬의 첫 Canvas 아래에 만들어 탭과 무관하게 화면을 덮으니 꼭 연결하세요.")]
    [SerializeField] private RectTransform buildParent;

    [Tooltip("체크하면 Start 에서 UI 를 만듭니다. 끄면 처음 열 때 만듭니다(메모리 절약)")]
    [SerializeField] private bool buildOnStart = true;

    [Header("표시 설정")]
    [SerializeField] private PlayerStatusText.Style style = new PlayerStatusText.Style();

    // ── 런타임 상태 ──
    private bool isOpen;

    // 구독한 인스턴스를 매니저마다 따로 기억 (bool 이면 재생성된 매니저 이벤트를 못 받고, 하나만 붙어도 완료로 굳음)
    private LevelUpManager   boundLm;
    private AugmentManager   boundAm;
    private CompanionManager boundCm;   // ★ 시너지 — 동료를 배치/회수하면 공격력(시너지 배율)이 바뀝니다

    /// <summary>패널이 열려 있는가.</summary>
    public bool IsOpen => isOpen;

    // 씬 전환으로 Player 가 새로 생기면 참조가 갈리므로 캐시하지 않고 매번 읽습니다.
    private PlayerStat Stat => Player.Instance != null ? Player.Instance.stat : null;

    // ── 라이프사이클 ──

    private void Start()
    {
        // 모든 Awake(매니저 Instance 설정) 뒤인 Start 에서

        if (buildOnStart) EnsureBuilt();   // → Build.cs
        TrySubscribe();
    }

    // ── ITabPage — TabWindow 가 부른다 ──

    /// <summary>탭 선택 — Open() 재사용(만들기·구독 확인·전체 다시 읽기).</summary>
    public void OnTabShow() => Open();

    /// <summary>다른 탭으로 — isOpen 을 내려 안 보이는 창을 위해 문자열을 만들지 않게 (구독은 OnDestroy 까지 유지).</summary>
    public void OnTabHide() => Close();

    private void OnDestroy() => Unsubscribe();

    // ── 이벤트 구독 ──

    /// <summary>매니저마다 현재 Instance 와 비교해 갈아탐 — 여러 번 안전, 탭을 열 때마다 불려 재생성돼도 자동 복구.</summary>
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

    /// <summary>Instance 가 아니라 구독했던 bound* 에서 해제 (그 사이 Instance 가 바뀌면 원래 구독이 누수).</summary>
    private void Unsubscribe()
    {
        UnbindLevelUp();
        UnbindAugment();
        UnbindCompanion();
    }

    // 구독/해제 목록은 한 쌍의 함수에만 (이벤트 추가 시 나란히 고쳐 한쪽 누락 방지)

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

    // 시그니처별 얇은 래퍼 (람다는 -= 로 해제할 수 없어 구독이 남음)
    private void HandleLevelChanged(int _)                     => RefreshIfOpen();
    private void HandleExpChanged(long _)                      => RefreshIfOpen();
    private void HandleStatUpgraded(LevelUpManager.StatType _) => RefreshIfOpen();
    private void HandleSynergyChanged(SynergyResult _)         => RefreshIfOpen();

    private void RefreshIfOpen()
    {
        if (isOpen) Refresh();
    }

    // ── 보이기 / 숨기기 (탭 전환 — 연출 없이 즉시) ──

    public void Open()
    {
        // Start 보다 먼저 불릴 수 있어(탭을 켠 직후 OnTabShow) 여기서도 만들기·구독 확인
        EnsureBuilt();    // → Build.cs. 아직 안 만들었으면 지금 만듭니다
        TrySubscribe();   // 매니저가 없었거나 교체된 경우를 대비

        isOpen = true;
        if (panelRoot != null) panelRoot.SetActive(true);

        // 열 때 무조건 전체를 다시 읽음 (이벤트만 믿으면 하나 놓쳐도 거짓 정보)
        Refresh();
    }

    public void Close()
    {
        isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // ── 갱신 — 문자열은 PlayerStatusText 가 만들고, 여기서는 넣기만 합니다 ──

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

    // ── 에디터 테스트 ──
    [ContextMenu("테스트: 스탯창 열기")]
    private void TestOpen() => Open();

    [ContextMenu("테스트: 지금 값으로 갱신")]
    private void TestRefresh() => Refresh();
}
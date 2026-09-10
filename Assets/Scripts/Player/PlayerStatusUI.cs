using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 스탯 창 — **프리팹 버전.**
///
/// 유니티에서 패널을 직접 만들고, TMP 텍스트와 버튼을 인스펙터로 연결합니다.
/// 코드로 전부 생성하는 버전은 PlayerStatusCodeUI 입니다. **둘 중 하나만 쓰세요.**
///
/// ─────────────────────────────────────────────────────────────
/// [이 스크립트가 지키는 원칙 세 가지]
///
/// ① UI 는 값을 소유하지 않는다.
///    여기에는 공격력을 저장하는 변수가 하나도 없습니다.
///    그릴 때마다 PlayerStat 에서 새로 읽습니다.
///    UI 가 사본을 들고 있으면 "화면은 500인데 실제는 725" 인 순간이 반드시 생깁니다.
///
/// ② UI 는 문장을 만들지 않는다.
///    "기본 20 · 강화 Lv.96 +480" 같은 문자열 조립은 PlayerStatusText 가 합니다.
///    그래서 코드 생성 버전과 이 프리팹 버전이 **항상 같은 값**을 보여줍니다.
///    이 파일에 남은 일은 "받은 문자열을 어느 TMP 에 넣을지" 뿐입니다.
///
/// ③ 매 프레임 계산하지 않는다.
///    Update() 가 없습니다. 값이 바뀌면 알려주는 이벤트가 이미 있으니까요.
///    방치형 모바일에서 매 프레임 문자열을 만드는 건 곧 배터리이고,
///    string 조립은 GC 쓰레기를 계속 만들어 프레임을 튀게 만듭니다.
///    게다가 패널이 닫혀 있으면 갱신 자체가 무의미합니다.
/// ─────────────────────────────────────────────────────────────
/// </summary>
public class PlayerStatusUI : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  인스펙터 연결
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 스탯 한 줄 = 합계 텍스트 + 세부 내역 텍스트.
    ///
    /// [왜 작은 클래스로 묶었나]
    /// 텍스트 8개를 그냥 필드로 늘어놓으면 인스펙터가
    /// damageTotal / damageDetail / critTotal / critDetail … 로 평평해집니다.
    /// 묶어두면 "Damage Line ▸ Total / Detail" 로 접히고,
    /// 스탯을 하나 더 추가할 때 필드 한 줄만 늘어납니다.
    /// </summary>
    [Serializable]
    public class StatLine
    {
        [Tooltip("최종 합계를 크게 보여주는 텍스트")]
        public TMP_Text total;

        [Tooltip("기본 · 강화 · 증강 내역을 작게 보여주는 텍스트 (비워두면 표시 생략)")]
        public TMP_Text detail;
    }

    [Header("열기 / 닫기")]
    [Tooltip("Status 버튼. 누르면 패널이 열립니다(다시 누르면 닫힘)")]
    [SerializeField] private Button openButton;

    [Tooltip("패널 안의 X 버튼 (없으면 비워두세요)")]
    [SerializeField] private Button closeButton;

    [Tooltip("패널의 최상위 오브젝트. 이걸 켜고 끕니다")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("패널 뒤쪽 반투명 영역. 눌러도 닫히게 하려면 Button 을 연결하세요")]
    [SerializeField] private Button dimmedBackgroundButton;

    [Header("레벨 / 경험치")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text expText;
    [Tooltip("경험치 게이지 (Image 의 Type 을 Filled 로 설정). 없으면 비워두세요")]
    [SerializeField] private Image expFillImage;

    [Header("전투 스탯")]
    [SerializeField] private StatLine damageLine;       // 공격력
    [SerializeField] private StatLine critDamageLine;   // 치명타 공격력
    [SerializeField] private StatLine attackSpeedLine;  // 공격 속도
    [SerializeField] private StatLine critChanceLine;   // 치명타 확률

    [Header("표시 설정")]
    [SerializeField] private PlayerStatusText.Style style = new PlayerStatusText.Style();

    // ══════════════════════════════════════════════════════════
    //  내부 상태
    // ══════════════════════════════════════════════════════════
    private bool isOpen;
    private bool subscribed;

    /// <summary>패널이 열려 있는가. 다른 UI 에서 참조할 수 있게 공개해 둡니다.</summary>
    public bool IsOpen => isOpen;

    // 씬 전환으로 Player 가 새로 생기면 참조가 갈리므로 캐시하지 않고 매번 읽습니다.
    private PlayerStat Stat => Player.Instance != null ? Player.Instance.stat : null;

    // ══════════════════════════════════════════════════════════
    //  라이프사이클
    // ══════════════════════════════════════════════════════════

    private void Awake()
    {
        // 버튼 연결을 코드로 하면 "누가 이 버튼을 듣고 있는지"가 파일 안에서 다 보입니다.
        if (openButton  != null) openButton.onClick.AddListener(Toggle);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (dimmedBackgroundButton != null) dimmedBackgroundButton.onClick.AddListener(Close);

        // 시작 상태는 항상 닫힘. 프리팹을 패널이 켜진 채로 저장하는 실수가 흔합니다.
        if (panelRoot != null) panelRoot.SetActive(false);
        isOpen = false;
    }

    private void Start()
    {
        // ★ 여기서 구독하는 이유
        //   매니저들은 Awake 에서 Instance 를 세팅합니다.
        //   같은 Awake 단계에서 구독하면 실행 순서에 따라 Instance 가 아직 null 일 수 있습니다.
        //   Start 는 모든 Awake 가 끝난 뒤에 불리므로 안전합니다.
        TrySubscribe();
    }

    private void OnDestroy()
    {
        // 구독은 반드시 해제합니다. 안 하면 파괴된 UI 를 이벤트가 계속 호출해
        // MissingReferenceException 이 뜨거나, 매니저가 죽은 오브젝트를 붙잡고 있어
        // 메모리가 새어 나갑니다.
        Unsubscribe();

        if (openButton  != null) openButton.onClick.RemoveListener(Toggle);
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        if (dimmedBackgroundButton != null) dimmedBackgroundButton.onClick.RemoveListener(Close);
    }

    // ══════════════════════════════════════════════════════════
    //  이벤트 구독
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 매니저가 아직 없을 수도 있으므로 "성공했을 때만" 구독 완료로 표시합니다.
    /// 실패했으면 패널을 열 때 다시 시도합니다.
    /// </summary>
    private void TrySubscribe()
    {
        if (subscribed) return;

        LevelUpManager lm = LevelUpManager.Instance;
        AugmentManager am = AugmentManager.Instance;

        if (lm == null && am == null) return;

        if (lm != null)
        {
            lm.OnLevelUp      += HandleLevelChanged;
            lm.OnExpChanged   += HandleExpChanged;
            lm.OnStatRestored += HandleLevelChanged;
            lm.OnStatUpgraded += HandleStatUpgraded;
        }

        if (am != null)
            am.OnChanged += RefreshIfOpen;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;

        LevelUpManager lm = LevelUpManager.Instance;
        if (lm != null)
        {
            lm.OnLevelUp      -= HandleLevelChanged;
            lm.OnExpChanged   -= HandleExpChanged;
            lm.OnStatRestored -= HandleLevelChanged;
            lm.OnStatUpgraded -= HandleStatUpgraded;
        }

        AugmentManager am = AugmentManager.Instance;
        if (am != null)
            am.OnChanged -= RefreshIfOpen;

        subscribed = false;
    }

    // 이벤트마다 시그니처가 달라서 얇은 래퍼를 하나씩 둡니다.
    // 람다로 걸면 짧지만, 람다는 -= 로 해제할 수 없어서 구독이 영원히 남습니다.
    private void HandleLevelChanged(int _)                     => RefreshIfOpen();
    private void HandleExpChanged(long _)                      => RefreshIfOpen();
    private void HandleStatUpgraded(LevelUpManager.StatType _) => RefreshIfOpen();

    private void RefreshIfOpen()
    {
        if (isOpen) Refresh();
    }

    // ══════════════════════════════════════════════════════════
    //  열기 / 닫기
    // ══════════════════════════════════════════════════════════

    public void Toggle()
    {
        if (isOpen) Close();
        else        Open();
    }

    public void Open()
    {
        TrySubscribe();   // Start 때 매니저가 없었던 경우를 대비

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
        PlayerStat s = Stat;   // null 이어도 됩니다. PlayerStatusText 가 "-" 를 돌려줍니다

        SetText(levelText, PlayerStatusText.Level(s));
        SetText(expText,   PlayerStatusText.Exp(s));

        if (expFillImage != null)
            expFillImage.fillAmount = PlayerStatusText.ExpRatio(s);

        Apply(damageLine,      PlayerStatusText.DamageTotal(s),      PlayerStatusText.DamageDetail(s, style));
        Apply(critDamageLine,  PlayerStatusText.CritDamageTotal(s),  PlayerStatusText.CritDamageDetail(s, style));
        Apply(attackSpeedLine, PlayerStatusText.AttackSpeedTotal(s), PlayerStatusText.AttackSpeedDetail(s, style));
        Apply(critChanceLine,  PlayerStatusText.CritChanceTotal(s),  PlayerStatusText.CritChanceDetail(s, style));
    }

    // ══════════════════════════════════════════════════════════
    //  안전한 텍스트 설정
    // ══════════════════════════════════════════════════════════
    //
    // 인스펙터 칸을 비워둘 수 있게 전부 null 검사를 통과시킵니다.
    // "경험치 게이지는 안 쓸래" 같은 선택을 코드 수정 없이 할 수 있고,
    // 연결을 하나 빠뜨렸을 때 NullReferenceException 으로 게임이 멈추지도 않습니다.

    private void Apply(StatLine line, string total, string detail)
    {
        if (line == null) return;

        SetText(line.total, total);

        if (line.detail != null)
        {
            line.detail.text  = detail;
            line.detail.color = style.detailColor;
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

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 스탯 창 — **코드 생성 버전.** 이 파일은 상태 · 열고 닫기 · 갱신을 담당합니다.
///
/// 씬 작업이 거의 없습니다. 빈 오브젝트에 이 컴포넌트 하나만 붙이면
/// 캔버스 · 버튼 · 패널 · 텍스트를 전부 스크립트가 만듭니다.
/// (프리팹으로 배치하는 버전은 PlayerStatusUI 입니다. **둘 중 하나만 쓰세요.**)
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
public partial class PlayerStatusCodeUI : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════
    //  인스펙터 설정 — 열기 / 닫기
    // ══════════════════════════════════════════════════════════
    [Header("Status 버튼")]
    [Tooltip("이미 만들어 둔 버튼을 쓰려면 여기에 연결하세요. 연결하면 아래 '버튼 자동 생성'은 무시됩니다")]
    [SerializeField] private Button externalOpenButton;

    [Tooltip("Status 버튼을 코드로 만들어 화면 모서리에 붙입니다")]
    [SerializeField] private bool createOpenButton = true;

    [SerializeField] private ScreenCorner buttonCorner = ScreenCorner.TopRight;
    [SerializeField] private Vector2      buttonOffset = new Vector2(28f, 28f);
    [SerializeField] private Vector2      buttonSize   = new Vector2(190f, 76f);
    [SerializeField] private string       buttonLabel  = "Status";

    [Header("생성 위치")]
    [Tooltip("비워두면 씬의 Canvas 를 찾고, 없으면 새로 만듭니다")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("체크하면 Start 에서 UI 를 만듭니다. 끄면 처음 열 때 만듭니다(메모리 절약)")]
    [SerializeField] private bool buildOnStart = true;

    [Header("표시 설정")]
    [SerializeField] private PlayerStatusText.Style style = new PlayerStatusText.Style();

    [Header("연출")]
    [Tooltip("열고 닫을 때 살짝 커지며 나타나는 연출")]
    [SerializeField] private bool  animate         = true;
    [SerializeField] private float animateDuration = 0.14f;

    /// <summary>화면 어느 모서리에 Status 버튼을 붙일지.</summary>
    public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    // ══════════════════════════════════════════════════════════
    //  런타임 상태
    // ══════════════════════════════════════════════════════════
    private bool isOpen;
    private bool subscribed;
    private Coroutine animRoutine;

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

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // ══════════════════════════════════════════════════════════
    //  이벤트 구독
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 매니저가 아직 없을 수도 있으므로 "성공했을 때만" 구독 완료로 표시하고,
    /// 실패하면 패널을 열 때 다시 시도합니다.
    /// 이렇게 두면 매니저가 나중에 생성되는 구조로 바뀌어도 UI 가 조용히 죽지 않습니다.
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
    // 람다(_ => Refresh())로 걸면 짧지만, 람다는 -= 로 해제할 수 없어 구독이 영원히 남습니다.
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
        EnsureBuilt();    // → Build.cs. 아직 안 만들었으면 지금 만듭니다
        TrySubscribe();   // Start 때 매니저가 없었던 경우를 대비

        isOpen = true;
        if (panelRoot != null) panelRoot.SetActive(true);

        // ★ 이벤트를 놓쳤더라도 열 때 무조건 전체를 다시 읽습니다.
        //   "이벤트로만 갱신" 하는 UI 는 이벤트 하나가 빠지면 조용히 거짓 정보를 보여줍니다.
        Refresh();

        PlayAnim(true);
    }

    public void Close()
    {
        isOpen = false;

        // 연출이 끝난 뒤에 꺼야 하므로 여기서 바로 SetActive(false) 하지 않습니다.
        PlayAnim(false);
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
    //  연출
    // ══════════════════════════════════════════════════════════

    private void PlayAnim(bool opening)
    {
        // 연출을 끈 경우 / 아직 안 만든 경우는 즉시 반영하고 끝냅니다.
        if (!animate || panelRoot == null)
        {
            if (panelRoot != null) panelRoot.SetActive(opening);
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = 1f;
            if (windowRect != null) windowRect.localScale = Vector3.one;
            return;
        }

        // 열고 닫기를 빠르게 반복해도 코루틴이 겹치지 않게 이전 것을 먼저 멈춥니다.
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(AnimateRoutine(opening));
    }

    private IEnumerator AnimateRoutine(bool opening)
    {
        float from = opening ? 0f : 1f;
        float to   = opening ? 1f : 0f;
        float t    = 0f;

        while (t < animateDuration)
        {
            // ★ unscaledDeltaTime 을 쓰는 이유
            //   증강 카드창이 열려 있으면 Time.timeScale 이 0 입니다.
            //   deltaTime 을 쓰면 그동안 연출이 아예 진행되지 않아 창이 멈춰 보입니다.
            //   "게임 시간"과 "UI 시간"은 분리해서 다뤄야 합니다.
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / animateDuration);
            ApplyAnimValue(Mathf.Lerp(from, to, EaseOut(k)));
            yield return null;
        }

        ApplyAnimValue(to);

        // 닫는 연출이 끝난 뒤에 비활성화합니다.
        if (!opening) panelRoot.SetActive(false);

        animRoutine = null;
    }

    private void ApplyAnimValue(float k)
    {
        if (panelCanvasGroup != null) panelCanvasGroup.alpha = k;

        // 0.92 → 1.0 로 살짝만 커집니다. 크게 튀면 저사양 기기에서 어지럽습니다.
        if (windowRect != null)
            windowRect.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
    }

    /// <summary>끝에서 부드럽게 감속. 1-(1-k)^3 (ease-out cubic)</summary>
    private static float EaseOut(float k)
    {
        float inv = 1f - k;
        return 1f - inv * inv * inv;
    }

    // ══════════════════════════════════════════════════════════
    //  에디터 테스트
    // ══════════════════════════════════════════════════════════
    [ContextMenu("테스트: 스탯창 열기")]
    private void TestOpen() => Open();

    [ContextMenu("테스트: 지금 값으로 갱신")]
    private void TestRefresh() => Refresh();
}

using System.Collections;
using UnityEngine;

/// <summary>
/// 각성 단계에 맞춰 플레이어 스프라이트를 갈아끼웁니다.
/// 플레이어 오브젝트(또는 그 자식 중 SpriteRenderer가 있는 곳)에 붙이세요.
///
/// ─── 왜 Player.cs에 넣지 않고 별도 컴포넌트로 뺐는가? (학습 포인트) ──────
/// Player.cs는 "타겟을 찾고 공격한다"는 전투 책임을 집니다.
/// 여기에 "어떻게 생겼는가"를 섞으면, 나중에 겉모습 연출을 늘릴 때마다
/// (오라 파티클, 색 변화, 각성 연출…) 전투 코드 파일이 계속 두꺼워집니다.
///
/// 컴포넌트를 분리하면 이런 것도 공짜로 따라옵니다:
///   · 동료(Companion)에게도 같은 스크립트를 붙여 재사용 가능
///   · 이 기능만 통째로 꺼보고 싶을 때 컴포넌트 체크만 해제
///   · Player.cs를 고칠 때 이 코드가 눈앞을 지나가지 않음
/// ──────────────────────────────────────────────────────────────────────
///
/// ═══ 연출이 재생되는 시점 (중요) ═══════════════════════════════════════
/// 각성 클리어는 EvolveScene에서 일어나고 **곧바로 씬이 전환됩니다.**
/// 그래서 "스프라이트가 바뀌면 연출을 재생"하게 짜면, 연출이 씬과 함께
/// 한두 프레임 만에 사라져 플레이어 눈에는 아무것도 안 보입니다.
///
/// 대신 AwakeningManager가 "축하할 일이 생겼다"는 예약만 들고 있다가,
/// 메인 스테이지에 도착한 **새 플레이어의 Start()에서** 그걸 가져가 재생합니다.
///
/// 덤으로, 상점/가챠를 왕복할 때마다 연출이 터지는 문제도 같이 해결됩니다.
/// (그 경우는 '각성'이 아니라 '복원'이라 예약이 없습니다 —
///  LevelUpManager에서 OnLevelUp과 OnStatRestored를 나눈 것과 같은 구분입니다)
/// ══════════════════════════════════════════════════════════════════════
/// </summary>
public class PlayerAwakeningVisual : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("비워두면 자신 → 자식 순서로 SpriteRenderer를 찾습니다.")]
    [SerializeField] private SpriteRenderer targetRenderer;

    [Tooltip("각성 0단계(기본) 스프라이트. 비워두면 시작할 때의 스프라이트를 자동으로 기억합니다.")]
    [SerializeField] private Sprite baseSprite;

    [Header("연출 (선택)")]
    [Tooltip("각성으로 단계가 오른 직후 한 번만 재생됩니다. 씬 왕복 시에는 재생되지 않습니다.")]
    [SerializeField] private bool punchOnAwaken = true;

    [Tooltip("연출이 건드릴 Transform. 비워두면 이 컴포넌트가 붙은 오브젝트입니다.\n" +
             "플레이어 본체에 다른 크기 연출이 있다면 스프라이트 자식을 따로 연결하세요.")]
    [SerializeField] private Transform punchTarget;

    [SerializeField] private float punchScale    = 1.25f;
    [SerializeField] private float punchDuration = 0.25f;

    // ★ bool 플래그가 아니라 '구독한 인스턴스'를 들고 비교합니다.
    //   PlayerStatusCodeUI에 적어두신 것과 같은 이유 — 매니저가 재생성되면
    //   bool로는 옛 인스턴스를 붙잡은 채 새 매니저의 이벤트를 영영 못 받습니다.
    private AwakeningManager boundAm;

    private Coroutine punchRoutine;
    private Vector3   baseScale;

    // ══════════════════════════════════════════════
    //  라이프사이클
    // ══════════════════════════════════════════════

    void Awake()
    {
        ResolveRenderer();

        if (punchTarget == null) punchTarget = transform;

        // ★ 원래 크기는 여기서 딱 한 번 기억합니다.
        //   연출 코루틴 안에서 늦게 기억하면, 연출 도중에 다시 호출됐을 때
        //   '커진 상태의 크기'를 원래 크기로 착각할 위험이 있습니다.
        baseScale = punchTarget.localScale;

        // 시작 시점의 모습을 '기본 모습'으로 기억해 둡니다.
        // 각성을 초기화(세이브 삭제)했을 때 되돌아갈 곳이 필요하니까요.
        if (baseSprite == null && targetRenderer != null)
            baseSprite = targetRenderer.sprite;
    }

    void OnEnable()
    {
        TrySubscribe();
        ApplySprite();   // 조용히 현재 단계 반영
    }

    void Start()
    {
        // ★ OnEnable 시점에는 AwakeningManager가 아직 없을 수 있습니다.
        //   매니저는 Awake에서 Instance를 세팅하는데 실행 순서는 보장되지 않아요.
        //   Start는 모든 Awake가 끝난 뒤라 여기서 한 번 더 시도합니다. (멱등)
        TrySubscribe();
        ApplySprite();

        // 각성해서 넘어온 길이면 여기서 축하 연출을 재생합니다.
        // ConsumeCelebration()은 가져가면 예약이 지워지므로 딱 한 번만 터집니다.
        if (punchOnAwaken && boundAm != null && boundAm.ConsumeCelebration())
            PlayPunch();
    }

    void OnDisable()
    {
        Unsubscribe();

        if (punchRoutine != null) { StopCoroutine(punchRoutine); punchRoutine = null; }
        if (punchTarget != null) punchTarget.localScale = baseScale;
    }

    // ══════════════════════════════════════════════
    //  구독
    // ══════════════════════════════════════════════

    private void TrySubscribe()
    {
        AwakeningManager am = AwakeningManager.Instance;
        if (am == boundAm) return;

        if (boundAm != null) boundAm.OnChanged -= HandleAwakeningChanged;

        boundAm = am;

        if (boundAm != null) boundAm.OnChanged += HandleAwakeningChanged;
    }

    private void Unsubscribe()
    {
        // ★ Instance가 아니라 boundAm에서 해제합니다.
        //   구독한 대상과 해제할 대상이 다르면 원래 구독이 남아 누수가 됩니다.
        if (boundAm != null) boundAm.OnChanged -= HandleAwakeningChanged;
        boundAm = null;
    }

    /// <summary>
    /// 각성 상태가 변했을 때. 스프라이트만 조용히 갈아끼웁니다.
    ///
    /// 여기서 연출을 재생하지 않는 이유는 클래스 주석의 '연출이 재생되는 시점' 참고.
    /// (이 시점은 대개 EvolveScene이고, 곧바로 씬이 전환됩니다)
    /// </summary>
    private void HandleAwakeningChanged() => ApplySprite();

    // ══════════════════════════════════════════════
    //  적용
    // ══════════════════════════════════════════════

    /// <summary>현재 각성 단계에 맞는 스프라이트로 갱신합니다. (연출 없음)</summary>
    public void ApplySprite()
    {
        if (targetRenderer == null) ResolveRenderer();
        if (targetRenderer == null) return;

        // 구독한 매니저 기준으로 읽습니다 (아직 구독 전이면 지금 Instance).
        AwakeningManager am = boundAm != null ? boundAm : AwakeningManager.Instance;
        Sprite want = am != null ? am.CurrentSprite : null;

        if (want == null) want = baseSprite;   // 0단계이거나 아트 미지정 → 기본 모습
        if (want == null) return;              // 기본 모습조차 없으면 건드리지 않음
        if (targetRenderer.sprite == want) return;

        targetRenderer.sprite = want;
    }

    private void ResolveRenderer()
    {
        if (targetRenderer != null) return;

        targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (targetRenderer == null)
            Debug.LogWarning("[각성 외형] SpriteRenderer를 찾지 못했습니다. " +
                             "Target Renderer를 직접 연결해 주세요.", this);
    }

    // ══════════════════════════════════════════════
    //  연출
    // ══════════════════════════════════════════════

    private void PlayPunch()
    {
        if (!isActiveAndEnabled || punchTarget == null) return;

        if (punchRoutine != null) StopCoroutine(punchRoutine);
        punchRoutine = StartCoroutine(PunchRoutine());
    }

    private IEnumerator PunchRoutine()
    {
        float t = 0f;
        while (t < punchDuration)
        {
            // ★ unscaledDeltaTime을 쓰는 이유
            //   증강 카드창이 열려 있으면 Time.timeScale이 0입니다.
            //   deltaTime을 쓰면 연출이 멈춘 채 커진 상태로 굳어버립니다.
            t += Time.unscaledDeltaTime;

            float k = Mathf.Clamp01(t / punchDuration);

            // 0 → 1 → 0 으로 갔다 오는 곡선 (사인 반주기)
            float wave = Mathf.Sin(k * Mathf.PI);
            punchTarget.localScale = baseScale * Mathf.Lerp(1f, punchScale, wave);

            yield return null;
        }

        punchTarget.localScale = baseScale;
        punchRoutine = null;
    }

    // ══════════════════════════════════════════════
    //  에디터 테스트
    // ══════════════════════════════════════════════

    [ContextMenu("테스트: 지금 단계로 외형 갱신")]
    private void TestApply() => ApplySprite();

    [ContextMenu("테스트: 각성 연출 재생")]
    private void TestPunch()
    {
        if (!Application.isPlaying) { Debug.Log("[각성 외형] 플레이 중에만 동작합니다."); return; }
        PlayPunch();
    }
}
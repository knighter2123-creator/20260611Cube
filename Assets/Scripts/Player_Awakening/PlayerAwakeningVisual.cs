using System.Collections;
using UnityEngine;

/// <summary>각성 단계에 맞춰 플레이어 스프라이트 교체(전투 코드와 분리한 외형 컴포넌트) — 축하 연출은 변경 순간이 아닌 메인 스테이지 도착 후 Start 에서 예약을 꺼내 재생(클리어 직후 씬이 바뀌고, 상점 왕복 '복원'엔 예약이 없어 안 터짐).</summary>
// [붙이는 위치] 플레이어 오브젝트(또는 SpriteRenderer 가 있는 자식)
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

    // 구독한 인스턴스를 들고 비교 (bool 이면 매니저 재생성 시 새 이벤트를 영영 못 받음)
    private AwakeningManager boundAm;

    private Coroutine punchRoutine;
    private Vector3   baseScale;

    // ── 라이프사이클 ──

    void Awake()
    {
        ResolveRenderer();

        if (punchTarget == null) punchTarget = transform;

        // 원래 크기는 여기서 한 번만 기억 (연출 중에 기억하면 커진 크기를 원래로 착각)
        baseScale = punchTarget.localScale;

        // 시작 모습을 기본으로 기억 (세이브 삭제로 각성 초기화 시 되돌아갈 곳)
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
        // Start 에서 구독 재시도 (OnEnable 땐 매니저가 아직 없을 수 있음, 멱등)
        TrySubscribe();
        ApplySprite();

        // 각성하고 넘어왔으면 축하 연출 (꺼내면 예약이 지워져 한 번만)
        if (punchOnAwaken && boundAm != null && boundAm.ConsumeCelebration())
            PlayPunch();
    }

    void OnDisable()
    {
        Unsubscribe();

        if (punchRoutine != null) { StopCoroutine(punchRoutine); punchRoutine = null; }
        if (punchTarget != null) punchTarget.localScale = baseScale;
    }

    // ── 구독 ──

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
        // 구독한 boundAm 에서 해제 (Instance 와 다르면 구독이 남아 누수)
        if (boundAm != null) boundAm.OnChanged -= HandleAwakeningChanged;
        boundAm = null;
    }

    /// <summary>각성 상태 변경 시 스프라이트만 교체 (연출 없음 — 대개 곧 씬이 바뀌는 EvolveScene 시점).</summary>
    private void HandleAwakeningChanged() => ApplySprite();

    // ── 적용 ──

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

    // ── 연출 ──

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
            // unscaledDeltaTime — 증강 창(timeScale 0) 중에도 연출이 굳지 않게
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

    // ── 에디터 테스트 ──

    [ContextMenu("테스트: 지금 단계로 외형 갱신")]
    private void TestApply() => ApplySprite();

    [ContextMenu("테스트: 각성 연출 재생")]
    private void TestPunch()
    {
        if (!Application.isPlaying) { Debug.Log("[각성 외형] 플레이 중에만 동작합니다."); return; }
        PlayPunch();
    }
}
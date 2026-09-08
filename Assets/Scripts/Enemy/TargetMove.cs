using UnityEngine;

/// <summary>
/// 웨이포인트를 따라 이동하는 컴포넌트.
///
/// ★ 이번 수정: 스테이지별 속도 배율(spawnSpeedMult) 레이어 추가
///
/// ─── 핵심 설계 원칙: "속도를 직접 대입하지 않는다" (학습 포인트) ──────────
/// 초보자가 가장 많이 하는 실수:
///
///     void ApplySlow()  { speed = speed * 0.5f; }   // 느려짐
///     void ClearSlow()  { speed = speed * 2f;   }   // 원래대로?
///
/// 이러면 슬로우가 두 번 겹치거나, 도중에 다른 코드가 speed를 바꾸면
/// 값이 영원히 어긋납니다. 게다가 오브젝트 풀링으로 재사용하면
/// 어긋난 값이 다음 적한테 그대로 넘어가요.
///
/// 해결책은 "원본은 절대 안 건드리고, 배율만 곱해서 읽을 때 계산"하는 것:
///
///     실제속도 = 원본속도 × 스테이지배율 × 둔화배율   (스턴이면 0)
///
/// 각 배율은 서로를 모릅니다. 슬로우가 끝나도 slowMultiplier만 1로 돌아가고
/// 스테이지 배율(1-5의 1.5배)은 그대로 유지됩니다. 이게 핵심이에요.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
public class TargetMove : MonoBehaviour
{
    private Transform[] waypoints;

    [Header("이동 옵션")]
    public float speed           = 0.5f;   // 인스펙터 원본 속도 (프리팹마다 다르게 설정)
    public float arrivalDistance = 0.1f;

    private int   currentTargetIndex = 0;
    private float initialZ;
    private bool  isInitialized = false;

    // ── 속도 배율 레이어 (speed를 직접 건드리지 않고 곱으로 합성) ──
    private float baseSpeed;                 // 프리팹 원본값 (Awake에서 1회 보관)
    private float spawnSpeedMult  = 1f;      // ★ 신규: 스테이지 변형 (1-5 = 1.5배 등)
    private float slowMultiplier  = 1f;      // 1 = 정상, 0.7 = 30% 둔화
    private bool  isStunned       = false;

    /// <summary>
    /// 실제 이동에 쓰이는 속도. 스테이지 배율·둔화·스턴이 합성된 결과.
    ///
    /// ─── 이 문법은 무엇인가? (학습 포인트) ──────────────────────────
    /// `public float CurrentSpeed => ...` 는 "표현식 본문 프로퍼티"입니다.
    /// 아래와 완전히 같은 뜻이에요:
    ///
    ///     public float CurrentSpeed { get { return ...; } }
    ///
    /// 필드(변수)가 아니라 프로퍼티라서, 읽을 때마다 매번 새로 계산됩니다.
    /// 그래서 배율 중 하나가 바뀌면 다음 프레임부터 바로 반영돼요.
    /// ────────────────────────────────────────────────────────────
    /// </summary>
    public float CurrentSpeed => isStunned ? 0f : baseSpeed * spawnSpeedMult * slowMultiplier;

    void Awake()
    {
        baseSpeed = speed;   // ★ 풀 재사용 대비 원본 보관 (이후 speed는 읽지 않음)
    }

    // ── 스폰 시 스테이지 배율 API ──────────────────
    /// <summary>
    /// 스테이지별 속도 배율을 지정합니다.
    /// ★ 반드시 ResetForSpawn() 이후에 호출하세요.
    ///   (ResetForSpawn이 이 값을 1로 되돌리기 때문입니다)
    ///
    /// Mathf.Max(0.01f, mult) 로 하한을 두는 이유:
    /// 실수로 0이나 음수가 들어오면 적이 아예 안 움직이거나 뒤로 갑니다.
    /// 이런 "말도 안 되는 입력 막기"를 방어적 프로그래밍이라고 불러요.
    /// </summary>
    public void SetSpawnSpeedMultiplier(float mult) => spawnSpeedMult = Mathf.Max(0.01f, mult);

    // ── 디버프용 API ───────────────────────────────
    public void SetSlowMultiplier(float mult) => slowMultiplier = Mathf.Clamp01(mult);
    public void ClearSlow()                   => slowMultiplier = 1f;
    public void SetStunned(bool value)        => isStunned = value;

    /// <summary>
    /// 풀에서 꺼낼 때 호출 — 디버프와 경로 진행도를 전부 초기화.
    /// (Enemy.ResetDebuffs() 안에서 _move?.ResetForSpawn() 으로 불립니다)
    ///
    /// ★ spawnSpeedMult도 여기서 1로 되돌립니다.
    ///   1-5(빠른 적)에서 죽은 인스턴스가 풀에 들어갔다가
    ///   1-6에서 다시 나올 때 1.5배 속도를 그대로 들고 나오면 안 되니까요.
    ///   이게 오브젝트 풀링의 가장 흔한 버그 유형입니다:
    ///   "이전 생애의 상태가 남아있다."
    /// </summary>
    public void ResetForSpawn()
    {
        spawnSpeedMult     = 1f;   // ★ 신규
        slowMultiplier     = 1f;
        isStunned          = false;
        currentTargetIndex = 0;
        isInitialized      = false;
    }

    // 기존 호출부 호환용 (다른 스크립트가 쓰고 있다면 유지)
    public float GetSpeed()        => CurrentSpeed;

    /// <summary>
    /// ⚠️ 주의: 이 함수는 원본 속도 자체를 영구히 덮어씁니다.
    /// 풀링되는 적에게 쓰면 다음 재사용 때도 바뀐 값이 유지돼요.
    /// 일시적인 속도 변경은 SetSlowMultiplier / SetSpawnSpeedMultiplier를 쓰세요.
    /// </summary>
    public void SetSpeed(float v)  => baseSpeed = v;

    public void SetupPath(Transform[] paths)
    {
        waypoints          = paths;
        initialZ           = transform.position.z;
        currentTargetIndex = 0;
        isInitialized      = true;   // ★ 이게 true가 되어야 Update가 실제로 움직입니다
    }

    void Update()
    {
        if (!isInitialized || waypoints == null || waypoints.Length == 0) return;
        if (isStunned) return;   // 스턴 중엔 계산 자체를 건너뜀

        Transform target = waypoints[currentTargetIndex];
        if (target == null) return;

        Vector3 targetPosition = target.position;
        targetPosition.z = initialZ;   // Z축 렌더링 사라짐 방지

        transform.position = Vector3.MoveTowards(
            transform.position, targetPosition, CurrentSpeed * Time.deltaTime);

        // ─── sqrMagnitude 비교로 sqrt 제거 (학습 포인트) ────────────────
        // Vector3.Distance()는 내부에서 제곱근(√)을 계산합니다. 제곱근은
        // 곱셈보다 훨씬 비싼 연산이에요. "거리가 0.1보다 작은가?"는
        // "거리의 제곱이 0.01보다 작은가?"와 결과가 같으므로,
        // 양쪽을 제곱한 채로 비교하면 √를 아예 안 써도 됩니다.
        // 적이 수십 마리 × 매 프레임이면 이 차이가 쌓입니다.
        // ────────────────────────────────────────────────────────────
        float dx = transform.position.x - targetPosition.x;
        float dy = transform.position.y - targetPosition.y;
        if (dx * dx + dy * dy <= arrivalDistance * arrivalDistance)
            currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
    }
}
using UnityEngine;

/// <summary>
/// 웨이포인트를 따라 이동하는 컴포넌트.
///
/// ★ 이번 수정: 스폰 위치 흩뿌리기용 pathOffset 레이어 추가
///   (EnemyRespawn이 enemiesPerSpawn > 1일 때, 겹쳐서 태어나는 적들을
///    살짝 떨어뜨려 배치하기 위해 씀 — "일반적 생성주기 개편" 문서 §9 참고)
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
///
/// 이번에 추가한 pathOffset도 같은 원칙입니다 — 목표 좌표(waypoint) 자체는
/// 절대 건드리지 않고, "읽을 때 더해서" 계산합니다. 모든 웨이포인트에
/// 똑같이 더해지므로, 적이 가는 길 전체가 살짝 평행 이동한 것처럼 보입니다
/// (특정 지점에서만 어긋나거나 다시 겹치는 일이 없습니다).
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
    private float spawnSpeedMult  = 1f;      // 스테이지 변형 (1-5 = 1.5배 등)
    private float slowMultiplier  = 1f;      // 1 = 정상, 0.7 = 30% 둔화
    private bool  isStunned       = false;

    // ★ 신규: 경로 전체를 평행 이동시키는 오프셋 (스폰 위치 흩뿌리기용)
    //   x, y만 사용합니다. z는 initialZ가 따로 관리하므로 항상 무시됩니다.
    private Vector3 pathOffset = Vector3.zero;

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
        baseSpeed = speed;   // 풀 재사용 대비 원본 보관 (이후 speed는 읽지 않음)
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

    /// <summary>
    /// ★ 신규: 이 적이 따라갈 경로 전체를 살짝 평행 이동시킵니다.
    /// SetupPath()보다 먼저 불러도, 나중에 불러도 상관없습니다 — Update()가
    /// 매 프레임 다시 계산하므로 순서에 민감하지 않습니다 (SetSpawnSpeedMultiplier와의 차이).
    /// 다만 ResetForSpawn() "이후"에 불러야 합니다 (아래 ResetForSpawn 참고).
    ///
    /// z는 무시합니다 — Z축은 initialZ가 렌더링 순서 보정용으로 따로 관리합니다.
    /// </summary>
    public void SetPathOffset(Vector2 offset) => pathOffset = new Vector3(offset.x, offset.y, 0f);

    // ── 디버프용 API ───────────────────────────────
    public void SetSlowMultiplier(float mult) => slowMultiplier = Mathf.Clamp01(mult);
    public void ClearSlow()                   => slowMultiplier = 1f;
    public void SetStunned(bool value)        => isStunned = value;

    /// <summary>
    /// 풀에서 꺼낼 때 호출 — 디버프와 경로 진행도를 전부 초기화.
    /// (Enemy.ResetDebuffs() 안에서 _move?.ResetForSpawn() 으로 불립니다)
    ///
    /// ★ pathOffset도 여기서 반드시 0으로 되돌립니다.
    ///   이번에 흩뿌려서 태어난 적이 죽어서 풀에 들어갔다가, 다음번엔
    ///   enemiesPerSpawn이 1인 스테이지에서 다시 나올 때 예전 오프셋을
    ///   그대로 들고 나오면 안 되니까요.
    ///   이게 오브젝트 풀링의 가장 흔한 버그 유형입니다:
    ///   "이전 생애의 상태가 남아있다." (spawnSpeedMult와 완전히 같은 이유)
    /// </summary>
    public void ResetForSpawn()
    {
        spawnSpeedMult     = 1f;
        slowMultiplier     = 1f;
        pathOffset         = Vector3.zero;   // ★ 신규
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

        // ★ pathOffset을 더해서 "이 적만의" 목표 지점을 계산합니다.
        //   모든 웨이포인트에 똑같은 오프셋이 더해지므로, 경로 전체가
        //   그만큼 평행 이동한 것처럼 움직입니다 (특정 구간에서만 튀지 않음).
        Vector3 targetPosition = target.position + pathOffset;
        targetPosition.z = initialZ;   // Z축 렌더링 사라짐 방지 (pathOffset.z는 항상 0이라 순서 무관)

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
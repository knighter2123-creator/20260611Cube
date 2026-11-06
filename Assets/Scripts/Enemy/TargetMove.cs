using UnityEngine;

/// <summary>웨이포인트 이동 — 원본은 안 바꾸고 읽을 때 '원본 × 스테이지 × 둔화(스턴 0)'로 계산, pathOffset 도 읽을 때 더함 (직접 곱/나누면 겹침·풀 재사용 때 값이 어긋나서).</summary>
public class TargetMove : MonoBehaviour
{
    private Transform[] waypoints;

    [Header("이동 옵션")]
    public float speed           = 0.5f;   // 인스펙터 원본 속도 (프리팹마다 다르게 설정, Awake 이후 읽지 않음)
    public float arrivalDistance = 0.1f;

    private int   currentTargetIndex = 0;
    private float initialZ;
    private bool  isInitialized = false;

    // ── 속도 배율 레이어 (speed를 직접 건드리지 않고 곱으로 합성) ──
    private float baseSpeed;                 // 프리팹 원본값 (Awake에서 1회 보관)
    private float spawnSpeedMult  = 1f;      // 스테이지 변형 (1-5 = 1.5배 등)
    private float slowMultiplier  = 1f;      // 1 = 정상, 0.7 = 30% 둔화
    private bool  isStunned       = false;

    // 경로 전체를 평행 이동시키는 오프셋 (스폰 위치 흩뿌리기용). z는 항상 0.
    private Vector3 pathOffset = Vector3.zero;

    /// <summary>실제 이동에 쓰이는 속도. 스테이지 배율·둔화·스턴이 합성된 결과.</summary>
    public float CurrentSpeed => isStunned ? 0f : baseSpeed * spawnSpeedMult * slowMultiplier;

    void Awake()
    {
        baseSpeed = speed;   // 풀 재사용 대비 원본 보관
    }

    // ── 스폰 API (반드시 ResetForSpawn() 이후에 호출 — ResetForSpawn이 값을 되돌리므로) ──

    /// <summary>스테이지별 속도 배율. 0 이하 입력은 하한 0.01로 막습니다.</summary>
    public void SetSpawnSpeedMultiplier(float mult) => spawnSpeedMult = Mathf.Max(0.01f, mult);

    /// <summary>이 적이 따라갈 경로 전체를 평행 이동시킵니다. z는 무시합니다.</summary>
    public void SetPathOffset(Vector2 offset) => pathOffset = offset;

    public void SetupPath(Transform[] paths)
    {
        waypoints          = paths;
        initialZ           = transform.position.z;
        currentTargetIndex = 0;
        isInitialized      = true;   // 이게 true가 되어야 Update가 실제로 움직입니다
    }

    // ── 디버프용 API ───────────────────────────────
    public void SetSlowMultiplier(float mult) => slowMultiplier = Mathf.Clamp01(mult);
    public void ClearSlow()                   => slowMultiplier = 1f;
    public void SetStunned(bool value)        => isStunned = value;

    /// <summary>풀에서 꺼낼 때(Enemy.ResetDebuffs) 배율·오프셋·진행도 전부 초기화 — 빠뜨리면 이전 상태가 다음 적에게 넘어감.</summary>
    public void ResetForSpawn()
    {
        spawnSpeedMult     = 1f;
        slowMultiplier     = 1f;
        pathOffset         = Vector3.zero;
        isStunned          = false;
        currentTargetIndex = 0;
        isInitialized      = false;
    }

    void Update()
    {
        if (!isInitialized || waypoints == null || waypoints.Length == 0) return;
        if (isStunned) return;   // 스턴 중엔 계산 자체를 건너뜀

        Transform target = waypoints[currentTargetIndex];
        if (target == null) return;

        Vector3 targetPosition = target.position + pathOffset;
        targetPosition.z = initialZ;   // Z축 렌더링 사라짐 방지

        transform.position = Vector3.MoveTowards(
            transform.position, targetPosition, CurrentSpeed * Time.deltaTime);

        // 거리 비교는 제곱끼리 (수십 마리 × 매 프레임 제곱근 비용 회피)
        Vector2 delta = (Vector2)(transform.position - targetPosition);
        if (delta.sqrMagnitude <= arrivalDistance * arrivalDistance)
            currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
    }
}

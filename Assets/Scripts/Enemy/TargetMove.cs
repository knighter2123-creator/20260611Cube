using UnityEngine;

public class TargetMove : MonoBehaviour
{
    private Transform[] waypoints;

    [Header("이동 옵션")]
    public float speed           = 0.5f;   // 인스펙터 원본 속도
    public float arrivalDistance = 0.1f;

    private int   currentTargetIndex = 0;
    private float initialZ;
    private bool  isInitialized = false;

    // ── 디버프 상태 (speed를 직접 건드리지 않고 배율로 합성) ──
    private float baseSpeed;                 // 프리팹 원본값 (Awake에서 1회 보관)
    private float slowMultiplier = 1f;       // 1 = 정상, 0.7 = 30% 둔화
    private bool  isStunned      = false;

    /// <summary>실제 이동에 쓰이는 속도. 둔화·스턴이 합성된 결과.</summary>
    public float CurrentSpeed => isStunned ? 0f : baseSpeed * slowMultiplier;

    void Awake()
    {
        baseSpeed = speed;   // ★ 풀 재사용 대비 원본 보관
    }

    // ── 디버프용 API ───────────────────────────────
    public void SetSlowMultiplier(float mult) => slowMultiplier = Mathf.Clamp01(mult);
    public void ClearSlow()                   => slowMultiplier = 1f;
    public void SetStunned(bool value)        => isStunned = value;

    /// <summary>풀에서 꺼낼 때 호출 — 디버프와 경로 진행도를 전부 초기화</summary>
    public void ResetForSpawn()
    {
        slowMultiplier     = 1f;
        isStunned          = false;
        currentTargetIndex = 0;
        isInitialized      = false;
    }

    // 기존 호출부 호환용 (다른 스크립트가 쓰고 있다면 유지)
    public float GetSpeed()             => CurrentSpeed;
    public void  SetSpeed(float v)      => baseSpeed = v;

    public void SetupPath(Transform[] paths)
    {
        waypoints          = paths;
        initialZ           = transform.position.z;
        currentTargetIndex = 0;
        isInitialized      = true;
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

        // sqrMagnitude 비교로 sqrt 제거
        float dx = transform.position.x - targetPosition.x;
        float dy = transform.position.y - targetPosition.y;
        if (dx * dx + dy * dy <= arrivalDistance * arrivalDistance)
            currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
    }
}
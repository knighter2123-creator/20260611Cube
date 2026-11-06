using UnityEngine;

/// <summary>플레이어 탄환(풀링): LaunchVolley 로 타겟 방향 중심 부채꼴 다발 발사, 맞히거나 lifeTime 이 지나면 풀로 반환.</summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet 설정")]
    [SerializeField] private float speed    = 15f;
    [SerializeField] private float lifeTime = 3f;

    private float       damage;
    private bool        isCritical;
    private Vector2     direction;
    private float       timer;
    private Rigidbody2D rb;
    private bool        isReturned;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
    }

    void OnEnable()
    {
        timer      = 0f;
        isReturned = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnDisable()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    // ── 발사 ──

    /// <summary>타겟 방향 중심으로 count 발을 부채꼴 동시 발사.</summary>
    /// <param name="count">동시 발사체 수 (1 이상)</param>
    /// <param name="spreadAngle">발사체 사이의 각도 간격(도). count가 1이면 무시됩니다.</param>
    public static void LaunchVolley(Enemy target, Transform firePoint, PlayerStat stat,
                                    int count, float spreadAngle)
    {
        // 방어 가드
        if (target == null || target.isDead) return;
        if (stat == null) return;
        if (firePoint == null) return;
        if (ObjectPoolManager.Instance == null) return;

        count = Mathf.Max(1, count);

        // 공격력 = baseDamage × 증강 × 각성 버프, 곱셈은 전부 stat.FinalDamage 안에서 (스탯창과 같은 값) — ⚠ 여기서 버프를 다시 곱하면 이중 적용
        float buffedDamage = stat.FinalDamage;

        Vector2 spawnPos = (Vector2)firePoint.position;
        Vector2 toTarget = (Vector2)target.transform.position - spawnPos;

        // 정확히 겹치면 normalized 가 (0,0) 이라 총알이 제자리에 서므로 기본 방향
        Vector2 baseDir   = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
        float   baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + FanOffset(i, spreadAngle);
            SpawnOne(spawnPos, angle, buffedDamage, stat);
        }
    }

    /// <summary>i번째 발사체 각도 오프셋 [0, +s, -s, +2s…] — 0번은 항상 정조준 (대칭 분배면 짝수 발일 때 가운데가 비어 작은 적에게 전부 빗나감).</summary>
    private static float FanOffset(int index, float spreadAngle)
    {
        if (index == 0) return 0f;          // 0번은 언제나 정조준

        int   pair = (index + 1) / 2;       // 1,1,2,2,3,3...
        float sign = (index % 2 == 1) ? 1f : -1f;

        return spreadAngle * pair * sign;
    }

    /// <summary>총알 1발을 풀에서 꺼내 지정한 각도로 쏩니다.</summary>
    private static void SpawnOne(Vector2 spawnPos, float angleDeg, float buffedDamage, PlayerStat stat)
    {
        // 치명타는 발사체마다 따로 굴림(대미지 출렁임 감소), 확률은 스탯창과 같은 FinalCritical (증강 보너스 포함)
        bool  isCritical  = Random.Range(0f, 100f) < stat.FinalCritical;
        float finalDamage = isCritical
            ? buffedDamage * stat.FinalCriticalMultiplier
            : buffedDamage;

        GameObject bulletObj = ObjectPoolManager.Instance.GetBulletInactive();

        // 파괴된 인스턴스 방어 (Unity == null 이 걸러 줌, 없으면 TryGetComponent 에서 MissingReferenceException)
        if (bulletObj == null) return;

        if (!bulletObj.TryGetComponent(out Bullet bullet))
        {
            Debug.LogError("[Bullet] Bullet 컴포넌트를 찾을 수 없습니다.");
            ObjectPoolManager.Instance.ReturnBullet(bulletObj);
            return;
        }

        // 각도 → 방향 벡터 (부채꼴이라 타겟 좌표가 아닌 각도로 계산)
        float   rad = angleDeg * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        bulletObj.transform.SetPositionAndRotation(
            (Vector3)spawnPos, Quaternion.Euler(0f, 0f, angleDeg));

        bulletObj.SetActive(true);
        bullet.Init(dir, finalDamage, isCritical);
    }

    // ── 인스턴스 동작 ──

    public void Init(Vector2 dir, float dmg, bool crit = false)
    {
        direction         = dir.normalized;
        damage            = dmg;
        isCritical        = crit;
        timer             = 0f;
        isReturned        = false;
        rb.linearVelocity = direction * speed;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            ReturnToPool();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 한 발 = 한 적 — 겹친 적은 같은 물리 스텝에 연달아 불려 반환 뒤에도 맞는 관통이 생김
        if (isReturned) return;

        if (!other.TryGetComponent(out Enemy target)) return;
        if (target.isDead) return;

        target.TakeDamage(damage, isCritical);
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReturned) return;
        isReturned = true;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReturnBullet(gameObject);
        else
            gameObject.SetActive(false);
    }
}
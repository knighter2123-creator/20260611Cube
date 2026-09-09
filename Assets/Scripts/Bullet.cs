using UnityEngine;

/// <summary>
/// ★ 이번 수정은 Launch() 안의 두 줄뿐입니다. "★ 증강" 을 검색하세요.
///   stat.baseDamage          → stat.FinalDamage
///   stat.CriticalMultiplier  → stat.FinalCriticalMultiplier
///
/// 나머지 코드는 원본 그대로입니다.
/// </summary>
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

    void Awake() // 1
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
    }

    void OnEnable() // 2
    {
        timer      = 0f;
        isReturned = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnDisable() // 8
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// Player에서 호출. 크리티컬 판정 포함 발사 처리 전부 담당.
    /// </summary>
    public static void Launch(Enemy target, Transform firePoint, PlayerStat stat)
    {
        // 방어 가드 (누락됐던 부분 복원)
        if (target == null || target.isDead) return;
        if (stat == null) return;
        if (firePoint == null) return;
        if (ObjectPoolManager.Instance == null) return;

        // ★ 증강 ─────────────────────────────────────────────────────────
        //
        // 이제 공격력이 세 겹의 곱으로 결정됩니다.
        //
        //     baseDamage      강화로 올리는 순수 기본값 (세이브에 저장되는 유일한 값)
        //   × 증강 배율        AugmentManager.Attack — FinalDamage 프로퍼티가 대신 곱해줍니다
        //   × 버프 배율        PlayerBuffManager.DamageMultiplier — 기존 그대로
        //
        // 세 출처가 각각 독립된 '레이어'라서 서로를 덮어쓰지 않습니다.
        // 버프가 꺼져도 증강은 남고, 증강을 초기화해도 강화 수치는 그대로입니다.
        // (Enemy 의 방어력, TargetMove 의 이동속도와 똑같은 구조예요)
        //
        // 여기서 stat.baseDamage 대신 stat.FinalDamage 를 쓰는 게 핵심입니다.
        // baseDamage 에 증강 배율을 직접 곱해서 저장해 버리면,
        // 그 값이 세이브에 남아 다음 실행 때 또 곱해집니다 → 무한 인플레.
        // FinalDamage 는 프로퍼티라 값을 저장하지 않고 읽을 때마다 계산합니다.
        // ────────────────────────────────────────────────────────────────

        // 영구 버프 + 증강이 적용된 기본 공격력
        float buffMult     = PlayerBuffManager.Instance?.DamageMultiplier ?? 1f;
        float buffedDamage = stat.FinalDamage * buffMult;      // ★ baseDamage → FinalDamage

        // 크리티컬 판정
        bool  isCritical  = Random.Range(0f, 100f) < stat.Critical;
        float finalDamage = isCritical
            ? buffedDamage * stat.FinalCriticalMultiplier      // ★ CriticalMultiplier → FinalCriticalMultiplier
            : buffedDamage;

        Vector2 spawnPos = (Vector2)firePoint.position;
        Vector2 dir = ((Vector2)target.transform.position - spawnPos).normalized;
        float   angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        GameObject bulletObj = ObjectPoolManager.Instance.GetBulletInactive();

        if (!bulletObj.TryGetComponent(out Bullet bullet))
        {
            Debug.LogError("[Bullet] Bullet 컴포넌트를 찾을 수 없습니다.");
            ObjectPoolManager.Instance.ReturnBullet(bulletObj);
            return;
        }

        bulletObj.transform.SetPositionAndRotation(
            (Vector3)spawnPos, Quaternion.Euler(0f, 0f, angle));

        bulletObj.SetActive(true);
        bullet.Init(dir, finalDamage, isCritical);
    }

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
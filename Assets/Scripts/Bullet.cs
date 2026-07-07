using UnityEngine;

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
        
        // 영구 버프 적용된 기본 공격력
        float buffMult     = PlayerBuffManager.Instance?.DamageMultiplier ?? 1f;
        float buffedDamage = stat.baseDamage * buffMult;

        // 크리티컬 판정
        bool  isCritical  = Random.Range(0f, 100f) < stat.Critical;
        float finalDamage = isCritical
            ? buffedDamage * stat.CriticalMultiplier
            : buffedDamage;

        Vector2 spawnPos = (Vector2)firePoint.position;
        Vector2 dir = ((Vector2)target.transform.position - spawnPos).normalized;
        float   angle    = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        GameObject bulletObj = ObjectPoolManager.Instance.GetBulletInactive();
        bulletObj.transform.position = (Vector3)spawnPos;
        bulletObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogError("[Bullet] Bullet 컴포넌트를 찾을 수 없습니다.");
            ObjectPoolManager.Instance.ReturnBullet(bulletObj);
            return;
        }

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
        if (!other.CompareTag("Enemy")) return;
        if (isReturned) return;

        Enemy target = other.GetComponent<Enemy>();
        if (target == null || target.isDead) return;

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
using UnityEngine;

public class Player : MonoBehaviour
{
    private static Player _instance;

    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private LayerMask enemyLayer;

    private Transform firePoint;
    private Enemy currentTarget;
    private float attackTimer;
    private bool  isDead = false;
    private bool  isFirstLoad;

    public PlayerStat stat => playerStat;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    void Start()
    {
        if (isFirstLoad)
        {
            playerStat.InitFull();
            isFirstLoad = false;
        }

        attackTimer = 0f;

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.Init(playerStat);
    }

    void Update()
    {
        FindTarget();
        HandleAttack();
    }

    void FindTarget()
    {
        currentTarget = null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        float closestDist  = Mathf.Infinity;
        Enemy closestEnemy = null;

        foreach (GameObject enemyObj in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);

            // ✅ detectRange → stat.attackRange 로 교체
            if (dist > stat.attackRange) continue;

            Enemy e = enemyObj.GetComponent<Enemy>();
            if (e == null || e.isDead) continue;

            if (dist < closestDist)
            {
                closestDist  = dist;
                closestEnemy = e;
            }
        }

        currentTarget = closestEnemy;
    }

    void HandleAttack()
    {
        if (isDead) return;
        if (currentTarget == null) return;

        attackTimer += Time.deltaTime;

        // ✅ AttackSpd를 쿨다운(초)으로 변환 — 최소 0.1초 보장
        float cooldown = Mathf.Max(stat.attackCooldown, 0.1f);

        if (attackTimer >= cooldown)
        {
            attackTimer = 0f;
            FireBullet();
        }
    }

    void FireBullet()
    {
        if (currentTarget == null || isDead) return;
        if (ObjectPoolManager.Instance == null)
        {
            Debug.LogWarning("[Player] ObjectPoolManager가 씬에 없습니다.");
            return;
        }

        Vector2 spawnPos = firePoint != null ? firePoint.position : (Vector2)transform.position;
        Vector2 dir      = ((Vector2)currentTarget.transform.position - spawnPos).normalized;

        GameObject bulletObj = ObjectPoolManager.Instance.GetBulletInactive();
        bulletObj.transform.position = (Vector3)spawnPos;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bulletObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogError("[Player] Bullet 컴포넌트를 찾을 수 없습니다.");
            ObjectPoolManager.Instance.ReturnBullet(bulletObj);
            return;
        }

        bulletObj.SetActive(true);
        bullet.Init(dir, playerStat.baseDamage);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        // ❌ Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.DrawWireSphere(transform.position, stat != null ? stat.attackRange : 10f); // ✅
    }
}
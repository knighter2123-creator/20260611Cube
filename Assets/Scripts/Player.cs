using UnityEngine;

public class Player : MonoBehaviour
{
    private static Player _instance;

    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private LayerMask  enemyLayer;

    [Header("적 탐지 범위")]
    [SerializeField] private float detectRange = 10f;

    [Header("Bullet 발사 위치 (미설정 시 Player 중심)")]
    [SerializeField] private Transform firePoint;

    private Enemy currentTarget;
    private float attackTimer;
    private bool  isDead = false;
    private bool  isFirstLoad;

    public PlayerStat stat => playerStat;

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────
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

        // attackTimer를 쿨다운 직전으로 초기화하면 게임 시작 즉시 한 발 발사됨
        // 0으로 두면 첫 발사까지 attackCooldown만큼 대기
        attackTimer = 0f;

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.Init(playerStat);
    }

    void Update()
    {
        FindTarget();
        HandleAttack();
    }

    // ──────────────────────────────────────────────
    //  적 탐지 — "Enemy" 태그 중 가장 가까운 대상
    // ──────────────────────────────────────────────
    void FindTarget()
    {
        // 현재 타겟이 살아있으면 유지
        if (currentTarget != null && !currentTarget.isDead)
            return;

        currentTarget = null;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        float closestDist  = Mathf.Infinity;
        Enemy closestEnemy = null;

        foreach (GameObject enemyObj in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
            if (dist > detectRange) continue;

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

    // ──────────────────────────────────────────────
    //  공격 쿨다운 처리
    // ──────────────────────────────────────────────
    void HandleAttack()
    {
        if (isDead) return;

        // 타겟이 없으면 타이머만 누적하지 않음
        if (currentTarget == null) return;

        attackTimer += Time.deltaTime;

        // attackCooldown이 0 이하면 1초로 강제 보정 (무한 발사 방지)
        float cooldown = Mathf.Max(stat.attackCooldown, 1f);

        if (attackTimer >= cooldown)
        {
            attackTimer = 0f;
            FireBullet();
        }
    }

    // ──────────────────────────────────────────────
    //  Bullet 발사 (ObjectPool 사용)
    // ──────────────────────────────────────────────
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

        // ① 비활성 상태로 꺼냄
        GameObject bulletObj = ObjectPoolManager.Instance.GetBulletInactive();

        // ② 위치 확정 (SetActive 이전)
        bulletObj.transform.position = (Vector3)spawnPos;

        // 2D에서 Bullet이 진행 방향을 바라보도록 Z축 회전
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bulletObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // ③ 방향·데미지 주입
        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet == null)
        {
            Debug.LogError("[Player] Bullet 컴포넌트를 찾을 수 없습니다.");
            ObjectPoolManager.Instance.ReturnBullet(bulletObj);
            return;
        }

        // ④ 활성화
        bulletObj.SetActive(true);

        // ⑤ Init은 SetActive 이후 → Rigidbody가 활성 상태에서 velocity 적용
        bullet.Init(dir, playerStat.baseDamage);
    }

    // ──────────────────────────────────────────────
    //  디버그용 탐지 범위 시각화
    // ──────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
    }
}
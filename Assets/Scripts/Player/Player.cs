using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private Transform  firePoint;
    [SerializeField] private LayerMask  enemyLayer;

    private Enemy currentTarget;
    private float attackTimer;
    private bool  isDead      = false;
    private bool  isFirstLoad;

    public PlayerStat stat => playerStat;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

        GameObject[] enemies      = GameObject.FindGameObjectsWithTag("Enemy");
        float        closestDist  = Mathf.Infinity;
        Enemy        closestEnemy = null;

        foreach (GameObject enemyObj in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
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

        float cooldown = Mathf.Max(stat.attackCooldown, 0.1f);
        if (attackTimer >= cooldown)
        {
            attackTimer = 0f;
            Bullet.Launch(currentTarget, firePoint != null ? firePoint : transform, playerStat);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stat != null ? stat.attackRange : 10f);
    }
}
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private Transform  firePoint;
    
    [Header("타겟 갱신")]
    [SerializeField] private float retargetInterval = 0.1f;   // 재탐색 주기(초)

    private float retargetTimer;

    private Enemy currentTarget;
    private float attackTimer;
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
        bool hasSave = SaveManager.Instance != null && SaveManager.Instance.HasSave();
        if (!hasSave)
            playerStat.InitFull();   // ← 이 줄이 실제로 있는지 확인

        attackTimer = 0f;

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.Init(playerStat);
    }

    void Update()
    {
        retargetTimer += Time.deltaTime;

        // 타겟이 사라졌거나 주기가 됐을 때만 재탐색
        if (currentTarget == null || currentTarget.isDead || retargetTimer >= retargetInterval)
        {
            retargetTimer = 0f;
            FindTarget();
        }

        HandleAttack();
    }


    void FindTarget()
    {
        Vector2 myPos      = transform.position;
        float   closestSqr = stat.attackRange * stat.attackRange;  // 사거리 검사를 비교에 흡수
        Enemy   closest    = null;

        List<Enemy> list = Enemy.Active;
        for (int i = 0; i < list.Count; i++)   // foreach 대신 for → 열거자 생성 없음
        {
            Enemy e = list[i];
            if (e == null || e.isDead) continue;

            float sqr = ((Vector2)e.transform.position - myPos).sqrMagnitude;
            if (sqr <= closestSqr)             // sqrt 생략
            {
                closestSqr = sqr;
                closest    = e;
            }
        }

        currentTarget = closest;
    }

    void HandleAttack()
    {
        float cooldown = Mathf.Max(stat.attackCooldown, 0.1f);

        if (attackTimer < cooldown)
        {
            attackTimer += Time.deltaTime;
            return;
        }

        // 쿨타임은 찼지만 타겟이 없음 → 타이머를 리셋하지 않고 유지(=발사 대기 상태)
        if (currentTarget == null || currentTarget.isDead) return;

        attackTimer = 0f;
        Bullet.Launch(currentTarget, firePoint, playerStat);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stat != null ? stat.attackRange : 10f);
    }
}
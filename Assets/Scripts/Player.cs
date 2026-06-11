using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    private static Player _instance;
    
    [Header("Player 스탯")]
    [SerializeField] private PlayerStat playerStat;

    [Header("세부 설정")]
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private LayerMask enemyLayer;

    private Enemy currentTarget;
    private float attackTimer;

    private bool isDead      = false;
    private bool isAttack    = false;
    private bool isFirstLoad;

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
        _instance      = this;
        
    }

    void Start()
    {
        if (isFirstLoad)
        {
            // 게임 최초 시작 시에만 모든 수치 초기화
            playerStat.InitFull();
            isFirstLoad = false;
        }

        if (LevelUpManager.Instance != null)
            LevelUpManager.Instance.Init(playerStat);
    }

    void Update()
    {
        FindTarget();
        HandleAttack();
    }

    // ──────────────────────────────────────────────
    //  전투
    // ──────────────────────────────────────────────
    void FindTarget() // 수정필요
    {
        if (currentTarget != null && !currentTarget.isDead)
            return;

        currentTarget = null;
    }

    void HandleAttack()
    {
        if (currentTarget == null || isDead) return;

        attackTimer += Time.deltaTime;
        if (attackTimer >= stat.attackCooldown)
        {
            attackTimer = 0f;
            PlayerAttack();
        }
    }

    void PlayerAttack()
    {
        if (currentTarget == null || isDead) return;

        isAttack = true;
        
        currentTarget.TakeDamage(playerStat.baseDamage);
    }
}
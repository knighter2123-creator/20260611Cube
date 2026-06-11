using UnityEngine;

public class Enemy : MonoBehaviour, IEnemyDead
{
    [Header("Enemy 스탯")]
    [SerializeField] private float maxHealth = 100f;

    private float currentHealth;
    public bool isDead { get; set; }

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────
    void Awake()
    {
        currentHealth = maxHealth;
        isDead        = false;
    }

    void Start()
    {
        
    }

    void Update()
    {
       
    }

    void FixedUpdate()
    {
       
    }

    // ──────────────────────────────────────────────
    //  전투
    // ──────────────────────────────────────────────
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"[Enemy] {gameObject.name} HP: {currentHealth}/{maxHealth}  (-{damage})");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }


    void Die()
    {
        if (isDead) return;   // 중복 호출 방지

        isDead = true;
        Debug.Log($"[Enemy] {gameObject.name} 사망");
        Destroy(gameObject);
    }
}
using UnityEngine;

public class Enemy : MonoBehaviour, ITakeDamage
{
    [Header("Enemy 스탯")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] private int rewardGold = 10;
    [SerializeField] private int rewardExp = 5;
    
    protected float currentHealth;
    
    public bool isDead { get; set; }

    void Awake()
    {
        InitStats();
    }

// virtual로 분리 — 자식에서 override 가능
    protected virtual void InitStats()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0f)
            Die();
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        CurrencyManager.Instance?.AddGold(rewardGold);
        LevelUpManager.Instance?.AddExp(rewardExp);
        StageCount.Instance?.ReportEnemyKill();

        Destroy(gameObject);
    }
}
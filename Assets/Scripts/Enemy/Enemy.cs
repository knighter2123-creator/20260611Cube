using System.Collections;
using UnityEngine;

public partial class Enemy : MonoBehaviour, ITakeDamage
{
    [Header("Enemy 스탯")]
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] private int rewardGold = 10;
    [SerializeField] private int rewardExp  = 5;

    protected float currentHealth;
    public bool isDead { get; set; }

    private GameObject hpBarObject;
    private EnemyHpBar hpBarController;

    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject    = hpBar;
        hpBarController = hpBar.GetComponentInChildren<EnemyHpBar>();
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    void Awake()
    {
        InitStats();
    }

    protected virtual void InitStats()
    {
        currentHealth = maxHealth;
        isDead        = false;
    }

    public virtual void ApplyStatMultiplier(float mult)
    {
        maxHealth     *= mult;
        currentHealth  = maxHealth;
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        float finalDamage = damage / _armorBreakMultiplier;   // 방어력 감소 반영
        currentHealth -= finalDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);
        hpBarController?.UpdateHp(currentHealth, maxHealth);

        if (currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        StopAllDebuffs();
        RemoveHpBar();
        CurrencyManager.Instance?.AddGold(rewardGold);
        LevelUpManager.Instance?.AddExp(rewardExp);
        StageManager.Instance?.ReportEnemyKill();

        Destroy(gameObject);
    }

    protected void RemoveHpBar()
    {
        if (hpBarObject != null)
            Destroy(hpBarObject);
    }
}
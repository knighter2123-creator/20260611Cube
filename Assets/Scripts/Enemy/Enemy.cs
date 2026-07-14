using System.Collections;
using UnityEngine;

public partial class Enemy : MonoBehaviour, ITakeDamage
{
    [Header("Enemy 스탯")]
    [SerializeField] protected float currentHealth;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] protected float defence   = 5f;
    [SerializeField] private int rewardGold = 10;
    [SerializeField] private int rewardExp  = 5;
    
    protected float statMult = 1f;   // 스폰 시 받은 누적 배율
    
    public bool isDead { get; set; }

    private GameObject hpBarObject;
    private EnemyHpBar hpBarController;
    private StageManager stageManager;

    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject     = hpBar;
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
        statMult      = mult;
        maxHealth     *= mult;
        currentHealth  = maxHealth;
        hpBarController?.UpdateHp(currentHealth, maxHealth);
    }

    // 일반 데미지 (스킬용 — 크리티컬 없음)
    public virtual void TakeDamage(float damage)
    {
        TakeDamage(damage, isCritical: false);
    }

    // 크리티컬 여부를 받는 메인 메서드
    public virtual void TakeDamage(float damage, bool isCritical)
    {
        if (isDead) return;

        float reducedDefence = defence / _armorBreakMultiplier;
        float finalDamage    = Mathf.Max(damage - reducedDefence, 0f);

        currentHealth -= finalDamage;
        currentHealth  = Mathf.Max(currentHealth, 0f);
        hpBarController?.UpdateHp(currentHealth, maxHealth);

        DamageTextPool.Instance?.ShowDamage(
            transform.position,
            Mathf.RoundToInt(finalDamage),
            isCritical
        );

        if (currentHealth <= 0)
            Die();
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        StopAllDebuffs();
        RemoveHpBar();
        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(rewardGold * statMult));
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(rewardExp * statMult));
        StageManager.Instance?.ReportEnemyKill();
        MissionManager.Instance?.ReportEnemyKill();
        GuideQuestManager.Instance?.ReportEnemyKill();

        Destroy(gameObject);
    }

    public void RemoveHpBar()
    {
        if (hpBarObject != null)
            Destroy(hpBarObject);
    }
}
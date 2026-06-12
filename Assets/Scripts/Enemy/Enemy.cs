using UnityEngine;
public class Enemy : MonoBehaviour, ITakeDamage
{
    [Header("Enemy 스탯")]
    [SerializeField] protected float currentHealth;
    [SerializeField] protected float maxHealth = 100f;
    [SerializeField] private int rewardGold = 10;
    [SerializeField] private int rewardExp = 5;

    public bool isDead { get; set; }

    // HP바 참조 추가
    private GameObject hpBarObject;
    private EnemyHpBar hpBarController; // HP바 슬라이더 제어용

    public void SetHpBar(GameObject hpBar)
    {
        hpBarObject = hpBar;
        hpBarController = hpBar.GetComponentInChildren<EnemyHpBar>();
    }

    void Awake() { InitStats(); }

    protected virtual void InitStats()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        currentHealth -= damage;

        // HP바 슬라이더 갱신
        hpBarController?.UpdateHp(currentHealth, maxHealth);

        if (currentHealth <= 0f) Die();
    }

    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;

        // HP바는 즉시 제거
        if (hpBarObject != null)
            Destroy(hpBarObject);

        CurrencyManager.Instance?.AddGold(rewardGold);
        LevelUpManager.Instance?.AddExp(rewardExp);
        StageManager.Instance?.ReportEnemyKill();

        Destroy(gameObject);
    }

// 자식에서 공통으로 쓸 수 있도록 분리
    protected void RemoveHpBar()
    {
        if (hpBarObject != null)
            Destroy(hpBarObject);
    }
}
using UnityEngine;

public class BossMonster : Enemy
{
    [Header("보스 전용 배율")]
    [SerializeField] private float bossHpMultiplier       = 1.5f;
    [SerializeField] private float bossExpMultiplier      = 2.0f;
    [SerializeField] private int   baseRewardExp          = 10;
    [SerializeField] private float bossCurrencyMultiplier = 1.5f;

    [Header("보석 보상")]
    [SerializeField] private int baseRewardGem = 10;       // ✅ 기본 보석 보상량

    protected override void InitStats()
    {
        base.InitStats();
        maxHealth     *= bossHpMultiplier;
        currentHealth  = maxHealth;
    }

    public override void ApplyStatMultiplier(float mult)
    {
        maxHealth     *= mult;
        currentHealth  = maxHealth;
    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        RemoveHpBar();
        StopAllCoroutines();

        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(100 * bossCurrencyMultiplier));
        CurrencyManager.Instance?.AddGem(baseRewardGem); // ✅ 보석 지급
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(baseRewardExp * bossExpMultiplier));
        StageManager.Instance?.ReportBossKill();

        Destroy(gameObject);
    }
}
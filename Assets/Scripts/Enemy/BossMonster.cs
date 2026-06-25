using UnityEngine;

public class BossMonster : Enemy
{
    [Header("보스 전용 배율")]
    [SerializeField] private float bossHpMultiplier       = 1.5f;
    [SerializeField] private float bossDefenceMultiplier  = 1.5f;
    [SerializeField] private float bossExpMultiplier      = 2.0f;
    [SerializeField] private int   baseRewardExp          = 10;
    [SerializeField] private float bossCurrencyMultiplier = 1.5f;

    [Header("보석 보상")]
    [SerializeField] private int baseRewardGem = 100;

    protected float statMult = 1f;   // 스폰 시 받은 누적 배율
    
    protected override void InitStats()
    {
        base.InitStats();
        maxHealth     *= bossHpMultiplier;
        currentHealth  = maxHealth;
        defence       *= bossDefenceMultiplier;
    }

    public override void ApplyStatMultiplier(float mult)
    {
        statMult      = mult;
        maxHealth     *= mult;
        currentHealth  = maxHealth;
        defence       *= mult;
    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        RemoveHpBar();
        StopAllCoroutines();

        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(100 * bossCurrencyMultiplier));
        CurrencyManager.Instance?.AddGem(baseRewardGem);
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(baseRewardExp * (bossExpMultiplier * statMult)));
        StageManager.Instance?.ReportBossKill();

        Destroy(gameObject);
    }
}
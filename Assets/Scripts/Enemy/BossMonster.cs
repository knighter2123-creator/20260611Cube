using System.Collections;
using UnityEngine;

public class BossMonster : Enemy
{
    [Header("보스 전용 배율")]
    [SerializeField] private float bossHpMultiplier       = 1.5f;
    [SerializeField] private float bossExpMultiplier      = 2.0f;
    [SerializeField] private int   baseRewardExp          = 10;
    [SerializeField] private float bossCurrencyMultiplier = 1.5f;
    
    protected override void InitStats()
    {
        base.InitStats();                        // Enemy 기본 초기화 먼저
        maxHealth    *= bossHpMultiplier;        // 배율 적용
        currentHealth = maxHealth;               // 변경된 maxHealth로 재설정
    }
    
    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        RemoveHpBar(); // HP바만 부모에서 가져와서 즉시 제거

        StopAllCoroutines();

        CurrencyManager.Instance?.AddGold(Mathf.RoundToInt(100 * bossCurrencyMultiplier));
        LevelUpManager.Instance?.AddExp(Mathf.RoundToInt(baseRewardExp * bossExpMultiplier));
        StageManager.Instance?.ReportBossKill();

        Destroy(gameObject, 1.5f);
    }
}

    
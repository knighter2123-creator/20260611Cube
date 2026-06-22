using UnityEngine;

public class EvolveBoss : Enemy
{
    [Header("보스 전용 배율")]
    [SerializeField] private float bossHpMultiplier      = 7f;
    [SerializeField] private float bossDefenceMultiplier = 7f;

    [Header("클리어 보상 (영구 버프)")]
    [SerializeField] private float damageBuffPercent = 0.3f; // +30%

    private bool health50Trigger = false; // 50% 미만
    private bool health20Trigger = false; // 20% 미만

    protected override void InitStats()
    {
        base.InitStats();
        maxHealth    *= bossHpMultiplier;
        currentHealth = maxHealth;
        defence      *= bossDefenceMultiplier;
    }

    public override void ApplyStatMultiplier(float mult)
    {
        maxHealth    *= mult;
        currentHealth = maxHealth;
        defence      *= mult;
    }

    // 메인 데미지 경로 하나만 후킹 → 스킬·크리티컬 둘 다 커버
    public override void TakeDamage(float damage, bool isCritical)
    {
        base.TakeDamage(damage, isCritical); // 방어력·HP바·데미지텍스트·사망처리는 base가 담당
        if (isDead) return;                  // base에서 죽었으면 페이즈 체크 불필요
        HealthPhase();
    }

    private void HealthPhase()
    {
        float healthPer = (currentHealth / maxHealth) * 100f;

        // else if가 아니라 독립 if → 한 방에 50%·20%를 같이 통과해도 둘 다 발동
        if (healthPer < 50f && !health50Trigger)
        {
            health50Trigger = true;
            HealthPhase2();
        }
        if (healthPer < 20f && !health20Trigger)
        {
            health20Trigger = true;
            HealthPhase3();
        }
    }

    private void HealthPhase2() => defence += 5f;
    private void HealthPhase3() => defence += 7f;

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        RemoveHpBar();
        StopAllCoroutines();

        // Gold/Gem/Exp 미지급 — 대신 플레이어에게 영구 데미지 버프
        PlayerBuffManager.Instance?.AddPermanentDamageBuff(damageBuffPercent);

        StageManager.Instance?.ReportBossKill();

        Destroy(gameObject);
    }
}
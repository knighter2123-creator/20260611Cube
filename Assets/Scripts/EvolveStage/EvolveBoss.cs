using UnityEngine;

/// <summary>
/// 진화 스테이지 전용 보스.
///   - 스탯 배율 / 보상%는 입장한 티어(EvolveStageData)에서 읽음 (없으면 SerializeField fallback)
///   - 전리품(Gold/Gem/Exp) 없음 → 대신 플레이어 베이스 대미지 영구 버프
///   - 보상은 티어당 1회만 지급 (무한 파밍 방지)
///   - 처치 보고는 StageManager가 아니라 EvolveStageManager로
/// </summary>
public class EvolveBoss : Enemy
{
    [Header("배율 fallback (티어 데이터가 없을 때만 사용)")]
    [SerializeField] private float bossHpMultiplier      = 15f;
    [SerializeField] private float bossDefenceMultiplier = 4f;

    [Header("보상 fallback (티어 데이터가 없을 때만 사용)")]
    [SerializeField] private float damageBuffPercent = 0.3f; // +30%

    private bool health50Trigger = false; // 50% 미만
    private bool health20Trigger = false; // 20% 미만

    // 입장 시 EvolveStageContext에 설정됨. 단독 실행 시 null → fallback 사용.
    private EvolveStageData Data => EvolveStageContext.SelectedData;

    protected override void InitStats()
    {
        base.InitStats();

        float hpMult  = Data != null ? Data.bossHpMultiplier      : bossHpMultiplier;
        float defMult = Data != null ? Data.bossDefenceMultiplier : bossDefenceMultiplier;

        maxHealth    *= hpMult;
        currentHealth = maxHealth;
        defence      *= defMult;
    }

    public override void ApplyStatMultiplier(float mult)
    {
        // 진화 스테이지는 일반 스테이지 배율을 받지 않지만, 호출돼도 안전하게 동작
        maxHealth    *= mult;
        currentHealth = maxHealth;
        defence      *= mult;
    }

    // 메인 데미지 경로 하나만 후킹 → 스킬·크리티컬 둘 다 커버
    public override void TakeDamage(float damage, bool isCritical)
    {
        base.TakeDamage(damage, isCritical); // 방어력·HP바·데미지텍스트·사망처리는 base 담당
        if (isDead) return;                  // base에서 죽었으면 페이즈 체크 불필요
        HealthPhase();
    }

    private void HealthPhase()
    {
        float healthPer = (currentHealth / maxHealth) * 100f;

        // 독립 if → 한 방에 50%·20%를 같이 통과해도 둘 다 발동
        if (healthPer < 50f && !health50Trigger)
        {
            health50Trigger = true;
            defence += 5f;
        }
        if (healthPer < 20f && !health20Trigger)
        {
            health20Trigger = true;
            defence += 7f;
        }
    }

    protected override void Die()
    {
        if (isDead) return;
        isDead = true;

        RemoveHpBar();
        StopAllCoroutines();

        GrantReward();

        // 진화 스테이지 매니저에 보고 (StageManager 아님!)
        EvolveStageManager.Instance?.ReportBossKill();

        Destroy(gameObject);
    }

    /// <summary>전리품 없음 — 영구 베이스 대미지 버프를 티어당 1회만 지급.</summary>
    private void GrantReward()
    {
        float buff = Data != null ? Data.damageBuffPercent : damageBuffPercent;
        string key = "EvolveRewardClaimed_" + (Data != null ? Data.id : name);

        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            Debug.Log($"[EvolveBoss] 이미 보상 지급된 티어({key}) — 버프 미지급");
            return;
        }

        PlayerBuffManager.Instance?.AddPermanentDamageBuff(buff);
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
        Debug.Log($"[EvolveBoss] 영구 대미지 버프 +{buff * 100f}% 지급 ({key})");
    }
}
using UnityEngine;

/// <summary>
/// 진화 스테이지 전용 보스.
///   - 스탯 배율 / 보상%는 입장한 티어(EvolveStageData)에서 읽음 (없으면 SerializeField fallback)
///   - 전리품(Gold/Gem/Exp) 없음 → 대신 플레이어 베이스 대미지 영구 버프
///   - 보상은 티어당 1회만 지급 (SaveData에 기록 — DeleteSave 시 함께 초기화)
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
        maxHealth    *= mult;
        currentHealth = maxHealth;
        defence      *= mult;
    }

    public override void TakeDamage(float damage, bool isCritical)
    {
        base.TakeDamage(damage, isCritical);
        if (isDead) return;
        HealthPhase();
    }

    private void HealthPhase()
    {
        float healthPer = (currentHealth / maxHealth) * 100f;

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

        EvolveStageManager.Instance?.ReportBossKill();

        Destroy(gameObject);
    }

    /// <summary>전리품 없음 — 영구 베이스 대미지 버프를 티어당 1회만 지급 (SaveData 플래그).</summary>
    private void GrantReward()
    {
        float buff = Data != null ? Data.damageBuffPercent : damageBuffPercent;
        string id  = Data != null ? Data.id : name;

        if (SaveManager.Instance != null && SaveManager.Instance.IsEvolveRewardClaimed(id))
        {
            Debug.Log($"[EvolveBoss] 이미 보상 지급된 티어({id}) — 버프 미지급");
            return;
        }

        PlayerBuffManager.Instance?.AddPermanentDamageBuff(buff);   // 내부에서 Save() 호출
        SaveManager.Instance?.MarkEvolveRewardClaimed(id);          // 플래그 기록 + Save()

        Debug.Log($"[EvolveBoss] 영구 대미지 버프 +{buff * 100f}% 지급 ({id})");
    }
}
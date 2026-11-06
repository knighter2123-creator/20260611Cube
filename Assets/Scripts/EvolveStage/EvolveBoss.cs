using UnityEngine;

/// <summary>진화 스테이지 보스 — 배율·보상%는 입장 티어 데이터에서, 전리품 대신 기본 대미지 영구 버프(티어당 1회, 세이브 기록), 처치는 EvolveStageManager 로 보고 (풀 출신이 아니라 부모 Die 의 Return 이 알아서 Destroy).</summary>
public class EvolveBoss : Enemy
{
    [Header("배율 fallback (티어 데이터가 없을 때만 사용)")]
    [SerializeField] private float bossHpMultiplier      = 15f;
    [SerializeField] private float bossDefenceMultiplier = 4f;

    [Header("보상 fallback (티어 데이터가 없을 때만 사용)")]
    [SerializeField] private float damageBuffPercent = 0.3f; // +30%

    private bool health50Trigger = false; // 50% 미만
    private bool health20Trigger = false; // 20% 미만

    /// <summary>입장 티어 데이터(없으면 null) — 접근마다 Context 를 조회해 항상 최신.</summary>
    private EvolveStageData Data => EvolveStageContext.SelectedData;

    /// <summary>SpawnBoss 가 Instantiate 직후 호출해 티어 배율 적용 — Awake 시점엔 티어 데이터 보장이 없어 외부에서 명시적으로 (base 값은 Awake 에서 이미 채워짐).</summary>
    public void InitForEvolveStage()
    {
        float hpMult  = Data != null ? Data.bossHpMultiplier      : bossHpMultiplier;
        float defMult = Data != null ? Data.bossDefenceMultiplier : bossDefenceMultiplier;

        statMult      = 1f;                          // 진화 보스는 스테이지 배율을 쓰지 않음
        maxHealth     = baseMaxHealth * hpMult;      // ★ 누적(*=)이 아니라 대입(=)
        defence       = baseDefence   * defMult;
        currentHealth = maxHealth;

        health50Trigger = false;
        health20Trigger = false;
        isDead          = false;
    }

    /// <summary>외부에서 실수로 불러도 무시 (난이도는 티어 데이터로만 결정).</summary>
    public override void ApplyStatMultiplier(float mult)
    {
        InitForEvolveStage();
    }

    public override void TakeDamage(float damage, bool isCritical)
    {
        base.TakeDamage(damage, isCritical);
        if (isDead) return;
        HealthPhase();
    }

    /// <summary>체력 구간마다 방어력이 오르는 페이즈 — 1회성 플래그로 매 타격 중복 상승 방지.</summary>
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

    /// <summary>훅 ① — 전리품 없음. 영구 베이스 대미지 버프를 티어당 1회만 지급.</summary>
    protected override void GrantRewards()
    {
        // base를 부르지 않습니다 — 골드/경험치를 주지 않는 것이 이 보스의 설계입니다.

        float buff = Data != null ? Data.damageBuffPercent : damageBuffPercent;
        string id  = Data != null ? Data.id : name;

        if (SaveManager.Instance != null && SaveManager.Instance.IsEvolveRewardClaimed(id))
        {
            GameLog.Info($"[EvolveBoss] 이미 보상 지급된 티어({id}) — 버프 미지급");
            return;
        }

        PlayerBuffManager.Instance?.AddPermanentDamageBuff(buff);   // 내부에서 Save() 호출
        SaveManager.Instance?.MarkEvolveRewardClaimed(id);          // 플래그 기록 + Save()

        GameLog.Info($"[EvolveBoss] 영구 대미지 버프 +{buff * 100f}% 지급 ({id})");
    }

    /// <summary>훅 ② — 일반 StageManager가 아니라 EvolveStageManager로 보고</summary>
    protected override void ReportKill()
    {
        // base 안 부름 — 일반 스테이지 킬 카운트에 더해지면 안 됨
        EvolveStageManager.Instance?.ReportBossKill();
    }

    // Die() 는 override 안 함 (부모가 전부 처리)
    // ⚠ ReportKill → 씬 전환이 사망 연출보다 먼저라 연출이 잘릴 수 있음 — Use Death Effect 해제 또는 전환 지연
}
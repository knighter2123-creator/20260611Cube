using UnityEngine;

/// <summary>일일 던전 보스(EvolveBoss 와 같은 구조) — 스탯은 던전 데이터 × 난이도 공식(스테이지 배율 무시), 전리품 없이 처치 보고는 DailyDungeonManager 로.</summary>
// 프리팹: 보스 프리팹을 복제해 EvolveBoss/BossMonster 대신 이 컴포넌트 (Enemy 인스펙터 값은 다시 채울 것)
public class DailyDungeonBoss : Enemy
{
    /// <summary>SpawnBoss 가 Instantiate 직후 호출 (base 값은 부모 Awake 에서 프리팹 원본으로 채워져 있음).</summary>
    public void InitForDungeon(DailyDungeonData data, int level)
    {
        float hpMult  = data != null ? data.HpMultiplierFor(level)      : 1f;
        float defMult = data != null ? data.DefenceMultiplierFor(level) : 1f;

        statMult      = 1f;
        maxHealth     = baseMaxHealth * hpMult;   // 누적(*=)이 아니라 대입(=)
        defence       = baseDefence   * defMult;
        currentHealth = maxHealth;
        isDead        = false;
    }

    /// <summary>외부에서 스테이지 배율을 넣어도 무시 — 던전 난이도는 던전 데이터로만 결정됩니다.</summary>
    public override void ApplyStatMultiplier(float mult)
    {
        InitForDungeon(DailyDungeonContext.SelectedData, DailyDungeonContext.SelectedLevel);
    }

    /// <summary>훅 ① — 전리품 없음. base 를 부르지 않습니다.</summary>
    protected override void GrantRewards() { }

    /// <summary>훅 ② — 일반 스테이지 킬 카운트에 더해지지 않도록 던전 매니저로만 보고.</summary>
    protected override void ReportKill()
    {
        DailyDungeonManager.Instance?.ReportBossKill();
    }
}

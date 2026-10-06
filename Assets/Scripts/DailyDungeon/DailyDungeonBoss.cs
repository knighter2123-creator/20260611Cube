using UnityEngine;

/// <summary>
/// 일일 던전 전용 보스. (EvolveBoss 와 같은 구조)
///   - 스탯은 입장한 던전 데이터 × 난이도 공식으로 결정 (스테이지 배율 statMult 는 쓰지 않음)
///   - 전리품(골드/경험치) 없음 — 던전 보상은 DailyDungeonManager 가 클리어 시 한 번에 지급
///   - 처치 보고는 StageManager 가 아니라 DailyDungeonManager 로
///
/// 기존 보스 프리팹을 쓰려면: 프리팹을 복제(또는 Prefab Variant)한 뒤
/// EvolveBoss / BossMonster 컴포넌트를 지우고 이 컴포넌트를 붙이세요.
/// (Enemy 를 상속하므로 Enemy 의 인스펙터 값은 그대로 다시 채워 넣어야 합니다)
/// </summary>
public class DailyDungeonBoss : Enemy
{
    /// <summary>
    /// DailyDungeonManager.SpawnBoss() 가 Instantiate 직후 호출합니다.
    /// baseMaxHealth / baseDefence 는 부모 Awake 에서 프리팹 원본값으로 채워져 있습니다.
    /// </summary>
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

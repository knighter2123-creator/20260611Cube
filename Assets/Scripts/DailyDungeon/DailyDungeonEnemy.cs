using UnityEngine;

/// <summary>
/// 일일 던전 디펜스 규칙(소환권 던전)에서 몰려오는 적. (DailyDungeonBoss 와 같은 구조)
///   - 스탯은 던전 데이터 × 난이도 공식으로 결정 (스테이지 배율 statMult 는 쓰지 않음)
///   - 전리품(골드/경험치) 없음 — 던전 보상은 DailyDungeonManager 가 클리어 시 한 번에 지급
///   - 처치해도 일반 스테이지 킬 카운트·미션에 더해지지 않음
///   - 도착 지점에 닿으면 DailyDungeonManager 가 Breach() 를 불러 조용히 치움 (침입 1회)
///
/// 프리팹 만들기: 일반 적 프리팹을 복제(또는 Prefab Variant)한 뒤
/// Enemy 컴포넌트를 지우고 이 컴포넌트를 붙이세요. (Enemy 의 인스펙터 값은 다시 채워 넣어야 합니다)
/// TargetMove 는 그대로 둡니다 — 경로 이동에 필요합니다.
/// </summary>
public class DailyDungeonEnemy : Enemy
{
    /// <summary>
    /// 스폰 시 OnSpawnFromPool 이 부릅니다. 넘어온 mult 는 무시하고 던전 데이터로만 계산합니다.
    /// baseMaxHealth / baseDefence 는 부모 Awake 에서 프리팹 원본값으로 채워져 있습니다 (누적 *= 금지, 대입 =).
    /// </summary>
    public override void ApplyStatMultiplier(float mult)
    {
        DailyDungeonManager manager = DailyDungeonManager.Instance;
        DailyDungeonData    data    = manager != null ? manager.ActiveData : DailyDungeonContext.SelectedData;
        int                 level   = manager != null ? manager.ActiveLevel : DailyDungeonContext.SelectedLevel;

        float hpMult  = data != null ? data.EnemyHpMultiplierFor(level)      : 1f;
        float defMult = data != null ? data.EnemyDefenceMultiplierFor(level) : 1f;

        statMult      = 1f;
        maxHealth     = baseMaxHealth * hpMult;
        defence       = baseDefence   * defMult;
        currentHealth = maxHealth;
    }

    /// <summary>훅 ① — 전리품 없음. base 를 부르지 않습니다.</summary>
    protected override void GrantRewards() { }

    /// <summary>훅 ② — 일반 스테이지 킬 카운트·미션에 더해지지 않도록 아무 데도 보고하지 않습니다.</summary>
    protected override void ReportKill() { }

    /// <summary>
    /// 도착 지점에 닿았을 때 — 죽이지 않고(보상·처치 보고 없이) 바로 풀로 돌려보냅니다.
    /// StageManager.ClearAllActiveEnemies 와 같은 정리 순서입니다.
    /// isDead 를 먼저 세우는 이유: 이미 날아오던 총알이 한 프레임 늦게 TakeDamage 를 불러도 무시되게.
    /// </summary>
    public void Breach()
    {
        if (isDead) return;
        isDead = true;

        Active.Remove(this);
        GrayscaleEffect.Clear(gameObject);
        StopAllDebuffs();
        RemoveHpBar();
        ReturnToPool();
    }
}

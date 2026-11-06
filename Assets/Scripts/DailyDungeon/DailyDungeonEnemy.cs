using UnityEngine;

/// <summary>디펜스 던전 적 — 스탯은 던전 데이터 × 난이도 공식, 전리품·킬 카운트·미션 없음, 도착 지점에 닿으면 Breach 로 조용히 치움(침입 1회).</summary>
// 프리팹: 일반 적을 복제해 Enemy 대신 이 컴포넌트 (TargetMove 는 유지, Enemy 인스펙터 값은 다시 채울 것)
public class DailyDungeonEnemy : Enemy
{
    /// <summary>스폰 시 호출 — mult 는 무시하고 던전 데이터로만, base 값에서 대입(누적 *= 금지).</summary>
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

    /// <summary>도착 지점에 닿으면 죽이지 않고 풀로 반환 — isDead 를 먼저 세워 늦게 온 총알의 TakeDamage 를 무시.</summary>
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

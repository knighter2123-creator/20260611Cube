// LevelUpManager 세이브 연동 — 레벨·경험치·강화 레벨·전투 스탯을 SaveData 와 주고받음

using UnityEngine;

public partial class LevelUpManager
{
    /// <summary>현재 스탯을 SaveData에 기록 (저장 시 SaveManager가 호출).</summary>
    public void CaptureTo(SaveData d)
    {
        if (!IsReady || d == null) return;

        d.level         = stat.Level;
        d.experience    = stat.Experience;
        d.maxExperience = stat.MaxExperience;

        d.upgradeDamage     = stat.UpgradeLevelDamage;
        d.upgradeAttackSpd  = stat.UpgradeLevelAttackSpd;
        d.upgradeCritChance = stat.UpgradeLevelCritChance;
        d.upgradeCritDamage = stat.UpgradeLevelCritDamage;

        d.baseDamage         = stat.baseDamage;
        d.critical           = stat.Critical;
        d.criticalMultiplier = stat.CriticalMultiplier;

        // 공격 주기는 불러올 때 레벨로 재계산하지만 호환성·파일 확인용으로 계속 기록
        d.attackSpd = stat.AttackSpd;
    }

    /// <summary>SaveData를 현재 스탯에 반영 (스탯 준비 후 호출 — Init 참고).</summary>
    public void ApplyFrom(SaveData d)
    {
        if (!IsReady || d == null) return;

        stat.Level = d.level > 0 ? d.level : 1;

        // 필요 경험치는 레벨로 다시 계산 (공식을 바꿔도 옛 값이 남지 않게)
        stat.MaxExperience = CalculateMaxExp(stat.Level);

        // 옛 공식 세이브는 경험치 진행률을 유지해 옮김 (그대로면 다음 처치 때 레벨이 한꺼번에 오를 수 있음)
        long savedExp = System.Math.Max(0L, d.experience);
        if (d.maxExperience > 0 && d.maxExperience != stat.MaxExperience)
        {
            double ratio = System.Math.Min(1.0, (double)savedExp / d.maxExperience);
            savedExp = (long)(ratio * stat.MaxExperience);
        }
        stat.Experience = System.Math.Min(savedExp, stat.MaxExperience - 1);

        stat.UpgradeLevelDamage     = d.upgradeDamage;
        stat.UpgradeLevelAttackSpd  = d.upgradeAttackSpd;
        stat.UpgradeLevelCritChance = d.upgradeCritChance;
        stat.UpgradeLevelCritDamage = d.upgradeCritDamage;

        // 오염된 세이브(0) 자가 치유 — 0/음수면 기본값으로 복구
        stat.baseDamage         = d.baseDamage         > 0 ? d.baseDamage         : PlayerStat.BASE_DAMAGE;
        stat.CriticalMultiplier = d.criticalMultiplier > 0 ? d.criticalMultiplier : PlayerStat.BASE_CRIT_MULT;

        // 치명타 확률·공격 주기는 강화 레벨로 다시 계산 (저장값이 오염돼도 무관, 곡선 변경도 자동 반영)
        stat.Critical  = CritChanceForLevel(stat.UpgradeLevelCritChance);
        stat.AttackSpd = AttackSpdForLevel(stat.UpgradeLevelAttackSpd);

        NotifyRestored();   // 복원은 레벨업이 아니므로 OnLevelUp 이 아니라 전용 이벤트

        GameLog.Info($"[LevelUp] ApplyFrom 완료 | Lv.{stat.Level}, dmg={stat.baseDamage}, " +
                  $"lvD={stat.UpgradeLevelDamage}, lvAS={stat.UpgradeLevelAttackSpd} → " +
                  $"{stat.AttackSpd:0.0}ms | id={GetInstanceID()}");
    }
}

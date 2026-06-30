// LevelUpManager의 세이브 연동 partial.
// 레벨/경험치/강화레벨/전투스탯을 SaveData와 주고받는다.

using UnityEngine;

partial class LevelUpManager
{
    /// <summary>현재 스탯을 SaveData에 기록 (저장 시 SaveManager가 호출).</summary>
    public void CaptureTo(SaveData d)
    {
        if (stat == null) return;

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
    }

    /// <summary>SaveData를 현재 스탯에 반영 (스탯 준비 후 호출 — Init 참고).</summary>
    public void ApplyFrom(SaveData d)
    {
        if (stat == null || d == null) return;

        stat.Level         = d.level;
        stat.Experience    = d.experience;
        stat.MaxExperience = d.maxExperience;

        stat.UpgradeLevelDamage     = d.upgradeDamage;
        stat.UpgradeLevelAttackSpd  = d.upgradeAttackSpd;
        stat.UpgradeLevelCritChance = d.upgradeCritChance;
        stat.UpgradeLevelCritDamage = d.upgradeCritDamage;

        stat.baseDamage         = d.baseDamage;
        stat.Critical           = d.critical;
        stat.CriticalMultiplier = d.criticalMultiplier;

        OnLevelUp?.Invoke(stat.Level);          // 레벨 UI 갱신
        OnExpChanged?.Invoke(stat.Experience);  // 경험치 바 갱신
        
        Debug.Log($"[LevelUp] ApplyFrom 완료 | dmg={stat.baseDamage}, lvD={stat.UpgradeLevelDamage} | id={GetInstanceID()}");
    }
}
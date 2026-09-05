// LevelUpManager의 세이브 연동 partial.
// 레벨/경험치/강화레벨/전투스탯을 SaveData와 주고받는다.

using UnityEngine;

partial class LevelUpManager
{
    /// <summary>현재 스탯을 SaveData에 기록 (저장 시 SaveManager가 호출).</summary>
    public void CaptureTo(SaveData d)
    {
        if (stat == null || d == null) return;

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
        d.attackSpd          = stat.AttackSpd;
    }

    /// <summary>SaveData를 현재 스탯에 반영 (스탯 준비 후 호출 — Init 참고).</summary>
    public void ApplyFrom(SaveData d)
    {
        if (stat == null || d == null) return;

        stat.Level      = d.level > 0 ? d.level : 1;
        stat.Experience = d.experience;

        // ★ maxExperience가 오염(0)됐을 때 100으로 고정하면 레벨과 어긋납니다.
        //   레벨에 맞는 값으로 재계산합니다.
        stat.MaxExperience = d.maxExperience > 0 ? d.maxExperience : CalculateMaxExp(stat.Level);

        stat.UpgradeLevelDamage     = d.upgradeDamage;
        stat.UpgradeLevelAttackSpd  = d.upgradeAttackSpd;
        stat.UpgradeLevelCritChance = d.upgradeCritChance;
        stat.UpgradeLevelCritDamage = d.upgradeCritDamage;

        // ★ 오염된 세이브(0) 자가 치유 — 0/음수면 기본값으로 복구
        stat.baseDamage         = d.baseDamage         > 0  ? d.baseDamage         : 20;
        stat.Critical           = d.critical           > 0  ? d.critical           : 3f;
        stat.CriticalMultiplier = d.criticalMultiplier > 0  ? d.criticalMultiplier : 1.5f;
        stat.AttackSpd          = RestoreAttackSpd(d.attackSpd, stat.UpgradeLevelAttackSpd);

        // ★★ 여기가 핵심 수정입니다.
        //   원래는 OnLevelUp 을 쏘고 있었습니다. 세이브를 불러온 것뿐인데
        //   구독자(레벨업 연출 / 가이드 퀘스트)는 "방금 Lv.1 → Lv.30 이 됐다"고 받아들여서,
        //   메인 스테이지에 들어가자마자 레벨업 연출과 블룸이 터졌습니다.
        //   복원은 레벨업이 아니므로 전용 이벤트로 알립니다.
        OnStatRestored?.Invoke(stat.Level);
        OnExpChanged?.Invoke(stat.Experience);

        Debug.Log($"[LevelUp] ApplyFrom 완료 | Lv.{stat.Level}, dmg={stat.baseDamage}, " +
                  $"lvD={stat.UpgradeLevelDamage} | id={GetInstanceID()}");
    }

    /// <summary>
    /// AttackSpd는 [100, 3000] 범위에서만 유효(ApplyGain 하한 100 클램프).
    /// 범위를 벗어난 저장값(0, 1 등 오염)은 강화 레벨로부터 재구성한다.
    /// </summary>
    private float RestoreAttackSpd(float saved, int upgradeLevel)
    {
        if (saved >= 100f && saved <= 3000f) return saved;   // 정상값은 그대로

        // ApplyGain 공식 역산: 3000 - gain * 강화레벨, 하한 100
        float restored = 3000f - attackspdConfig.gainPerUpgrade * upgradeLevel;
        restored = Mathf.Clamp(restored, 100f, 3000f);

        Debug.LogWarning($"[LevelUp] AttackSpd 오염값({saved}) 감지 → {restored}f로 복구 " +
                         $"(강화레벨 {upgradeLevel})");
        return restored;
    }
}
// LevelUpManager의 세이브 연동 partial.
// 레벨/경험치/강화레벨/전투스탯을 SaveData와 주고받는다.
//
// ★ 이번 수정: 공격 속도와 치명타 확률을 세이브 값이 아니라 '강화 레벨'에서 다시 계산해 복원합니다.
//   RestoreAttackSpd() 는 더 이상 필요 없어서 제거했습니다. (아래 ApplyFrom 주석 참고)

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

        // ※ 공격 주기는 이제 불러올 때 쓰이지 않지만 계속 기록합니다.
        //   SaveData 의 필드를 지우거나 이름을 바꾸면 호환성이 깨질 수 있고,
        //   세이브 파일을 열어봤을 때 현재 값을 눈으로 확인하는 용도로도 쓸모가 있습니다.
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
        stat.CriticalMultiplier = d.criticalMultiplier > 0  ? d.criticalMultiplier : 1.5f;

        // ★ [수정] 치명타 확률도 공격 속도와 마찬가지로 강화 레벨에서 다시 계산합니다.
        //   PlayerStat 에 "전투 스탯 — 강화로만 상승" 이라고 적혀 있듯이,
        //   이 값을 바꾸는 건 강화뿐이라 레벨만 있으면 항상 복원할 수 있습니다.
        //   (증강 카드로 오르는 치명타 확률은 FinalCritical 쪽 레이어라 여기와 무관합니다)
        stat.Critical = CritChanceForLevel(stat.UpgradeLevelCritChance);

        // ═══ ★ [수정] 공격 주기는 저장값을 믿지 않고 강화 레벨에서 다시 계산합니다 ═══
        //
        // 예전에는 d.attackSpd 를 읽고, 범위를 벗어나면(오염) RestoreAttackSpd() 로
        // "강화 레벨로부터 역산"해서 고쳤습니다. 즉 **원래도 강화 레벨이 진짜 기준**이었고
        // 저장된 공격 주기는 그걸 베껴둔 사본이었던 셈입니다.
        //
        // 사본을 읽고 → 의심하고 → 틀리면 원본으로 다시 계산하는 대신,
        // 처음부터 원본(레벨)으로 계산하면 오염 검사 자체가 필요 없어집니다.
        // 사본이 오염돼도 영향이 없으니까요.
        //
        // 덤으로, 인스펙터에서 공격 속도 곡선을 바꿔도 다음 실행 때
        // 새 곡선으로 자동 재계산됩니다. 세이브를 지우지 않아도 됩니다.
        stat.AttackSpd = AttackSpdForLevel(stat.UpgradeLevelAttackSpd);

        // ★★ 여기가 핵심 수정입니다.
        //   원래는 OnLevelUp 을 쏘고 있었습니다. 세이브를 불러온 것뿐인데
        //   구독자(레벨업 연출 / 가이드 퀘스트)는 "방금 Lv.1 → Lv.30 이 됐다"고 받아들여서,
        //   메인 스테이지에 들어가자마자 레벨업 연출과 블룸이 터졌습니다.
        //   복원은 레벨업이 아니므로 전용 이벤트로 알립니다.
        OnStatRestored?.Invoke(stat.Level);
        OnExpChanged?.Invoke(stat.Experience);

        Debug.Log($"[LevelUp] ApplyFrom 완료 | Lv.{stat.Level}, dmg={stat.baseDamage}, " +
                  $"lvD={stat.UpgradeLevelDamage}, lvAS={stat.UpgradeLevelAttackSpd} → " +
                  $"{stat.AttackSpd:0.0}ms | id={GetInstanceID()}");
    }
}
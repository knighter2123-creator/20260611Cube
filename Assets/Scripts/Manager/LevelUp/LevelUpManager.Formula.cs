using UnityEngine;

/// <summary>레벨 → 수치 공식·미리보기: 공격 속도·치명타 확률은 누적하지 않고 강화 레벨로 매번 계산 — Lv.5000 에 정확히 최대, float 누적 오차 없음, 곡선을 바꿔도 세이브가 안 꼬임.</summary>
public partial class LevelUpManager
{
    /// <summary>공격 속도 곡선 — 둘 다 Lv.0 = 3000ms, Lv.5000 = 100ms 로 양 끝은 같고 중간 경로만 다름.</summary>
    public enum AttackSpeedCurve
    {
        /// <summary>공격 주기(ms)가 일정하게 줄어듦. 초반엔 체감이 거의 없고 막판에 폭발.</summary>
        LinearMs,

        /// <summary>초당 공격 횟수가 일정하게 늘어남. 매 강화의 DPS 증가량이 같음. (권장)</summary>
        LinearSpeed,
    }

    [Header("공격 속도 곡선")]
    [Tooltip("LinearSpeed(권장): 강화 1회당 DPS 증가량이 항상 같습니다.\n" +
             "LinearMs: ms 가 일정하게 줄지만, 초반엔 체감이 거의 없고 마지막 1000레벨에 성장이 몰립니다.\n\n" +
             "어느 쪽이든 Lv.5000 에서 정확히 100ms 에 도달합니다.\n" +
             "우클릭 → '테스트: 공격 속도 곡선 출력' 으로 두 곡선을 비교할 수 있습니다.")]
    [SerializeField] private AttackSpeedCurve attackSpeedCurve = AttackSpeedCurve.LinearSpeed;

    /// <summary>강화 레벨 → 공격 주기(ms) — 강화·복원·스탯창이 모두 이 함수 하나로 얻음.</summary>
    public float AttackSpdForLevel(int level)
    {
        // 양 끝은 상수 그대로 (float 나눗셈 오차로 '정확히 같은가' 비교가 흔들리지 않게)
        if (level <= 0)                 return PlayerStat.BASE_ATTACK_SPD;
        if (level >= MAX_UPGRADE_LEVEL) return PlayerStat.MIN_ATTACK_SPD;

        float t = UpgradeProgress(level);

        // 3000 → 100 을 곧게 잇습니다. (레벨당 0.58ms)
        if (attackSpeedCurve == AttackSpeedCurve.LinearMs)
            return Mathf.Lerp(PlayerStat.BASE_ATTACK_SPD, PlayerStat.MIN_ATTACK_SPD, t);

        // LinearSpeed: DPS 는 초당 공격 횟수(1000/ms)에 비례 → 횟수를 일정하게 늘려 매 강화가 같은 DPS 를 더함 (ms 를 일정하게 줄이면 막판 1ms 가 약 900배 가치)
        float apsStart = 1000f / PlayerStat.BASE_ATTACK_SPD;   // 초당 0.333회
        float apsEnd   = 1000f / PlayerStat.MIN_ATTACK_SPD;    // 초당 10회
        return 1000f / Mathf.Lerp(apsStart, apsEnd, t);
    }

    /// <summary>강화 레벨 → 치명타 확률(Lv.0 = 3%, Lv.5000 = 100%, 직선) — 기대 대미지가 확률에 정비례라 직선이 곧 매 강화 같은 DPS.</summary>
    public float CritChanceForLevel(int level)
    {
        // 양 끝은 상수 (99.99999 가 나오면 스탯창 "(최대)" 가 영영 안 뜸)
        if (level <= 0)                 return PlayerStat.BASE_CRITICAL;
        if (level >= MAX_UPGRADE_LEVEL) return PlayerStat.MAX_CRITICAL;

        return Mathf.Lerp(PlayerStat.BASE_CRITICAL, PlayerStat.MAX_CRITICAL, UpgradeProgress(level));
    }

    /// <summary>강화 진행도 0~1. (float) 을 붙이지 않으면 정수 나눗셈이 되어 항상 0 이 나옵니다.</summary>
    private static float UpgradeProgress(int level) => level / (float)MAX_UPGRADE_LEVEL;

    /// <summary>공격력 1회 강화 증가량. 적용(ApplyGain)과 미리보기가 같은 반올림을 쓰도록 한 곳에 둡니다.</summary>
    private int DamageGainPerUpgrade => Mathf.RoundToInt(damageConfig.gainPerUpgrade);

    /// <summary>levels 번 더 강화했을 때의 스탯 값(미리보기, 실제 스탯 불변, 단위는 PlayerStat 과 같음 — 공격 속도는 ms) — 공식은 이 매니저 한 곳에만 두고 UI 는 묻기만.</summary>
    public float PreviewStatValue(StatType type, int levels)
    {
        if (!IsReady) return 0f;

        int current = GetUpgradeLevelValue(type);
        int steps   = ClampToRemaining(current, levels);   // 상한에 걸리면 levels 보다 작아집니다
        int target  = current + steps;

        return type switch
        {
            // ApplyGain 과 같게 1회분을 반올림한 뒤 곱함 (전체를 곱한 뒤 반올림하면 실제와 어긋남)
            StatType.Damage     => stat.baseDamage + DamageGainPerUpgrade * steps,
            StatType.CritChance => CritChanceForLevel(target),
            StatType.CritDamage => stat.CriticalMultiplier + critDamageConfig.gainPerUpgrade * steps,
            StatType.Attackspd  => AttackSpdForLevel(target),
            _                   => 0f
        };
    }
}

using UnityEngine;

/// <summary>
/// LevelUpManager — 레벨 → 수치 공식 / 미리보기.
///
/// [원칙] 레벨 → 수치는 '쌓지 말고 계산한다'
///   공격 속도와 치명타 확률은 강화할 때마다 값을 더하거나 빼서 누적하지 않고,
///   강화 레벨을 공식에 넣어 매번 처음부터 계산합니다 (AttackSpdForLevel / CritChanceForLevel).
///     · Lv.5000 에서 정확히 최대치, 그 전에는 절대 닿지 않음 (골드만 쓰고 효과 없는 구간이 없음)
///     · float 누적 오차 없음
///     · 곡선을 바꿔도 세이브가 꼬이지 않음 (불러올 때 레벨로 다시 계산)
///   PlayerStat.FinalDamage 를 프로퍼티로 만든 것과 같은 원리입니다 —
///   **"저장된 결과값"보다 "원인(레벨)에서 다시 계산한 값"이 믿을 만합니다.**
/// </summary>
public partial class LevelUpManager
{
    /// <summary>
    /// 강화 레벨에 따라 공격 속도가 어떤 모양으로 빨라지는가.
    /// 두 곡선 모두 Lv.0 = 3000ms, Lv.5000 = 100ms 로 **양 끝은 같고** 중간 경로만 다릅니다.
    /// </summary>
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

    /// <summary>
    /// 강화 레벨 → 공격 주기(ms).
    /// 강화, 세이브 복원, 스탯창이 **모두 이 함수 하나**로 공격 주기를 얻습니다.
    /// </summary>
    public float AttackSpdForLevel(int level)
    {
        // 양 끝은 공식을 거치지 않고 상수를 그대로 돌려줍니다.
        // float 나눗셈은 1000 / (1000/3000) 이 정확히 3000 이 아니라 2999.9999... 가 될 수 있어서
        // IsAttackSpeedCapped(<= 100) 같은 '정확히 같은가' 비교가 흔들립니다.
        if (level <= 0)                 return PlayerStat.BASE_ATTACK_SPD;
        if (level >= MAX_UPGRADE_LEVEL) return PlayerStat.MIN_ATTACK_SPD;

        float t = UpgradeProgress(level);

        // 3000 → 100 을 곧게 잇습니다. (레벨당 0.58ms)
        if (attackSpeedCurve == AttackSpeedCurve.LinearMs)
            return Mathf.Lerp(PlayerStat.BASE_ATTACK_SPD, PlayerStat.MIN_ATTACK_SPD, t);

        // ─── LinearSpeed: 왜 '초당 공격 횟수'로 계산하는가 ─────────────
        // DPS 는 공격 주기가 아니라 **초당 공격 횟수**(= 1000 / ms)에 비례합니다.
        //   3000ms → 2999ms : 초당 0.3333 → 0.3334회   (+0.03%)
        //    101ms →  100ms : 초당 9.90   → 10.0회     (+1%)
        // 같은 1ms 인데 막판이 약 900배 가치가 있습니다.
        // 초당 횟수를 일정하게 늘리면 매 강화가 똑같은 DPS 를 더해줍니다.
        // ────────────────────────────────────────────────────────────
        float apsStart = 1000f / PlayerStat.BASE_ATTACK_SPD;   // 초당 0.333회
        float apsEnd   = 1000f / PlayerStat.MIN_ATTACK_SPD;    // 초당 10회
        return 1000f / Mathf.Lerp(apsStart, apsEnd, t);
    }

    /// <summary>
    /// 강화 레벨 → 치명타 확률(%). Lv.0 = 3%, Lv.5000 = 100%, 그 사이는 직선.
    ///
    /// 공격 속도와 달리 곡선 선택지가 없는 이유: 기대 대미지
    ///     공격력 × (1 + 치명타확률 × (치명타배수 − 1))
    /// 가 **확률에 정비례**하므로, 직선이 곧 "매 강화가 같은 DPS 를 더하는" 공평한 곡선입니다.
    /// (곡선은 "스탯 수치"가 아니라 "그 스탯이 DPS 에 어떻게 들어가는가"를 보고 고릅니다)
    /// </summary>
    public float CritChanceForLevel(int level)
    {
        // 양 끝은 상수로 못 박습니다. 스탯창의 "(최대)" 표시가 FinalCritical >= MAX_CRITICAL 로
        // 판단하므로, 5000레벨에서 99.99999 가 나오면 "(최대)" 가 영영 안 뜹니다.
        if (level <= 0)                 return PlayerStat.BASE_CRITICAL;
        if (level >= MAX_UPGRADE_LEVEL) return PlayerStat.MAX_CRITICAL;

        return Mathf.Lerp(PlayerStat.BASE_CRITICAL, PlayerStat.MAX_CRITICAL, UpgradeProgress(level));
    }

    /// <summary>강화 진행도 0~1. (float) 을 붙이지 않으면 정수 나눗셈이 되어 항상 0 이 나옵니다.</summary>
    private static float UpgradeProgress(int level) => level / (float)MAX_UPGRADE_LEVEL;

    /// <summary>공격력 1회 강화 증가량. 적용(ApplyGain)과 미리보기가 같은 반올림을 쓰도록 한 곳에 둡니다.</summary>
    private int DamageGainPerUpgrade => Mathf.RoundToInt(damageConfig.gainPerUpgrade);

    /// <summary>
    /// 지금부터 levels 번 더 강화했을 때의 스탯 값 (강화창 '현재 → 다음' 미리보기용).
    /// **실제 스탯은 건드리지 않습니다.**
    ///
    /// 반환값의 단위는 스탯마다 PlayerStat 에 저장되는 단위와 같습니다.
    ///   Damage     → baseDamage
    ///   CritChance → Critical (%)
    ///   CritDamage → CriticalMultiplier (배수)
    ///   Attackspd  → AttackSpd (ms)   ※ UI 에서 초당 횟수로 바꿔 보여주세요
    ///
    /// 증가량과 곡선은 이 매니저만 압니다. UI 가 공식을 따로 적어두면 인스펙터 값을 바꾸는 순간
    /// 미리보기만 틀립니다 → **공식은 한 곳에만, UI 는 물어보기만.**
    /// </summary>
    public float PreviewStatValue(StatType type, int levels)
    {
        if (!IsReady) return 0f;

        int current = GetUpgradeLevelValue(type);
        int steps   = ClampToRemaining(current, levels);   // 상한에 걸리면 levels 보다 작아집니다
        int target  = current + steps;

        return type switch
        {
            // ApplyGain 과 똑같이 '1회분을 반올림한 뒤' 곱합니다.
            // (전체를 곱한 뒤 반올림하면 소수 증가량일 때 실제와 어긋납니다)
            StatType.Damage     => stat.baseDamage + DamageGainPerUpgrade * steps,
            StatType.CritChance => CritChanceForLevel(target),
            StatType.CritDamage => stat.CriticalMultiplier + critDamageConfig.gainPerUpgrade * steps,
            StatType.Attackspd  => AttackSpdForLevel(target),
            _                   => 0f
        };
    }
}

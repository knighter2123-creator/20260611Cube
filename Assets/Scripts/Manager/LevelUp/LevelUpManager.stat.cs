using System;
using UnityEngine;

/// <summary>
/// LevelUpManager — 스탯 강화.
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
    /// <summary>강화 상한. UI 도 이 값을 읽습니다 (하드코딩 금지).</summary>
    public const int MAX_UPGRADE_LEVEL = 5000;

    /// <summary>강화 상한 (UI 표시용)</summary>
    public int MaxUpgradeLevel => MAX_UPGRADE_LEVEL;

    /// <summary>스탯 강화 성공 시 강화 종류를 전달합니다.</summary>
    public event Action<StatType> OnStatUpgraded;

    // ×10 / ×100 반복 강화 중에는 이벤트를 억제하고, 끝난 뒤 한 번만 알립니다.
    private bool batchingUpgrades;

    // ══════════════════════════════════════════════
    //  강화 대상 열거형
    // ══════════════════════════════════════════════
    public enum StatType
    {
        Damage,
        CritChance,
        CritDamage,
        Attackspd,
    }

    // ══════════════════════════════════════════════
    //  강화 설정 구조체  (인스펙터에서 조정 가능)
    // ══════════════════════════════════════════════
    [Serializable]
    public struct UpgradeConfig
    {
        [Tooltip("강화 Lv.0 -> Lv.1 기준 비용")]
        public int baseCost;

        [Tooltip("강화 레벨 1 증가 시 추가 비용")]
        public int costPerLevel;

        [Tooltip("강화 1회당 고정 스탯 증가량\n" +
                 "※ 공격 속도와 치명타 확률은 이 값을 쓰지 않습니다.\n" +
                 "   두 스탯은 '레벨 → 수치' 공식으로 계산되어 Lv.5000 에서 정확히 최대치가 됩니다.")]
        public float gainPerUpgrade;
    }

    [Header("스탯별 강화 설정")]
    [SerializeField] private UpgradeConfig damageConfig     = new UpgradeConfig { baseCost = 10,  costPerLevel = 15,  gainPerUpgrade = 5f    };

    [Tooltip("공격 속도는 비용(baseCost / costPerLevel)만 사용합니다.\n" +
             "gainPerUpgrade 는 무시됩니다 — 증가량은 '공격 속도 곡선'이 레벨로 계산합니다.")]
    [SerializeField] private UpgradeConfig attackspdConfig  = new UpgradeConfig { baseCost = 150, costPerLevel = 50,  gainPerUpgrade = 10f   };

    [Tooltip("치명타 확률은 비용(baseCost / costPerLevel)만 사용합니다.\n" +
             "gainPerUpgrade 는 무시됩니다 — Lv.0 = 3%, Lv.5000 = 100% 를 직선으로 잇습니다.")]
    [SerializeField] private UpgradeConfig critChanceConfig = new UpgradeConfig { baseCost = 300, costPerLevel = 150, gainPerUpgrade = 0.05f };
    [SerializeField] private UpgradeConfig critDamageConfig = new UpgradeConfig { baseCost = 100, costPerLevel = 30,  gainPerUpgrade = 0.1f  };

    // ══════════════════════════════════════════════
    //  레벨 → 수치 공식
    // ══════════════════════════════════════════════

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

        switch (attackSpeedCurve)
        {
            case AttackSpeedCurve.LinearMs:
                // 3000 → 100 을 곧게 잇습니다. (레벨당 0.58ms)
                return Mathf.Lerp(PlayerStat.BASE_ATTACK_SPD, PlayerStat.MIN_ATTACK_SPD, t);

            default: // LinearSpeed
            {
                // ─── 왜 '초당 공격 횟수'로 계산하는가 ─────────────
                // DPS 는 공격 주기가 아니라 **초당 공격 횟수**(= 1000 / ms)에 비례합니다.
                //   3000ms → 2999ms : 초당 0.3333 → 0.3334회   (+0.03%)
                //    101ms →  100ms : 초당 9.90   → 10.0회     (+1%)
                // 같은 1ms 인데 막판이 약 900배 가치가 있습니다.
                // 초당 횟수를 일정하게 늘리면 매 강화가 똑같은 DPS 를 더해줍니다.
                // ────────────────────────────────────────────────
                float apsStart = 1000f / PlayerStat.BASE_ATTACK_SPD;   // 초당 0.333회
                float apsEnd   = 1000f / PlayerStat.MIN_ATTACK_SPD;    // 초당 10회
                float aps      = Mathf.Lerp(apsStart, apsEnd, t);
                return 1000f / aps;
            }
        }
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
        if (stat == null) return 0f;

        int current = GetUpgradeLevelValue(type);
        int steps   = ClampToRemaining(current, levels);   // 상한에 걸리면 levels 보다 작아집니다
        int target  = current + steps;

        switch (type)
        {
            case StatType.Damage:
                // ApplyGain 과 똑같이 '1회분을 반올림한 뒤' 곱합니다.
                // (전체를 곱한 뒤 반올림하면 소수 증가량일 때 실제와 어긋납니다)
                return stat.baseDamage + DamageGainPerUpgrade * steps;

            case StatType.CritChance:
                return CritChanceForLevel(target);

            case StatType.CritDamage:
                return stat.CriticalMultiplier + critDamageConfig.gainPerUpgrade * steps;

            case StatType.Attackspd:
                return AttackSpdForLevel(target);

            default:
                return 0f;
        }
    }

    /// <summary>공격력 1회 강화 증가량. 적용(ApplyGain)과 미리보기가 같은 반올림을 쓰도록 한 곳에 둡니다.</summary>
    private int DamageGainPerUpgrade => Mathf.RoundToInt(damageConfig.gainPerUpgrade);

    // ══════════════════════════════════════════════
    //  스탯 강화  (Currency 소비)
    // ══════════════════════════════════════════════

    /// <summary>다음 강화 비용을 반환합니다. (UI 표시용)</summary>
    public int GetUpgradeCost(StatType type)
    {
        if (stat == null) return 0;
        return CalculateCost(GetConfig(type), GetUpgradeLevelValue(type));
    }

    /// <summary>현재 강화 레벨을 반환합니다. (UI 표시용)</summary>
    public int GetUpgradeLevel(StatType type)
    {
        if (stat == null) return 0;
        return GetUpgradeLevelValue(type);
    }

    /// <summary>해당 스탯이 최대 강화 레벨에 도달했는가.</summary>
    public bool IsMaxUpgraded(StatType type)
    {
        if (stat == null) return false;
        return GetUpgradeLevelValue(type) >= MAX_UPGRADE_LEVEL;
    }

    /// <summary>Currency를 소비해 스탯을 1단계 강화합니다.</summary>
    /// <returns>강화 성공 여부</returns>
    public bool TryUpgrade(StatType type)
    {
        if (stat == null)
        {
            Debug.LogError("[LevelUpManager] stat이 null입니다.");
            return false;
        }

        int currentLv = GetUpgradeLevelValue(type);
        if (currentLv >= MAX_UPGRADE_LEVEL) return false;

        CurrencyManager cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogError("[LevelUpManager] CurrencyManager.Instance 가 없어 강화할 수 없습니다.");
            return false;
        }

        if (!cm.SpendGold(CalculateCost(GetConfig(type), currentLv))) return false;

        // 레벨을 먼저 올리고 스탯을 반영합니다.
        // 공격 속도와 치명타 확률은 '새 레벨'에서 계산하므로 순서가 바뀌면 한 단계씩 밀립니다.
        SetUpgradeLevelValue(type, currentLv + 1);
        ApplyGain(type);

        if (!batchingUpgrades)
            OnStatUpgraded?.Invoke(type);

        return true;
    }

    /// <summary>
    /// 최대 times 횟수만큼 반복 강화합니다. Currency가 부족하면 중단합니다.
    /// UI의 ×10 / ×100 버튼 등에 활용하세요.
    /// </summary>
    /// <returns>실제 강화 성공 횟수</returns>
    public int TryUpgradeMultiple(StatType type, int times)
    {
        if (times <= 0) return 0;
        if (times == 1) return TryUpgrade(type) ? 1 : 0;

        int successCount = 0;

        // ×100이면 OnStatUpgraded가 100번 발화해 구독자(UpgradeUI)가 매번 TMP를 갱신하게 됩니다.
        // 반복 중에는 억제하고 끝난 뒤 한 번만 알립니다.
        batchingUpgrades = true;
        try
        {
            for (int i = 0; i < times; i++)
            {
                if (!TryUpgrade(type)) break;
                successCount++;
            }
        }
        finally
        {
            batchingUpgrades = false;
        }

        if (successCount > 0)
            OnStatUpgraded?.Invoke(type);

        return successCount;
    }

    /// <summary>
    /// 비용 공식 — 프로젝트 전체에서 '1회 강화 비용'을 정의하는 유일한 곳입니다.
    /// N회 누적 비용(MultiCost.cs)도 이 함수를 N번 더해서 구합니다.
    /// 비용 곡선을 바꾸고 싶으면 여기 한 줄만 고치면 UI 표시와 실제 결제가 함께 바뀝니다.
    /// </summary>
    private static int CalculateCost(UpgradeConfig config, int currentLv)
        => config.baseCost + config.costPerLevel * currentLv;

    /// <summary>
    /// 현재 레벨에서 최대 times 회 강화하려 할 때, **상한 때문에 실제로 가능한 횟수**.
    /// 비용 · 미리보기 · 구매 가능 횟수가 모두 이 함수를 써서 상한 규칙이 한 곳에만 있습니다.
    /// </summary>
    private static int ClampToRemaining(int currentLv, int times)
    {
        int remain = MAX_UPGRADE_LEVEL - currentLv;

        // remain 이 음수(손상된 세이브)면 0, times 보다 크면 times.
        return Mathf.Clamp(remain, 0, Mathf.Max(0, times));
    }

    /// <summary>강화 1회분을 스탯에 반영합니다. (레벨은 이미 올라가 있어야 합니다 — TryUpgrade 참고)</summary>
    private void ApplyGain(StatType type)
    {
        switch (type)
        {
            case StatType.Damage:
                stat.baseDamage += DamageGainPerUpgrade;
                break;

            case StatType.CritChance:
                stat.Critical = CritChanceForLevel(stat.UpgradeLevelCritChance);
                break;

            case StatType.CritDamage:
                stat.CriticalMultiplier += critDamageConfig.gainPerUpgrade;
                break;

            case StatType.Attackspd:
                stat.AttackSpd = AttackSpdForLevel(stat.UpgradeLevelAttackSpd);
                break;
        }
    }

    // ══════════════════════════════════════════════
    //  StatType → 설정 / 레벨  (switch 는 여기 세 곳에만)
    // ══════════════════════════════════════════════

    private UpgradeConfig GetConfig(StatType type)
    {
        return type switch
        {
            StatType.Damage     => damageConfig,
            StatType.CritChance => critChanceConfig,
            StatType.CritDamage => critDamageConfig,
            StatType.Attackspd  => attackspdConfig,
            _                   => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private int GetUpgradeLevelValue(StatType type)
    {
        return type switch
        {
            StatType.Damage     => stat.UpgradeLevelDamage,
            StatType.CritChance => stat.UpgradeLevelCritChance,
            StatType.CritDamage => stat.UpgradeLevelCritDamage,
            StatType.Attackspd  => stat.UpgradeLevelAttackSpd,
            _                   => 0
        };
    }

    private void SetUpgradeLevelValue(StatType type, int value)
    {
        switch (type)
        {
            case StatType.Damage:     stat.UpgradeLevelDamage     = value; break;
            case StatType.CritChance: stat.UpgradeLevelCritChance = value; break;
            case StatType.CritDamage: stat.UpgradeLevelCritDamage = value; break;
            case StatType.Attackspd:  stat.UpgradeLevelAttackSpd  = value; break;
        }
    }

    // ══════════════════════════════════════════════
    //  에디터 테스트 — 곡선 비교
    // ══════════════════════════════════════════════

    /// <summary>
    /// 두 곡선의 레벨별 공격 주기 / 초당 공격 횟수를 콘솔에 표로 찍습니다.
    /// Play 하지 않아도 동작합니다 (스탯이 아니라 공식만 쓰기 때문).
    /// </summary>
    [ContextMenu("테스트: 공격 속도 곡선 출력")]
    private void DumpAttackSpeedCurves()
    {
        AttackSpeedCurve saved = attackSpeedCurve;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[공격 속도 곡선]   레벨  |  LinearMs (ms / 초당)  |  LinearSpeed (ms / 초당)");

        try
        {
            for (int lv = 0; lv <= MAX_UPGRADE_LEVEL; lv += 500)
            {
                attackSpeedCurve = AttackSpeedCurve.LinearMs;
                float a = AttackSpdForLevel(lv);

                attackSpeedCurve = AttackSpeedCurve.LinearSpeed;
                float b = AttackSpdForLevel(lv);

                sb.AppendLine($"  Lv.{lv,4}  |  {a,7:0.0}ms / {1000f / a,5:0.00}회  |  {b,7:0.0}ms / {1000f / b,5:0.00}회");
            }
        }
        finally
        {
            // 비교하느라 잠깐 바꾼 설정을 반드시 원래대로 돌려놓습니다.
            attackSpeedCurve = saved;
        }

        Debug.Log(sb.ToString());
    }
}

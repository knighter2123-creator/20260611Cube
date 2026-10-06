using System;
using UnityEngine;

/// <summary>
/// LevelUpManager — 스탯 강화 (설정 · 비용 · 결제).
/// 레벨 → 수치 공식은 LevelUpManager.Formula.cs 에 있습니다.
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
    //  조회  (UI 표시용)
    // ══════════════════════════════════════════════

    /// <summary>다음 강화 비용을 반환합니다.</summary>
    public int GetUpgradeCost(StatType type)
        => IsReady ? CalculateCost(GetConfig(type), GetUpgradeLevelValue(type)) : 0;

    /// <summary>현재 강화 레벨을 반환합니다.</summary>
    public int GetUpgradeLevel(StatType type)
        => IsReady ? GetUpgradeLevelValue(type) : 0;

    /// <summary>해당 스탯이 최대 강화 레벨에 도달했는가.</summary>
    public bool IsMaxUpgraded(StatType type)
        => IsReady && GetUpgradeLevelValue(type) >= MAX_UPGRADE_LEVEL;

    // ══════════════════════════════════════════════
    //  스탯 강화  (Currency 소비)
    // ══════════════════════════════════════════════

    /// <summary>Currency를 소비해 스탯을 1단계 강화합니다.</summary>
    /// <returns>강화 성공 여부</returns>
    public bool TryUpgrade(StatType type)
    {
        if (!IsReady)
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
            while (successCount < times && TryUpgrade(type))
                successCount++;
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
    /// remain 이 음수(손상된 세이브)면 0, times 보다 크면 times.
    /// </summary>
    private static int ClampToRemaining(int currentLv, int times)
        => Mathf.Clamp(MAX_UPGRADE_LEVEL - currentLv, 0, Mathf.Max(0, times));

    /// <summary>강화 1회분을 스탯에 반영합니다. (레벨은 이미 올라가 있어야 합니다 — TryUpgrade 참고)</summary>
    private void ApplyGain(StatType type)
    {
        switch (type)
        {
            case StatType.Damage:     stat.baseDamage         += DamageGainPerUpgrade;                          break;
            case StatType.CritChance: stat.Critical            = CritChanceForLevel(stat.UpgradeLevelCritChance); break;
            case StatType.CritDamage: stat.CriticalMultiplier += critDamageConfig.gainPerUpgrade;               break;
            case StatType.Attackspd:  stat.AttackSpd           = AttackSpdForLevel(stat.UpgradeLevelAttackSpd);   break;
        }
    }

    // ══════════════════════════════════════════════
    //  StatType → 설정 / 레벨  (switch 는 여기 세 곳에만)
    // ══════════════════════════════════════════════

    private UpgradeConfig GetConfig(StatType type) => type switch
    {
        StatType.Damage     => damageConfig,
        StatType.CritChance => critChanceConfig,
        StatType.CritDamage => critDamageConfig,
        StatType.Attackspd  => attackspdConfig,
        _                   => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private int GetUpgradeLevelValue(StatType type) => type switch
    {
        StatType.Damage     => stat.UpgradeLevelDamage,
        StatType.CritChance => stat.UpgradeLevelCritChance,
        StatType.CritDamage => stat.UpgradeLevelCritDamage,
        StatType.Attackspd  => stat.UpgradeLevelAttackSpd,
        _                   => 0
    };

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
}

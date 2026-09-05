using System;
using UnityEngine;

public partial class LevelUpManager
{
    // ★ private const 였던 것을 공개합니다.
    //   UpgradeUI가 5000을 하드코딩하고 있어서, 여기 값을 바꾸면 UI가 거짓말을 했습니다.
    public const int MAX_UPGRADE_LEVEL = 5000;

    /// <summary>강화 상한 (UI 표시용)</summary>
    public int MaxUpgradeLevel => MAX_UPGRADE_LEVEL;

    /// <summary>스탯 강화 성공 시 강화 종류를 전달합니다.</summary>
    public event Action<StatType> OnStatUpgraded;

    // ×10 / ×100 반복 강화 중에는 이벤트와 로그를 억제하고, 끝난 뒤 한 번만 알립니다.
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

        [Tooltip("강화 1회당 고정 스탯 증가량")]
        public float gainPerUpgrade;
    }

    [Header("스탯별 강화 설정")]
    [SerializeField] private UpgradeConfig damageConfig     = new UpgradeConfig { baseCost = 10,  costPerLevel = 15,  gainPerUpgrade = 5f    };
    [SerializeField] private UpgradeConfig attackspdConfig  = new UpgradeConfig { baseCost = 150, costPerLevel = 50,  gainPerUpgrade = 10f   };
    [SerializeField] private UpgradeConfig critChanceConfig = new UpgradeConfig { baseCost = 300, costPerLevel = 150, gainPerUpgrade = 0.05f };
    [SerializeField] private UpgradeConfig critDamageConfig = new UpgradeConfig { baseCost = 100, costPerLevel = 30,  gainPerUpgrade = 0.1f  };

    // ══════════════════════════════════════════════
    //  [3] 스탯 강화  (Currency 소비)
    // ══════════════════════════════════════════════

    /// <summary>
    /// 다음 강화 비용을 반환합니다. (UI 표시용)
    /// 비용 공식: baseCost + (costPerLevel × 현재강화레벨)
    /// </summary>
    public int GetUpgradeCost(StatType type)
    {
        if (stat == null) return 0;
        var (config, currentLv) = GetConfigAndLevel(type);
        return CalculateCost(config, currentLv);
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

    /// <summary>
    /// Currency를 소비해 스탯을 1단계 강화합니다.
    /// </summary>
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

        var (config, _) = GetConfigAndLevel(type);
        int cost = CalculateCost(config, currentLv);

        // ★ CurrencyManager가 없으면 NullReferenceException으로 터지던 부분.
        //   다른 호출부는 전부 ?. 를 쓰는데 여기만 무방비였습니다.
        CurrencyManager cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogError("[LevelUpManager] CurrencyManager.Instance 가 없어 강화할 수 없습니다.");
            return false;
        }

        if (!cm.SpendGold(cost)) return false;

        ApplyGain(type, config.gainPerUpgrade);
        SetUpgradeLevelValue(type, currentLv + 1);

        if (!batchingUpgrades)
        {
            OnStatUpgraded?.Invoke(type);
            Debug.Log($"[LevelUpManager] {type} 강화 Lv.{currentLv + 1} | 비용 {cost}");
        }

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

        // ★ 원래는 ×100이면 OnStatUpgraded가 100번 발화하고 Debug.Log도 100번 찍혔습니다.
        //   구독자(UpgradeUI)가 100번 문자열을 만들고 TMP를 갱신하게 되어 모바일에서 프레임이 튑니다.
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
        {
            OnStatUpgraded?.Invoke(type);   // 끝난 뒤 한 번만
            Debug.Log($"[LevelUpManager] {type} 강화 {successCount}회 성공 " +
                      $"(요청 {times}회) → Lv.{GetUpgradeLevelValue(type)}");
        }

        return successCount;
    }

    // 비용 공식
    private int CalculateCost(UpgradeConfig config, int currentLv)
        => config.baseCost + config.costPerLevel * currentLv;

    // 스탯 적용
    private void ApplyGain(StatType type, float gain)
    {
        switch (type)
        {
            case StatType.Damage:
                stat.baseDamage += Mathf.RoundToInt(gain);
                break;

            case StatType.CritChance:
                stat.Critical = Mathf.Min(stat.Critical + gain, 100f);
                break;

            case StatType.CritDamage:
                stat.CriticalMultiplier += gain;
                break;

            case StatType.Attackspd:
                // ✅ AttackSpd(ms) 감소 → 쿨다운 감소 → 공격속도 증가
                // 최소 100ms(0.1초) 아래로 내려가지 않도록 클램프
                stat.AttackSpd = Mathf.Max(stat.AttackSpd - Mathf.RoundToInt(gain), 100);

                // 반복 강화 중에는 로그를 찍지 않습니다 (×100이면 100줄)
                if (!batchingUpgrades)
                    Debug.Log($"[LevelUpManager] AttackSpd: {stat.AttackSpd}ms → " +
                              $"cooldown: {stat.AttackSpd / 1000f:F3}초");
                break;
        }
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
}
using System;
using UnityEngine;
using Random = System.Random;


public class LevelUpManager : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  싱글턴
    // ──────────────────────────────────────────────
    public static LevelUpManager Instance;

    private PlayerStat stat;
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    // ══════════════════════════════════════════════
    //  이벤트
    // ══════════════════════════════════════════════
    /// <summary>스탯 강화 성공 시 강화 종류를 전달합니다.</summary>
    public event Action<StatType> OnStatUpgraded;
    public event Action<int> OnLevelUp; // 레벨업 시 새 레벨 전달
    public event Action<int> OnExpChanged; // 현재 exp 전달 (경험치 바 UI용)
    
    // ══════════════════════════════════════════════
    //  상수
    // ══════════════════════════════════════════════
    private const int MAX_PLAYER_LEVEL = 999;
    private const int MAX_UPGRADE_LEVEL = 5000;
    
    // ══════════════════════════════════════════════
    //  강화 대상 열거형
    // ══════════════════════════════════════════════
    public enum StatType
    {
        Damage,
        CritChance,
        CritDamage,
        attackspd,
    }
    
    // ══════════════════════════════════════════════
    //  강화 설정 구조체  (인스펙터에서 조정 가능)
    // ══════════════════════════════════════════════
    public struct UpgradeConfig
    {
        [Tooltip("강화 Lv.0 -> Lv.1 기준 비용")] 
        public int baseCost;

        [Tooltip("강화 레벨 1 증가 시 추가 비용")]
        public int costPerLevel;

        [Tooltip("강화 1회당 고정 스탯 증가량")]
        public float gainPerUpgrade;
    }
    
    // ──────────────────────────────────────────────
    //  강화 설정값  (인스펙터에서 조정 가능)
    //
    //  최대체력  : 강화당 +10 HP      비용 50 → 레벨당 +10
    //  공격력   : 강화당 +2           비용 80 → 레벨당 +15
    //  방어력   : 강화당 +1           비용 70 → 레벨당 +12
    //  치명타확률: 강화당 +0.5%       비용 100 → 레벨당 +20
    //  치명타피해: 강화당 +0.1배율    비용 120 → 레벨당 +25
    // ──────────────────────────────────────────────
    [Header("스탯별 강화 설정")]
    [Header("스탯별 강화 설정")]
    [SerializeField] private UpgradeConfig damageConfig     = new UpgradeConfig { baseCost = 10, costPerLevel = 15, gainPerUpgrade = 5f   };
    [SerializeField] private UpgradeConfig attackspdConfig  = new UpgradeConfig { baseCost = 30, costPerLevel = 15, gainPerUpgrade = 5f   }; // ✅ gain 양수로 변경
    [SerializeField] private UpgradeConfig critChanceConfig = new UpgradeConfig { baseCost = 50, costPerLevel = 20, gainPerUpgrade = 0.5f };
    [SerializeField] private UpgradeConfig critDamageConfig = new UpgradeConfig { baseCost = 30, costPerLevel = 25, gainPerUpgrade = 0.1f };
    // ══════════════════════════════════════════════
    //  프로퍼티
    // ══════════════════════════════════════════════
    public int CurrentExp   => stat != null ? stat.Experience : 0;
    public int MaxExp       => stat != null ? stat.MaxExperience     : 100;
    public int CurrentLevel => stat != null ? stat.Level : 1;
    
    // ══════════════════════════════════════════════
    //  초기화
    // ══════════════════════════════════════════════
    /// <summary>
    /// 플레이어 스탯 참조를 설정합니다.
    /// 씬 전환 후 새 플레이어 오브젝트가 생성되어도 이전 수치를 복원합니다.
    /// </summary>
    public void Init(PlayerStat playerStat)
    {
        if (stat != null)
        {
            // 씬 전환 시 이전 데이터 복원
            playerStat.Level = stat.Level;
            playerStat.Experience = stat.Experience;
            playerStat.MaxExperience = stat.MaxExperience;
            
            // 강화 레벨 복원
            playerStat.UpgradeLevelDamage = stat.UpgradeLevelDamage;
            playerStat.UpgradeLevelAttackSpd = stat.UpgradeLevelAttackSpd;
            playerStat.UpgradeLevelCritChance = stat.UpgradeLevelCritChance;
            playerStat.UpgradeLevelCritDamage = stat.UpgradeLevelCritDamage;
            
            // 전투 스탯 복원
            playerStat.baseDamage = stat.baseDamage;
            playerStat.Critical =  stat.Critical;
            playerStat.CriticalMultiplier = stat.CriticalMultiplier;
            
        }
        stat = playerStat;
    }
    public void ResetStat()
    {
        stat = null;
    }
    
    // ══════════════════════════════════════════════
    //  [3] 스탯 강화  (Currency 소비)
    // ══════════════════════════════════════════════

    /// <summary>
    /// 다음 강화 비용을 반환합니다. (UI 표시용)
    ///
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

    /// <summary>
    /// Currency를 소비해 스탯을 1단계 강화합니다.
    /// </summary>
    /// <returns>강화 성공 여부</returns>
    public bool TryUpgrade(StatType type)
    {
        if (stat == null) { Debug.LogError("[LevelUpManager] stat이 null입니다."); return false; }

        int currentLv = GetUpgradeLevelValue(type);
        if (currentLv >= MAX_UPGRADE_LEVEL) return false;

        var (config, _) = GetConfigAndLevel(type);
        int cost = CalculateCost(config, currentLv);

        // 변경: stat.Currency 직접 차감 → CurrencyManager 위임
        if (!CurrencyManager.Instance.SpendGold(cost)) return false;

        ApplyGain(type, config.gainPerUpgrade);
        SetUpgradeLevelValue(type, currentLv + 1);
        OnStatUpgraded?.Invoke(type);

        // OnCurrencyChanged 제거 — CurrencyManager.SpendGold()에서 UI 자동 갱신
        Debug.Log($"[LevelUpManager] {type} 강화 Lv.{currentLv + 1} | 비용 {cost}");
        return true;
    }

    /// <summary>
    /// 최대 times 횟수만큼 반복 강화합니다. Currency가 부족하면 중단합니다.
    /// UI의 ×10 / ×100 버튼 등에 활용하세요.
    /// </summary>
    /// <returns>실제 강화 성공 횟수</returns>
    public int TryUpgradeMultiple(StatType type, int times)
    {
        int successCount = 0;
        for (int i = 0; i < times; i++)
        {
            if (!TryUpgrade(type)) break;
            successCount++;
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

            case StatType.attackspd:
                // ✅ AttackSpd(ms) 감소 → 쿨다운 감소 → 공격속도 증가
                // 최소 100ms(0.1초) 아래로 내려가지 않도록 클램프
                stat.AttackSpd = Mathf.Max(stat.AttackSpd - Mathf.RoundToInt(gain), 100);
                Debug.Log($"[LevelUpManager] AttackSpd: {stat.AttackSpd}ms → cooldown: {stat.AttackSpd / 1000f:F3}초");
                break;
        }
    }
    /// <summary>Enemy/Boss 사망 시 호출. 경험치 지급 + 레벨업 처리.</summary>
    public void AddExp(int amount)
    {
        if (stat == null) return;

        stat.Experience += amount;
        OnExpChanged?.Invoke(stat.Experience);

        // 레벨업 (초과 경험치 이월)
        while (stat.Experience >= stat.MaxExperience)
        {
            stat.Experience -= stat.MaxExperience;
            stat.Level++;
            stat.MaxExperience = CalculateMaxExp(stat.Level); // 레벨별 필요 경험치 계산
            OnLevelUp?.Invoke(stat.Level);
            Debug.Log($"[LevelUpManager] 레벨업! Lv.{stat.Level} | 다음 레벨까지 {stat.MaxExperience} exp");
        }

        Debug.Log($"[LevelUpManager] 경험치 +{amount} | {stat.Experience}/{stat.MaxExperience}");
    }

    /// <summary>레벨에 따른 필요 경험치 공식 (인스펙터 설정으로 교체 가능)</summary>
    private int CalculateMaxExp(int level)
    {
        // 예: 100 → 150 → 225... (1.5배 증가)
        return Mathf.RoundToInt(100 * Mathf.Pow(1.5f, level - 1));
    }
    // StatType → (UpgradeConfig, 현재 강화 레벨)
    private (UpgradeConfig config, int level) GetConfigAndLevel(StatType type)
    {
        return type switch
        {
            StatType.Damage     => (damageConfig,     stat.UpgradeLevelDamage),
            StatType.CritChance => (critChanceConfig, stat.UpgradeLevelCritChance),
            StatType.CritDamage => (critDamageConfig, stat.UpgradeLevelCritDamage),
            StatType.attackspd => (attackspdConfig, stat.UpgradeLevelAttackSpd),
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
            StatType.attackspd => stat.UpgradeLevelAttackSpd,
            _                   => 0
        };
    }

    private void SetUpgradeLevelValue(StatType type, int value)
    {
        switch (type)
        {
            case StatType.Damage:     stat.UpgradeLevelDamage      = value; break;
            case StatType.CritChance: stat.UpgradeLevelCritChance  = value; break;
            case StatType.CritDamage: stat.UpgradeLevelCritDamage  = value; break;
            case StatType.attackspd: stat.UpgradeLevelAttackSpd = value; break;
        }
    }
    
}
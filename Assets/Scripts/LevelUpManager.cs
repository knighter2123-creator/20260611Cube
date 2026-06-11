using System;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// 플레이어의 레벨/경험치 관리 + Currency 기반 스탯 강화를 통합 담당합니다.
///
/// ┌─── 흐름 ────────────────────────────────────────────────────┐
/// │  Enemy 처치                                                  │
/// │    └─ AddExperience()  → 레벨업 (스탯 직접 변경 없음)       │
/// │    └─ AddCurrency()    → Currency 획득 (Enemy 드랍)          │
/// │                                                              │
/// │  강화 UI                                                     │
/// │    └─ TryUpgrade()     → Currency 소비 → 스탯 상승           │
/// └──────────────────────────────────────────────────────────────┘
///
/// ┌─── 강화 공식 (선형) ─────────────────────────────────────────┐
/// │  비용   : baseCost + (costPerLevel × 현재강화레벨)           │
/// │  증가량 : 강화마다 고정 (gainPerUpgrade)                     │
/// └──────────────────────────────────────────────────────────────┘
/// </summary>
public class LevelUpManager : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  싱글턴
    // ──────────────────────────────────────────────
    public static LevelUpManager Instance { get; private set; }

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

    /// <summary>Currency 변경 시 현재 Currency를 전달합니다.</summary>
    public event Action<int> OnCurrencyChanged;
    
    // ══════════════════════════════════════════════
    //  상수
    // ══════════════════════════════════════════════
    private const int MAX_UPGRADE_LEVEL = 5000;
    
    // ══════════════════════════════════════════════
    //  강화 대상 열거형
    // ══════════════════════════════════════════════
    public enum StatType
    {
        Damage,
        CritChance,
        CritDamage
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
    [SerializeField] private UpgradeConfig damageConfig      = new UpgradeConfig { baseCost = 80,  costPerLevel = 15,  gainPerUpgrade = 2f   };
    [SerializeField] private UpgradeConfig critChanceConfig  = new UpgradeConfig { baseCost = 100, costPerLevel = 20,  gainPerUpgrade = 0.5f };
    [SerializeField] private UpgradeConfig critDamageConfig  = new UpgradeConfig { baseCost = 120, costPerLevel = 25,  gainPerUpgrade = 0.1f };
   
    // ══════════════════════════════════════════════
    //  프로퍼티
    // ══════════════════════════════════════════════
    public int CurrentLevel => stat != null ? stat.Level : 1;
    public int CurrentCurrency => stat != null ? stat.Currency : 0;
    
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
            playerStat.Currency = stat.Currency;
            
            // 강화 레벨 복원
            playerStat.UpgradeLevelMaxHealth = stat.UpgradeLevelMaxHealth;
            playerStat.UpgradeLevelDamage = stat.UpgradeLevelDamage;
            playerStat.UpgradeLevelDefence = stat.UpgradeLevelDefence;
            playerStat.UpgradeLevelCritChance = stat.UpgradeLevelCritChance;
            playerStat.UpgradeLevelCritDamage = stat.UpgradeLevelCritDamage;
            
            // 전투 스탯 복원
            playerStat.baseDamage = stat.baseDamage;
            playerStat.Critical =  stat.Critical;
            playerStat.CriticalMultiplier = stat.CriticalMultiplier;
            
        }
        stat = playerStat;
    }
   
  
    // ══════════════════════════════════════════════
    //  [2] Currency  (Enemy 드랍으로만 획득)
    // ══════════════════════════════════════════════
    /// <summary>
    /// Enemy 처치 시 Currency를 드랍합니다.
    /// Enemy 스크립트에서 OnDeath 등 사망 이벤트 시 호출하세요.
    /// </summary>

    public void AddCurrency(float chance = 100f)
    {
        if (stat == null) return;
        
        // 드랍 확률 판정
        if (UnityEngine.Random.Range(0f, 100f) > chance)
        {
            Debug.Log("[LevelUpMager] Currency 드랍 실패 (확률 미정)");
            return;
        }

        int amount = 100;
        stat.Currency += amount;
        
        OnCurrencyChanged?.Invoke(stat.Currency);
        Debug.Log($"[LevelUpManager] 골드 + {amount} (합계 : {stat.Currency})");
    }
    /// <summary>고정 Currency를 즉시 지급합니다. (퀘스트·디버그용)</summary>
    public void AddCurrencyFixed(int amount)
    {
        if (stat == null) return;
        stat.Currency += amount;
        OnCurrencyChanged?.Invoke(stat.Currency);
        Debug.Log($"[LevelUpManager] 골드 +{amount} (합계 : {stat.Currency})");
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
        if (stat == null)
        {
            Debug.LogError("[LevelManager] Stat이 null 입니다.");
            return false;
        }

        int currentLv = GetUpgradeLevelValue(type);
        if (currentLv >= MAX_UPGRADE_LEVEL)
        {
            Debug.Log($"[LevelManager] {type} 강화가 최대 레벨 {MAX_UPGRADE_LEVEL}에 도달했습니다.");
            return false;
        }
        var (config, _) = GetConfigAndLevel(type);
        int cost = CalculateCost(config, currentLv);

        if (stat.Currency < cost)
        {
            Debug.Log($"[LevelUpManager] 골드 부족, 필요 {cost}, 보유 : {stat.Currency}");
            return false;
        }
        
        // 비용 차감
        stat.Currency -= cost;
        
        // 스탯 증가 (고정 증가량)
        ApplyGain(type, config.gainPerUpgrade);
        
        // 강화 레벨 증가
        SetUpgradeLevelValue(type, currentLv + 1);
        
        OnStatUpgraded?.Invoke(type);
        OnCurrencyChanged?.Invoke(stat.Currency);
        
        Debug.Log($"[LevelUpManager] {type} 강화 Lv.{currentLv + 1} | 스탯 상승 값 {config.gainPerUpgrade} | 필요 재화 {cost} | 남은 재화 {stat.Currency}");
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
                stat.Critical = Mathf.Min(stat.Critical + gain, 100f); // 최대 100%
                break;
            case StatType.CritDamage:
                stat.CriticalMultiplier += gain;
                break;
        }
    }
    // StatType → (UpgradeConfig, 현재 강화 레벨)
    private (UpgradeConfig config, int level) GetConfigAndLevel(StatType type)
    {
        return type switch
        {
            StatType.Damage     => (damageConfig,     stat.UpgradeLevelDamage),
            StatType.CritChance => (critChanceConfig, stat.UpgradeLevelCritChance),
            StatType.CritDamage => (critDamageConfig, stat.UpgradeLevelCritDamage),
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
        }
    }
    
}
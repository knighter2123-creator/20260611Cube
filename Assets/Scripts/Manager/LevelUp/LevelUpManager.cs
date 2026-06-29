using System;
using UnityEngine;
using Random = System.Random;


partial class LevelUpManager : MonoBehaviour
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
    
    public event Action<int> OnLevelUp; // 레벨업 시 새 레벨 전달
    public event Action<int> OnExpChanged; // 현재 exp 전달 (경험치 바 UI용)
    
    // ══════════════════════════════════════════════
    //  상수
    // ══════════════════════════════════════════════
    private const int MAX_PLAYER_LEVEL = 999;
  
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
            // (기존 씬 전환 복원 로직 — 그대로 유지)
            playerStat.Level = stat.Level;
            playerStat.Experience = stat.Experience;
            // ... 나머지 복원 ...
            stat = playerStat;
        }
        else
        {
            stat = playerStat;
            // 이번 세션 첫 초기화 → 세이브가 있으면 적용
            if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
                ApplyFrom(SaveManager.Instance.Current);
        }
    }
    public void ResetStat()
    {
        stat = null;
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
        }
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
            StatType.Attackspd => (attackspdConfig, stat.UpgradeLevelAttackSpd),
            _                   => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

 
    
}
using System;
using UnityEngine;

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
        
    }
    // ══════════════════════════════════════════════
    //  이벤트
    // ══════════════════════════════════════════════
    
    public event Action<int> OnLevelUp; // 레벨업 시 새 레벨 전달
    public event Action<long> OnExpChanged; // 현재 exp 전달 (경험치 바 UI용)
    
    // ══════════════════════════════════════════════
    //  상수
    // ══════════════════════════════════════════════
    private const int MAX_PLAYER_LEVEL = 999;
  
    // ══════════════════════════════════════════════
    //  프로퍼티
    // ══════════════════════════════════════════════
    public long CurrentExp => stat != null ? stat.Experience : 0;
    public long MaxExp     => stat != null ? stat.MaxExperience : 100;
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
            // 씬 전환: 메모리의 옛 stat(최신 강화 반영)을 새 PlayerStat에 그대로 이전
            playerStat.Level         = stat.Level;
            playerStat.Experience    = stat.Experience;
            playerStat.MaxExperience = stat.MaxExperience;

            // ★ 강화 레벨 복원 (누락분)
            playerStat.UpgradeLevelDamage     = stat.UpgradeLevelDamage;
            playerStat.UpgradeLevelAttackSpd  = stat.UpgradeLevelAttackSpd;
            playerStat.UpgradeLevelCritChance = stat.UpgradeLevelCritChance;
            playerStat.UpgradeLevelCritDamage = stat.UpgradeLevelCritDamage;

            // ★ 강화로 누적된 실제 전투 스탯 복원 (누락분 — 이게 빠져서 dmg가 리셋됐음)
            playerStat.baseDamage          = stat.baseDamage;
            playerStat.Critical            = stat.Critical;
            playerStat.CriticalMultiplier  = stat.CriticalMultiplier;
            playerStat.AttackSpd           = stat.AttackSpd;

            stat = playerStat;

            OnLevelUp?.Invoke(stat.Level);
            OnExpChanged?.Invoke(stat.Experience);
        }
        else
        {
            stat = playerStat;
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
    private long CalculateMaxExp(int level)
    {
        // 100 → 150 → 225 ... (1.5배 증가)
        double value = 100.0 * System.Math.Pow(1.15, level - 1);
        return (long)System.Math.Max(1.0, System.Math.Round(value)); // 0 방지 가드
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
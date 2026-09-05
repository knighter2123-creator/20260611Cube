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

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════
    //  이벤트
    // ══════════════════════════════════════════════

    /// <summary>진짜로 레벨이 올랐을 때만 발생. 연출은 이 이벤트에 연결하세요.</summary>
    public event Action<int> OnLevelUp;

    /// <summary>현재 exp 전달 (경험치 바 UI용)</summary>
    public event Action<long> OnExpChanged;

    /// <summary>
    /// 씬 전환이나 세이브 로드로 스탯이 "복원"됐을 때 발생. 레벨업이 아니므로 연출을 재생하면 안 됩니다.
    /// UI 갱신 / 퀘스트 진행도 동기화 용도로만 쓰세요.
    /// </summary>
    public event Action<int> OnStatRestored;

    // ══════════════════════════════════════════════
    //  상수
    // ══════════════════════════════════════════════
    private const int MAX_PLAYER_LEVEL = 999;

    // ══════════════════════════════════════════════
    //  프로퍼티
    // ══════════════════════════════════════════════
    public long CurrentExp   => stat != null ? stat.Experience    : 0;
    public long MaxExp       => stat != null ? stat.MaxExperience : 100;
    public int  CurrentLevel => stat != null ? stat.Level         : 1;

    /// <summary>
    /// PlayerStat이 주입되어 강화/경험치 API를 쓸 수 있는 상태인가.
    /// ★ Instance는 있는데 stat이 null인 구간이 존재합니다. UI는 이걸 봐야
    ///   "비용 0 / 레벨 0" 같은 거짓 정보를 표시하지 않습니다.
    /// </summary>
    public bool IsReady => stat != null;

    // ══════════════════════════════════════════════
    //  초기화
    // ══════════════════════════════════════════════
    /// <summary>
    /// 플레이어 스탯 참조를 설정합니다.
    /// 씬 전환 후 새 플레이어 오브젝트가 생성되어도 이전 수치를 복원합니다.
    /// </summary>
    public void Init(PlayerStat playerStat)
    {
        if (playerStat == null)
        {
            Debug.LogError("[LevelUpManager] Init에 null PlayerStat이 들어왔습니다.");
            return;
        }

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

            // ★ 여기서 OnLevelUp 을 쏘면 "씬만 바꿔도 레벨업 연출이 터집니다".
            //   복원은 레벨업이 아니므로 전용 이벤트로 분리했습니다.
            OnStatRestored?.Invoke(stat.Level);
            OnExpChanged?.Invoke(stat.Experience);
        }
        else
        {
            stat = playerStat;
            if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
                ApplyFrom(SaveManager.Instance.Current);
            else
            {
                // 세이브가 없어도 UI는 초기값으로 한 번 갱신돼야 합니다
                OnStatRestored?.Invoke(stat.Level);
                OnExpChanged?.Invoke(stat.Experience);
            }
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
        while (stat.Experience >= stat.MaxExperience && stat.Level < MAX_PLAYER_LEVEL)
        {
            stat.Experience -= stat.MaxExperience;
            stat.Level++;
            stat.MaxExperience = CalculateMaxExp(stat.Level); // 레벨별 필요 경험치 계산

            OnLevelUp?.Invoke(stat.Level);
            OnExpChanged?.Invoke(stat.Experience);
        }
    }

    /// <summary>레벨에 따른 필요 경험치 공식 (인스펙터 설정으로 교체 가능)</summary>
    private long CalculateMaxExp(int level)
    {
        // 100 → 115 → 132 ... (1.15배 증가)
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
            StatType.Attackspd  => (attackspdConfig,  stat.UpgradeLevelAttackSpd),
            _                   => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
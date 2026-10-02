using System;
using UnityEngine;

/// <summary>
/// 플레이어 레벨 · 경험치 · 스탯 강화의 중앙 관리자.
///
/// partial 로 네 파일에 나뉘어 있습니다.
///   LevelUpManager.cs           ← 지금 이 파일. 싱글턴 / 이벤트 / 초기화 / 경험치
///   LevelUpManager.stat.cs      ← 스탯 강화 (비용 · 레벨 → 수치 공식)
///   LevelUpManager.MultiCost.cs ← N회 누적 비용
///   LevelUpManager.Save.cs      ← 세이브 연동
/// </summary>
public partial class LevelUpManager : MonoBehaviour
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
    /// Instance는 있는데 stat이 null인 구간이 존재합니다. UI는 이걸 봐야
    /// "비용 0 / 레벨 0" 같은 거짓 정보를 표시하지 않습니다.
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
            CopyProgress(stat, playerStat);
            stat = playerStat;
            NotifyRestored();
            return;
        }

        stat = playerStat;
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
            ApplyFrom(SaveManager.Instance.Current);   // 안에서 NotifyRestored
        else
            NotifyRestored();                          // 세이브가 없어도 UI는 초기값으로 한 번 갱신돼야 합니다
    }

    /// <summary>
    /// 진행도(레벨 · 경험치 · 강화 레벨 · 강화로 오른 전투 스탯)를 통째로 옮깁니다.
    /// 필드 목록을 한 곳에만 두어야, 스탯을 추가했을 때 씬 전환에서 하나만 빠지는 일이 없습니다.
    /// </summary>
    private static void CopyProgress(PlayerStat from, PlayerStat to)
    {
        to.Level         = from.Level;
        to.Experience    = from.Experience;
        to.MaxExperience = from.MaxExperience;

        to.UpgradeLevelDamage     = from.UpgradeLevelDamage;
        to.UpgradeLevelAttackSpd  = from.UpgradeLevelAttackSpd;
        to.UpgradeLevelCritChance = from.UpgradeLevelCritChance;
        to.UpgradeLevelCritDamage = from.UpgradeLevelCritDamage;

        to.baseDamage         = from.baseDamage;
        to.Critical           = from.Critical;
        to.CriticalMultiplier = from.CriticalMultiplier;
        to.AttackSpd          = from.AttackSpd;
    }

    /// <summary>
    /// 복원 알림. 여기서 OnLevelUp 을 쏘면 "씬만 바꿔도 / 세이브만 불러와도 레벨업 연출이 터집니다".
    /// 복원은 레벨업이 아니므로 전용 이벤트로 알립니다.
    /// </summary>
    private void NotifyRestored()
    {
        OnStatRestored?.Invoke(stat.Level);
        OnExpChanged?.Invoke(stat.Experience);
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
            stat.MaxExperience = CalculateMaxExp(stat.Level);

            OnLevelUp?.Invoke(stat.Level);
            OnExpChanged?.Invoke(stat.Experience);
        }
    }

    /// <summary>레벨에 따른 필요 경험치. 100 → 115 → 132 ... (1.15배 증가)</summary>
    private static long CalculateMaxExp(int level)
    {
        double value = 100.0 * Math.Pow(1.15, level - 1);
        return (long)Math.Max(1.0, Math.Round(value)); // 0 방지 가드
    }
}

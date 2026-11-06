using System;
using UnityEngine;

/// <summary>플레이어 레벨·경험치·스탯 강화 중앙 관리자 — 이 파일은 싱글턴·이벤트·PlayerStat 주입(씬이 바뀌면 진행도를 새 PlayerStat 으로 이전).</summary>
// partial 구성: .Exp(경험치·레벨업) .stat(강화 설정·비용·결제) .Formula(레벨→수치 공식) .MultiCost(N회 누적 비용) .Save .Editor
public partial class LevelUpManager : MonoBehaviour
{
    // ── 싱글턴 ──
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

    // ── 이벤트 ──

    /// <summary>진짜로 레벨이 올랐을 때만 발생. 연출은 이 이벤트에 연결하세요.</summary>
    public event Action<int> OnLevelUp;

    /// <summary>현재 exp 전달 (경험치 바 UI용)</summary>
    public event Action<long> OnExpChanged;

    /// <summary>씬 전환·세이브 로드로 스탯이 '복원'됐을 때 — 레벨업이 아니므로 연출 금지, UI·퀘스트 동기화용.</summary>
    public event Action<int> OnStatRestored;

    // ── 프로퍼티 ──
    public long CurrentExp   => stat?.Experience    ?? 0;
    public long MaxExp       => stat?.MaxExperience ?? 100;
    public int  CurrentLevel => stat?.Level         ?? 1;

    /// <summary>PlayerStat 이 주입돼 API 를 쓸 수 있는가 — Instance 는 있는데 stat 이 null 인 구간이 있어, UI 가 '비용 0' 같은 거짓 정보를 안 띄우게.</summary>
    public bool IsReady => stat != null;

    // ── 초기화 ──
    /// <summary>플레이어 스탯 참조 설정 — 씬 전환으로 새 플레이어가 생겨도 이전 수치를 옮김.</summary>
    public void Init(PlayerStat playerStat)
    {
        if (playerStat == null)
        {
            Debug.LogError("[LevelUpManager] Init에 null PlayerStat이 들어왔습니다.");
            return;
        }

        if (IsReady)
        {
            // 씬 전환: 메모리의 옛 stat(최신 강화 반영)을 새 PlayerStat에 그대로 이전
            CopyProgress(stat, playerStat);
            stat = playerStat;
            NotifyRestored();
            return;
        }

        stat = playerStat;
        SaveManager sm = SaveManager.Instance;
        if (sm != null && sm.HasSave())
            ApplyFrom(sm.Current);   // 안에서 NotifyRestored
        else
            NotifyRestored();        // 세이브가 없어도 UI는 초기값으로 한 번 갱신돼야 합니다
    }

    /// <summary>진행도(레벨·경험치·강화 레벨·전투 스탯)를 통째로 옮김 — 필드 목록을 한 곳에만 둬서 스탯 추가 때 누락 방지.</summary>
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

    /// <summary>복원 알림 — 전용 이벤트로 (OnLevelUp 을 쏘면 씬만 바꿔도 레벨업 연출이 터짐).</summary>
    private void NotifyRestored()
    {
        OnStatRestored?.Invoke(stat.Level);
        OnExpChanged?.Invoke(stat.Experience);
    }

    public void ResetStat() => stat = null;
}

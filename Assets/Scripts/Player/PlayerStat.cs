using UnityEngine;

[System.Serializable]
public class PlayerStat
{   
    // ──────────────────────────────────────────────
    //  레벨 / 경험치
    // ──────────────────────────────────────────────
    [Header("레벨 / 경험치")]
    public int   Level         = 1;
    public long Experience        = 0;
    public long MaxExperience     = 100;
    
    // ──────────────────────────────────────────────
    //  공격 범위
    // ──────────────────────────────────────────────
    [Header("기본 공격 스탯")]
    public float attackRange        = 500f;
    public float attackCooldown => AttackSpd / 1000f; // 300ms → 0.3초
    
    // ──────────────────────────────────────────────
    //  전투 스탯
    // ──────────────────────────────────────────────
    [Header("전투 스탯(강화로만 상승)")]
    public int   baseDamage          = 5;
    public float   AttackSpd           = 3000f;
    public float Critical            = 3f;
    public float CriticalMultiplier  = 1.5f;

    // ──────────────────────────────────────────────
    //  강화 레벨 (최대 5,000)
    // ──────────────────────────────────────────────
    [Header("강화 레벨(최대 5,000)")]
    public int UpgradeLevelDamage     = 0;
    public int UpgradeLevelAttackSpd  = 0;
    public int UpgradeLevelCritChance = 0;
    public int UpgradeLevelCritDamage = 0;

    // ──────────────────────────────────────────────
    //  초기화
    // ──────────────────────────────────────────────

    /// <summary>
    /// 게임 최초 시작 시에만 호출합니다.
    /// Currency, 강화레벨을 포함한 모든 수치를 초기화합니다.
    /// (Player.Start() → isFirstLoad == true 일 때만 호출)
    /// </summary>
    public void InitFull()
    {
        Level              = 1;
        Experience         = 0;
        MaxExperience      = 100;   // 누락
        attackRange        = 500f;  // 누락 — 이게 빠져서 10이 유지됨
        baseDamage         = 20;
        AttackSpd          = 3000;
        Critical           = 3f;    // 필드 초기화값과 통일 (기존 5f)
        CriticalMultiplier = 1.5f;  // 필드 초기화값과 통일 (기존 1.3f)

        UpgradeLevelDamage     = 0;
        UpgradeLevelAttackSpd  = 0;
        UpgradeLevelCritChance = 0;
        UpgradeLevelCritDamage = 0;
    }
}

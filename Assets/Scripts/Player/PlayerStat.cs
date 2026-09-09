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
    //  ★ 증강 반영 최종 스탯 (신규)
    // ──────────────────────────────────────────────
    //
    // [왜 필드가 아니라 프로퍼티(=>)인가 — 이번 작업의 핵심 학습 포인트]
    //
    // baseDamage 에 증강 배율을 직접 곱해서 저장하면 이런 문제가 생깁니다.
    //   · 증강을 하나 고를 때마다 baseDamage 가 커지고, 그 값이 세이브에 기록됨
    //   · 게임을 껐다 켜면 "저장된 커진 값" 위에 증강이 또 곱해짐 → 무한 인플레
    //   · 강화(UpgradeLevelDamage)로 오른 건지 증강으로 오른 건지 구분 불가
    //
    // 프로퍼티는 값을 저장하지 않고 "읽을 때마다 계산"합니다.
    // 그래서 baseDamage 는 순수한 강화 수치로만 남고, 증강은 항상 그 위에 얹힙니다.
    // 증강을 초기화하면 자동으로 원래 값으로 돌아옵니다.
    //
    // [System.Serializable] 클래스에서 프로퍼티는 직렬화되지 않으므로
    // 세이브 데이터 구조도 전혀 바뀌지 않습니다. 기존 세이브 그대로 호환됩니다.
    //
    // AugmentManager 가 씬에 없어도 Attack 은 1, CritDamage 는 0 이 나오므로
    // 증강 시스템을 꺼도 이 코드는 안전하게 동작합니다.

    /// <summary>증강 공격력 배율이 반영된 최종 공격력. 대미지 계산에는 이 값을 쓰세요.</summary>
    public float FinalDamage => baseDamage * AugmentManager.Attack;

    /// <summary>증강 치명타 대미지가 반영된 최종 치명타 배수.</summary>
    public float FinalCriticalMultiplier => CriticalMultiplier + AugmentManager.CritDamage;

    /// <summary>치명타 확률(%). 지금은 증강 대상이 아니지만, 나중에 카드를 추가하면 여기에 얹으면 됩니다.</summary>
    public float FinalCritical => Critical;

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
        AttackSpd          = 3000f;
        Critical           = 3f;    // 필드 초기화값과 통일 (기존 5f)
        CriticalMultiplier = 1.5f;  // 필드 초기화값과 통일 (기존 1.3f)

        UpgradeLevelDamage     = 0;
        UpgradeLevelAttackSpd  = 0;
        UpgradeLevelCritChance = 0;
        UpgradeLevelCritDamage = 0;
    }
}
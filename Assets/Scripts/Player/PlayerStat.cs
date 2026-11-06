using UnityEngine;

/// <summary>플레이어 수치와 그 출처: FinalDamage = baseDamage(기본+강화, 저장) × 증강 × 각성 버프 × 동료 시너지 — 배율은 이 한 곳에서만 곱하고 Final* 는 프로퍼티라 저장되지 않아 껐다 켜도 중복으로 곱해지지 않음.</summary>
[System.Serializable]
public class PlayerStat
{
    // ── 기본값 상수 — 단일 진실 공급원(single source of truth) ──
    public const int   BASE_DAMAGE       = 20;      // 순수 기본 공격력
    public const float BASE_ATTACK_SPD   = 3000f;   // 순수 기본 공격 주기 (ms)
    public const float BASE_CRITICAL     = 3f;      // 순수 기본 치명타 확률 (%)
    public const float BASE_CRIT_MULT    = 1.5f;    // 순수 기본 치명타 배수
    public const float BASE_ATTACK_RANGE = 500f;

    /// <summary>공격 주기 하한(ms) — 강화 곡선(AttackSpdForLevel)의 Lv.5000 값.</summary>
    public const float MIN_ATTACK_SPD = 100f;

    /// <summary>치명타 확률 상한(%) — 강화 공식(CritChanceForLevel)의 Lv.5000 값.</summary>
    public const float MAX_CRITICAL = 100f;

    /// <summary>공격 쿨타임 하한 (초). FinalAttackCooldown 이 이 값으로 하한을 겁니다.</summary>
    public const float MIN_ATTACK_COOLDOWN = MIN_ATTACK_SPD / 1000f;

    // ── 레벨 / 경험치 ──
    [Header("레벨 / 경험치")]
    public int  Level         = 1;
    public long Experience    = 0;
    public long MaxExperience = 100;

    // ── 공격 범위 ──
    [Header("기본 공격 스탯")]
    public float attackRange = BASE_ATTACK_RANGE;

    /// <summary>공격 1회 사이의 대기 시간(초, 하한 적용 전). 3000ms → 3.0초</summary>
    public float attackCooldown => AttackSpd / 1000f;

    // ── 전투 스탯 (강화로만 상승 · 세이브에 저장되는 값) ──
    [Header("전투 스탯(강화로만 상승)")]
    public int   baseDamage         = BASE_DAMAGE;
    public float AttackSpd          = BASE_ATTACK_SPD;   // ms. 낮을수록 빠름
    public float Critical           = BASE_CRITICAL;     // %
    public float CriticalMultiplier = BASE_CRIT_MULT;    // 배수

    // ── 강화 레벨 (최대 5,000) ──
    [Header("강화 레벨(최대 5,000)")]
    public int UpgradeLevelDamage     = 0;
    public int UpgradeLevelAttackSpd  = 0;
    public int UpgradeLevelCritChance = 0;
    public int UpgradeLevelCritDamage = 0;

    // ── 최종 스탯 (증강 + 각성 버프 반영) ──

    /// <summary>최종 공격력(모든 배율 포함) — 대미지 계산엔 이 값, 배율을 곱하는 곳은 여기 하나뿐.</summary>
    public float FinalDamage => DamageAfterAwaken * SynergyDamageMultiplier;

    /// <summary>증강 치명타 대미지가 반영된 최종 치명타 배수.</summary>
    public float FinalCriticalMultiplier => CriticalMultiplier + AugmentCritDamageBonus;

    /// <summary>치명타 확률(%). 상한 MAX_CRITICAL.</summary>
    public float FinalCritical => Mathf.Min(Critical + AugmentCritChanceBonus, MAX_CRITICAL);

    /// <summary>치명타가 터졌을 때 실제로 들어가는 대미지. 스탯창 "치명타 공격력" 표시용.</summary>
    public float FinalCriticalDamage => FinalDamage * FinalCriticalMultiplier;

    /// <summary>실제 공격 쿨타임(초) — 공격과 스탯창이 같은 값을 써서 표시와 실제가 어긋나지 않음.</summary>
    public float FinalAttackCooldown => Mathf.Max(attackCooldown, MIN_ATTACK_COOLDOWN);

    /// <summary>초당 공격 횟수. 스탯창 "공격 속도" 표시용.</summary>
    public float FinalAttacksPerSecond => 1f / FinalAttackCooldown;

    /// <summary>공격 주기가 하한(100ms)에 걸려 더 이상 빨라지지 않는 상태인가. 스탯창 경고 표시용.</summary>
    public bool IsAttackSpeedCapped => AttackSpd <= MIN_ATTACK_SPD;

    // ── 스탯 출처 분리(스탯창 세부 내역) — 기여분은 '현재 − 기본'으로 역산, 기본값 상수를 올려도 음수가 안 되게 Max(0) ──

    // ── 공격력: ① 기본 / ② 강화 ──
    /// <summary>강화로 올린 공격력 (기여분).</summary>
    public int UpgradeDamageBonus => Mathf.Max(0, baseDamage - BASE_DAMAGE);

    /// <summary>강화분을 뺀 순수 기본 공격력. (기본 + 강화 = baseDamage 가 항상 성립)</summary>
    public int PureBaseDamage => baseDamage - UpgradeDamageBonus;

    // ── 공격력: ③ 증강 ──
    /// <summary>증강 공격력 배율 (1 = 증강 없음).</summary>
    public float AugmentAttackMultiplier => AugmentManager.Attack;

    /// <summary>증강까지 적용한 중간값 — 레이어별 기여분은 전후 차이라 중간값을 한 곳에 둬서 공식 복사를 막음.</summary>
    public float DamageAfterAugment => baseDamage * AugmentAttackMultiplier;

    /// <summary>증강으로 늘어난 공격력 절대량 (Final − base 면 각성분이 섞여 중간값 기준).</summary>
    public float AugmentDamageBonus => DamageAfterAugment - baseDamage;

    // ── 공격력: ④ 각성 영구 버프 ──
    /// <summary>각성 클리어로 누적된 영구 대미지 배율(1 = 없음, 매니저가 없으면 1).</summary>
    public float PermanentDamageMultiplier =>
        PlayerBuffManager.Instance != null ? PlayerBuffManager.Instance.DamageMultiplier : 1f;

    /// <summary>각성까지 적용한 공격력(시너지 전 중간값). DamageAfterAugment 와 같은 이유로 둡니다.</summary>
    public float DamageAfterAwaken => DamageAfterAugment * PermanentDamageMultiplier;

    /// <summary>각성 버프로 늘어난 공격력 절대량 (Final 기준이면 시너지분까지 섞여 중간값 기준).</summary>
    public float AwakenDamageBonus => DamageAfterAwaken - DamageAfterAugment;

    // ── 공격력: ⑤ 동료 시너지 ──
    /// <summary>동료 시너지 공격력 배율(1 = 없음) — FinalDamage 를 통해 평타와 동료 스킬의 플레이어 몫 양쪽에 자동 적용.</summary>
    public float SynergyDamageMultiplier
        => Mathf.Max(0f, 1f + CompanionManager.SynergyValue(SynergyStat.PlayerDamage));

    /// <summary>시너지로 늘어난 공격력의 절대량.</summary>
    public float SynergyDamageBonus => FinalDamage - DamageAfterAwaken;

    // ── 치명타 대미지(배수) ──
    public float UpgradeCritDamageBonus => Mathf.Max(0f, CriticalMultiplier - BASE_CRIT_MULT);
    public float PureBaseCritMultiplier => CriticalMultiplier - UpgradeCritDamageBonus;
    public float AugmentCritDamageBonus => AugmentManager.CritDamage;

    // ── 치명타 확률 ──
    public float UpgradeCritChanceBonus => Mathf.Max(0f, Critical - BASE_CRITICAL);
    public float PureBaseCritical        => Critical - UpgradeCritChanceBonus;

    /// <summary>증강 치명타 확률(%p) — 카드가 없어 항상 0, 만들면 이 한 줄만 바꾸면 스탯창까지 반영.</summary>
    public float AugmentCritChanceBonus => 0f;

    // ── 공격 속도 (ms 가 줄어드는 방향이라 부호가 반대) ──
    /// <summary>강화로 줄인 공격 주기(ms). 값이 클수록 빠릅니다.</summary>
    public float UpgradeAttackSpdReduction => Mathf.Max(0f, BASE_ATTACK_SPD - AttackSpd);

    /// <summary>강화분을 되돌린 순수 기본 공격 주기(ms).</summary>
    public float PureBaseAttackSpd => AttackSpd + UpgradeAttackSpdReduction;

    /// <summary>증강으로 줄인 공격 주기(ms). 해당 카드가 아직 없어서 0.</summary>
    public float AugmentAttackSpdReduction => 0f;

    // ── 초기화 ──

    /// <summary>최초 시작(세이브 없음)에만 호출 — 모든 값을 const 기본값으로 (각성 버프는 PlayerBuffManager 가 따로 관리).</summary>
    public void InitFull()
    {
        Level         = 1;
        Experience    = 0;
        MaxExperience = 100;
        attackRange   = BASE_ATTACK_RANGE;

        baseDamage         = BASE_DAMAGE;
        AttackSpd          = BASE_ATTACK_SPD;
        Critical           = BASE_CRITICAL;
        CriticalMultiplier = BASE_CRIT_MULT;

        UpgradeLevelDamage     = 0;
        UpgradeLevelAttackSpd  = 0;
        UpgradeLevelCritChance = 0;
        UpgradeLevelCritDamage = 0;
    }
}

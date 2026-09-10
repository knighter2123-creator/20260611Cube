using UnityEngine;

/// <summary>
/// 플레이어의 모든 수치를 담는 데이터 덩어리.
///
/// ─────────────────────────────────────────────────────────────
/// [이번 작업에서 추가된 것 — 스탯 출처 분리]
///
/// 지금까지 이 클래스는 "최종 결과값"만 알고 있었습니다.
/// baseDamage 가 500 이라고 할 때, 그게 어디서 왔는지는 알 수 없었죠.
///   · 원래 기본값이 500인가?
///   · 기본 20에 강화로 480을 올린 건가?
///
/// 플레이어에게 스탯 창을 보여주려면 이 구분이 필요합니다.
/// 그래서 "기본값"을 const 로 못 박고, 나머지를 역산하는 프로퍼티를 추가했습니다.
///
///   기본값(const)  +  강화 기여분(계산)  =  baseDamage       ← 세이브에 저장되는 값
///                                        × 증강 배율        ← 매 프레임 계산
///                                        = FinalDamage      ← 실제 대미지
///
/// 여기서 중요한 원칙이 하나 있습니다 —
/// **합계가 항상 실제 값과 일치해야 한다.**
/// 그래서 "강화 기여분 = 현재값 − 기본값" 으로 역산합니다.
/// (강화레벨 × 회당증가량 으로 계산하는 방법도 있지만, 상한 클램프에 걸린 경우
///  합이 실제 값과 어긋납니다. 화면에 "20 + 500 = 480" 같은 게 뜨면 안 되죠.)
/// ─────────────────────────────────────────────────────────────
/// </summary>
[System.Serializable]
public class PlayerStat
{
    // ══════════════════════════════════════════════════════════
    //  기본값 상수 — 단일 진실 공급원(single source of truth)
    // ══════════════════════════════════════════════════════════
    //
    // [왜 const 로 빼는가]
    // 전에는 같은 숫자가 세 곳에 흩어져 있었습니다.
    //   ① 필드 초기화값 (baseDamage = 5)
    //   ② InitFull() 안의 값     (baseDamage = 20)   ← ①과 다릅니다! 실제 버그였습니다
    //   ③ 스탯창에 표시할 "기본값"
    //
    // 같은 의미의 숫자가 여러 곳에 있으면 반드시 하나는 어긋납니다.
    // 한 곳에만 두고 나머지가 그걸 참조하면, 밸런스 패치 때 한 줄만 고치면 됩니다.

    public const int   BASE_DAMAGE       = 20;      // 순수 기본 공격력
    public const float BASE_ATTACK_SPD   = 3000f;   // 순수 기본 공격 주기 (ms)
    public const float BASE_CRITICAL     = 3f;      // 순수 기본 치명타 확률 (%)
    public const float BASE_CRIT_MULT    = 1.5f;    // 순수 기본 치명타 배수
    public const float BASE_ATTACK_RANGE = 500f;

    /// <summary>공격 주기 하한 (ms). LevelUpManager.ApplyGain 의 클램프와 반드시 같은 값이어야 합니다.</summary>
    public const float MIN_ATTACK_SPD = 100f;

    /// <summary>치명타 확률 상한 (%). LevelUpManager.ApplyGain 의 클램프와 같은 값.</summary>
    public const float MAX_CRITICAL = 100f;

    /// <summary>공격 쿨타임 하한 (초). Player.HandleAttack 의 Mathf.Max 와 같은 값.</summary>
    public const float MIN_ATTACK_COOLDOWN = MIN_ATTACK_SPD / 1000f;

    // ══════════════════════════════════════════════════════════
    //  레벨 / 경험치
    // ══════════════════════════════════════════════════════════
    [Header("레벨 / 경험치")]
    public int  Level         = 1;
    public long Experience    = 0;
    public long MaxExperience = 100;

    // ══════════════════════════════════════════════════════════
    //  공격 범위
    // ══════════════════════════════════════════════════════════
    [Header("기본 공격 스탯")]
    public float attackRange = BASE_ATTACK_RANGE;

    /// <summary>공격 1회 사이의 대기 시간(초). 3000ms → 3.0초</summary>
    public float attackCooldown => AttackSpd / 1000f;

    // ══════════════════════════════════════════════════════════
    //  전투 스탯 (강화로만 상승 · 세이브에 저장되는 값)
    // ══════════════════════════════════════════════════════════
    [Header("전투 스탯(강화로만 상승)")]
    public int   baseDamage         = BASE_DAMAGE;
    public float AttackSpd          = BASE_ATTACK_SPD;   // ms. 낮을수록 빠름
    public float Critical           = BASE_CRITICAL;     // %
    public float CriticalMultiplier = BASE_CRIT_MULT;    // 배수

    // ══════════════════════════════════════════════════════════
    //  강화 레벨 (최대 5,000)
    // ══════════════════════════════════════════════════════════
    [Header("강화 레벨(최대 5,000)")]
    public int UpgradeLevelDamage     = 0;
    public int UpgradeLevelAttackSpd  = 0;
    public int UpgradeLevelCritChance = 0;
    public int UpgradeLevelCritDamage = 0;

    // ══════════════════════════════════════════════════════════
    //  ★ 증강 반영 최종 스탯
    // ══════════════════════════════════════════════════════════
    //
    // [왜 필드가 아니라 프로퍼티(=>)인가 — 지난 작업의 핵심 학습 포인트]
    //
    // baseDamage 에 증강 배율을 직접 곱해서 저장하면 이런 문제가 생깁니다.
    //   · 증강을 하나 고를 때마다 baseDamage 가 커지고, 그 값이 세이브에 기록됨
    //   · 게임을 껐다 켜면 "저장된 커진 값" 위에 증강이 또 곱해짐 → 무한 인플레
    //   · 강화로 오른 건지 증강으로 오른 건지 구분 불가
    //
    // 프로퍼티는 값을 저장하지 않고 "읽을 때마다 계산"합니다.
    // 그래서 baseDamage 는 순수한 강화 수치로만 남고, 증강은 항상 그 위에 얹힙니다.
    // [System.Serializable] 클래스에서 프로퍼티는 직렬화되지 않으므로 세이브 구조도 그대로입니다.

    /// <summary>증강 공격력 배율이 반영된 최종 공격력. 대미지 계산에는 이 값을 쓰세요.</summary>
    public float FinalDamage => baseDamage * AugmentManager.Attack;

    /// <summary>증강 치명타 대미지가 반영된 최종 치명타 배수.</summary>
    public float FinalCriticalMultiplier => CriticalMultiplier + AugmentManager.CritDamage;

    /// <summary>치명타 확률(%). 지금은 증강 대상이 아니지만, 카드를 추가하면 여기에 얹으면 됩니다.</summary>
    public float FinalCritical => Mathf.Min(Critical + AugmentCritChanceBonus, MAX_CRITICAL);

    /// <summary>치명타가 터졌을 때 실제로 들어가는 대미지. 스탯창 "치명타 공격력" 표시용.</summary>
    public float FinalCriticalDamage => FinalDamage * FinalCriticalMultiplier;

    /// <summary>
    /// 실제로 적용되는 공격 쿨타임(초).
    ///
    /// ★ Player.HandleAttack() 이 Mathf.Max(attackCooldown, 0.1f) 로 하한을 걸고 있습니다.
    ///   스탯창이 그 하한을 무시하고 계산하면 "화면엔 20회/초인데 실제론 10회/초"가 됩니다.
    ///   **화면에 보이는 수치와 실제 동작이 다른 것**은 가장 나쁜 종류의 버그입니다.
    ///   그래서 표시용 계산도 같은 하한을 통과시킵니다.
    /// </summary>
    public float FinalAttackCooldown => Mathf.Max(attackCooldown, MIN_ATTACK_COOLDOWN);

    /// <summary>초당 공격 횟수. 스탯창 "공격 속도" 표시용.</summary>
    public float FinalAttacksPerSecond => 1f / FinalAttackCooldown;

    /// <summary>공격 주기가 하한(100ms)에 걸려 더 이상 빨라지지 않는 상태인가. 스탯창 경고 표시용.</summary>
    public bool IsAttackSpeedCapped => AttackSpd <= MIN_ATTACK_SPD;

    // ══════════════════════════════════════════════════════════
    //  ★ 스탯 출처 분리 (신규) — 스탯창 세부 내역 표시용
    // ══════════════════════════════════════════════════════════
    //
    // 규칙: 기본 + 강화 = 저장된 값.  항상 정확히 맞습니다.
    // Mathf.Max 로 음수를 막는 이유는, 밸런스 패치로 기본값 상수를 올렸을 때
    // 기존 유저의 저장값이 새 기본값보다 작을 수 있기 때문입니다.
    // 그 경우 "기본 = 저장값, 강화 = 0" 으로 보이게 되어 합계는 여전히 맞습니다.

    // ── 공격력 ──
    /// <summary>강화로 올린 공격력 (기여분).</summary>
    public int UpgradeDamageBonus => Mathf.Max(0, baseDamage - BASE_DAMAGE);

    /// <summary>강화분을 뺀 순수 기본 공격력. (기본 + 강화 = baseDamage 가 항상 성립)</summary>
    public int PureBaseDamage => baseDamage - UpgradeDamageBonus;

    /// <summary>증강 공격력 배율 (1 = 증강 없음).</summary>
    public float AugmentAttackMultiplier => AugmentManager.Attack;

    /// <summary>증강 배율 때문에 늘어난 공격력의 절대량. "×1.5" 가 몇으로 보이는지 알려줍니다.</summary>
    public float AugmentDamageBonus => FinalDamage - baseDamage;

    // ── 치명타 대미지(배수) ──
    public float UpgradeCritDamageBonus => Mathf.Max(0f, CriticalMultiplier - BASE_CRIT_MULT);
    public float PureBaseCritMultiplier => CriticalMultiplier - UpgradeCritDamageBonus;
    public float AugmentCritDamageBonus => AugmentManager.CritDamage;

    // ── 치명타 확률 ──
    public float UpgradeCritChanceBonus => Mathf.Max(0f, Critical - BASE_CRITICAL);
    public float PureBaseCritical        => Critical - UpgradeCritChanceBonus;

    /// <summary>
    /// 증강으로 오른 치명타 확률(%p).
    /// 지금은 해당 증강 카드가 없어서 항상 0 입니다.
    /// 나중에 카드를 만들면 AugmentManager 에 CritChance static 접근자를 추가하고
    /// 이 한 줄만 바꾸면 스탯창이 자동으로 반영합니다.
    /// </summary>
    public float AugmentCritChanceBonus => 0f;

    // ── 공격 속도 ──
    // 공격 속도는 "ms 가 줄어드는" 방향이라 부호가 반대입니다. 헷갈리기 쉬운 지점입니다.
    /// <summary>강화로 줄인 공격 주기(ms). 값이 클수록 빠릅니다.</summary>
    public float UpgradeAttackSpdReduction => Mathf.Max(0f, BASE_ATTACK_SPD - AttackSpd);

    /// <summary>강화분을 되돌린 순수 기본 공격 주기(ms).</summary>
    public float PureBaseAttackSpd => AttackSpd + UpgradeAttackSpdReduction;

    /// <summary>증강으로 줄인 공격 주기(ms). 해당 카드가 아직 없어서 0.</summary>
    public float AugmentAttackSpdReduction => 0f;

    // ══════════════════════════════════════════════════════════
    //  초기화
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 게임 최초 시작 시에만 호출합니다. (Player.Start() → 세이브가 없을 때만)
    /// 모든 값이 위의 const 를 참조하므로, 밸런스를 바꿀 때 const 만 고치면 됩니다.
    /// </summary>
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
using UnityEngine;

/// <summary>
/// 플레이어의 모든 수치를 담는 데이터 덩어리.
///
/// ─────────────────────────────────────────────────────────────
/// [스탯 출처 분리]
///
/// 이 클래스는 "최종 결과값"만이 아니라 **그 값이 어디서 왔는지**를 알려줍니다.
///
///   기본값(const)  +  강화 기여분(계산)  =  baseDamage       ← 세이브에 저장되는 값
///                                        × 증강 배율        ← 매 프레임 계산
///                                        × 각성 영구 버프    ← ★ 이번에 추가
///                                        = FinalDamage      ← 실제 대미지
///
/// 중요한 원칙: **합계가 항상 실제 값과 일치해야 한다.**
/// 그래서 "강화 기여분 = 현재값 − 기본값" 으로 역산합니다.
/// ─────────────────────────────────────────────────────────────
///
/// ═══ ★ 이번 수정 — 각성 영구 버프를 레이어로 편입 ════════════════════
///
/// [무엇이 문제였나]
/// 각성 영구 버프(PlayerBuffManager.DamageMultiplier)는 지금까지
/// **Bullet.LaunchVolley() 안에서 발사 직전에만** 곱해졌습니다.
///
///     float buffMult = PlayerBuffManager.Instance?.DamageMultiplier ?? 1f;
///     float buffedDamage = stat.FinalDamage * buffMult;   // ← 여기서만
///
/// 그래서 FinalDamage 에는 버프가 없었고, 스탯창은 FinalDamage 를 보여주므로
/// **각성으로 공격력이 30% 올라도 스탯창 숫자가 전혀 움직이지 않았습니다.**
/// 실제 대미지만 조용히 1.3배가 되고 있었죠.
///
/// 이 클래스에 직접 적어두신 문장이 정확히 이 상황을 가리킵니다 —
/// **"화면에 보이는 수치와 실제 동작이 다른 것은 가장 나쁜 종류의 버그입니다."**
///
/// [어떻게 고쳤나]
/// 버프를 FinalDamage 안으로 들여오고, Bullet 쪽의 곱셈을 제거했습니다.
/// 곱하는 지점이 하나로 모이므로 이중 적용이 구조적으로 불가능해집니다.
///
/// [세이브에 영향이 없는 이유]
/// FinalDamage 는 필드가 아니라 **프로퍼티**입니다. 값을 저장하지 않고
/// 읽을 때마다 계산하죠. 세이브에 기록되는 건 baseDamage 하나뿐이고,
/// 버프는 SaveData.damageMultiplier 에 따로 저장됩니다.
/// 그래서 이 변경으로 저장 구조가 바뀌지 않고, 무한 인플레도 생기지 않습니다.
/// ══════════════════════════════════════════════════════════════════
/// </summary>
[System.Serializable]
public class PlayerStat
{
    // ══════════════════════════════════════════════════════════
    //  기본값 상수 — 단일 진실 공급원(single source of truth)
    // ══════════════════════════════════════════════════════════
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
    //  ★ 최종 스탯 (증강 + 각성 버프 반영)
    // ══════════════════════════════════════════════════════════
    //
    // [왜 필드가 아니라 프로퍼티(=>)인가 — 핵심 학습 포인트]
    //
    // baseDamage 에 배율을 직접 곱해서 저장하면 이런 문제가 생깁니다.
    //   · 증강/각성을 얻을 때마다 baseDamage 가 커지고, 그 값이 세이브에 기록됨
    //   · 게임을 껐다 켜면 "저장된 커진 값" 위에 또 곱해짐 → 무한 인플레
    //   · 강화로 오른 건지 배율로 오른 건지 구분 불가
    //
    // 프로퍼티는 값을 저장하지 않고 "읽을 때마다 계산"합니다.
    // [System.Serializable] 클래스에서 프로퍼티는 직렬화되지 않으므로 세이브 구조도 그대로입니다.

    /// <summary>
    /// 최종 공격력. 대미지 계산에는 이 값을 쓰세요.
    ///
    /// ★ 이제 각성 영구 버프까지 포함합니다.
    ///   Bullet 쪽에서 PlayerBuffManager 를 따로 곱하면 **이중 적용**이 되니
    ///   절대 다시 넣지 마세요. 곱하는 곳은 여기 한 군데뿐이어야 합니다.
    /// </summary>
    public float FinalDamage => DamageAfterAugment * PermanentDamageMultiplier;

    /// <summary>증강 치명타 대미지가 반영된 최종 치명타 배수.</summary>
    public float FinalCriticalMultiplier => CriticalMultiplier + AugmentManager.CritDamage;

    /// <summary>치명타 확률(%). 지금은 증강 대상이 아니지만, 카드를 추가하면 여기에 얹으면 됩니다.</summary>
    public float FinalCritical => Mathf.Min(Critical + AugmentCritChanceBonus, MAX_CRITICAL);

    /// <summary>
    /// 치명타가 터졌을 때 실제로 들어가는 대미지. 스탯창 "치명타 공격력" 표시용.
    /// FinalDamage 를 쓰므로 각성 버프가 자동으로 반영됩니다.
    /// </summary>
    public float FinalCriticalDamage => FinalDamage * FinalCriticalMultiplier;

    /// <summary>
    /// 실제로 적용되는 공격 쿨타임(초).
    ///
    /// ★ Player.HandleAttack() 이 Mathf.Max(attackCooldown, MIN_ATTACK_COOLDOWN) 로
    ///   하한을 걸고 있습니다. 표시용 계산도 같은 상수를 통과시켜야
    ///   "화면엔 20회/초인데 실제론 10회/초" 같은 일이 생기지 않습니다.
    /// </summary>
    public float FinalAttackCooldown => Mathf.Max(attackCooldown, MIN_ATTACK_COOLDOWN);

    /// <summary>초당 공격 횟수. 스탯창 "공격 속도" 표시용.</summary>
    public float FinalAttacksPerSecond => 1f / FinalAttackCooldown;

    /// <summary>공격 주기가 하한(100ms)에 걸려 더 이상 빨라지지 않는 상태인가. 스탯창 경고 표시용.</summary>
    public bool IsAttackSpeedCapped => AttackSpd <= MIN_ATTACK_SPD;

    // ══════════════════════════════════════════════════════════
    //  ★ 스탯 출처 분리 — 스탯창 세부 내역 표시용
    // ══════════════════════════════════════════════════════════
    //
    // 규칙: 기본 + 강화 = baseDamage.  그 위에 증강 배율, 다시 각성 배율.
    // Mathf.Max 로 음수를 막는 이유는, 밸런스 패치로 기본값 상수를 올렸을 때
    // 기존 유저의 저장값이 새 기본값보다 작을 수 있기 때문입니다.

    // ── 공격력: ① 기본 / ② 강화 ──
    /// <summary>강화로 올린 공격력 (기여분).</summary>
    public int UpgradeDamageBonus => Mathf.Max(0, baseDamage - BASE_DAMAGE);

    /// <summary>강화분을 뺀 순수 기본 공격력. (기본 + 강화 = baseDamage 가 항상 성립)</summary>
    public int PureBaseDamage => baseDamage - UpgradeDamageBonus;

    // ── 공격력: ③ 증강 ──
    /// <summary>증강 공격력 배율 (1 = 증강 없음).</summary>
    public float AugmentAttackMultiplier => AugmentManager.Attack;

    /// <summary>
    /// 증강까지만 적용한 공격력. 각성 버프는 아직 곱하지 않은 중간값입니다.
    ///
    /// ★ 이 중간값을 굳이 이름 붙여 꺼내는 이유
    ///   기여분을 계산하려면 "그 레이어를 지나기 전후"가 둘 다 필요합니다.
    ///   중간값이 없으면 각 기여분을 저마다 다시 곱해서 구하게 되고,
    ///   그러면 공식이 여러 곳에 복사돼 언젠가 하나가 어긋납니다.
    /// </summary>
    public float DamageAfterAugment => baseDamage * AugmentAttackMultiplier;

    /// <summary>
    /// 증강 배율 때문에 늘어난 공격력의 절대량. "×1.45" 가 몇으로 보이는지 알려줍니다.
    ///
    /// ★ [수정] 예전에는 FinalDamage − baseDamage 였습니다.
    ///   FinalDamage 에 각성 버프가 들어오면서, 그대로 두면 **각성분까지
    ///   증강 기여분으로 합산**되어 스탯창이 거짓말을 하게 됩니다.
    ///   레이어를 하나 끼워 넣을 때는 그 레이어를 쓰던 식들을 반드시 같이 봐야 합니다.
    /// </summary>
    public float AugmentDamageBonus => DamageAfterAugment - baseDamage;

    // ── 공격력: ④ 각성 영구 버프 ──
    /// <summary>
    /// 각성 스테이지 클리어로 누적된 영구 대미지 배율 (1 = 버프 없음).
    ///
    /// PlayerBuffManager 가 아직 없으면(씬 단독 테스트 등) 1을 돌려줍니다.
    /// 예전 Bullet 의 `?? 1f` 와 같은 역할이라 동작이 달라지지 않습니다.
    /// </summary>
    public float PermanentDamageMultiplier =>
        PlayerBuffManager.Instance != null ? PlayerBuffManager.Instance.DamageMultiplier : 1f;

    /// <summary>각성 영구 버프로 늘어난 공격력의 절대량.</summary>
    public float AwakenDamageBonus => FinalDamage - DamageAfterAugment;

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
    ///
    /// ※ 각성 영구 버프는 여기서 건드리지 않습니다. 그건 PlayerBuffManager 가
    ///   세이브에서 관리하는 별개의 값이고, 이 클래스는 읽기만 합니다.
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
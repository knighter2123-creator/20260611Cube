using UnityEngine;

/// <summary>
/// 모든 액티브 스킬의 베이스 클래스.
/// Execute() 호출 전 CalcDamage()로 플레이어 스탯 기반 최종 데미지와 크리티컬 여부를 계산합니다.
///
/// ★ [등급별 수치] 같은 스킬 에셋을 여러 동료가 같이 쓰므로, 에셋 값을 실행 중에 바꾸지 않습니다.
///   대신 '누가 쐈는지(caster)' 의 등급을 보고 그때그때 알맞은 값을 골라 씁니다.
///   → GetDamage(caster), GetCooldown(caster) — 등급과 성급(★)을 함께 반영. 각 스킬의 GetXxx(grade)
///
///   ✗ skill.damage = 70;              // 에셋이 바뀜 — 같은 스킬을 쓰는 일반 동료도 70 이 되고,
///                                     //   에디터에선 플레이를 멈춰도 값이 에셋에 남습니다
///   ✓ skill.GetDamage(caster)          // 읽기만 함
/// </summary>
public abstract class ActiveSkill : ScriptableObject
{
    [Header("스킬 기본 정보")]
    public string skillName = "스킬";

    // ★ 필드 이름을 그대로 두었습니다 (damage / cooldown). 이름을 바꾸면 기존 에셋 값이 사라집니다.
    [Tooltip("일반 등급 피해 (등급별 값을 끄면 모든 등급이 이 값)")]
    public float  damage    = 20f;
    [Tooltip("체크 시 희귀/영웅/전설 피해")]
    public GradeFloat damageByGrade = new GradeFloat();

    [Tooltip("일반 등급 재사용 대기(초)")]
    public float  cooldown  = 5f;
    [Tooltip("체크 시 희귀/영웅/전설 재사용 대기(초)")]
    public GradeFloat cooldownByGrade = new GradeFloat();

    // 성급(★)별 값. 체크하면 위의 등급별 값 대신 [등급, 성급] 칸을 씁니다 (CompanionStar 규칙).
    [Header("성급별 수치 (등급 × 성급)")]
    [Tooltip("체크 시 등급·성급별 피해. 처음 켜면 각 줄이 그 등급의 지금 피해로 채워집니다")]
    public GradeStarFloat damageByStar = new GradeStarFloat();
    [Tooltip("체크 시 등급·성급별 재사용 대기(초). 성급이 오를수록 작게 적으세요")]
    public GradeStarFloat cooldownByStar = new GradeStarFloat();

    [Tooltip("동료 머리 위 쿨다운 인디케이터에 표시할 아이콘 (없으면 원형만 표시)")]
    public Sprite icon;

    [Header("이펙트")]
    public GameObject effectPrefab;
    public float  effectDuration = 0.5f;

    // 도감 전용 문구. 스킬 에셋은 세이브 대상이 아니라 저장 구조와 무관합니다.
    [Header("도감 표시")]
    [Tooltip("도감 상세창에 추가로 보여줄 설명 (선택). 비워두면 효과 요약만 표시됩니다.")]
    [TextArea(2, 4)]
    public string description = "";

    /// <summary>
    /// 쿨다운 하한. 실수로 0 을 넣으면 Companion.Update 가 '매 프레임' 스킬을 쏘게 됩니다.
    /// 플레이어 공격 쿨타임 하한과 같은 상수를 써서 두 값이 따로 놀지 않게 합니다.
    /// </summary>
    public const float MIN_COOLDOWN = PlayerStat.MIN_ATTACK_COOLDOWN;

    public abstract void Execute(Enemy target, Companion caster);

    // ══════════════════════════════════════════════
    //  [등급별 수치] 조회
    // ══════════════════════════════════════════════

    /// <summary>시전자의 등급. 시전자가 없거나 데이터가 비어 있으면 일반으로 봅니다.</summary>
    public static CompanionGrade GradeOf(Companion caster)
    {
        // Companion(MonoBehaviour) 과 CompanionData(ScriptableObject) 는 UnityEngine.Object 라
        // '?.' 대신 '!= null' 로 검사합니다. ('?.' 는 파괴된 오브젝트를 null 로 보지 못함)
        if (caster == null || caster.Data == null) return CompanionGrade.Normal;
        return caster.Data.grade;
    }

    /// <summary>
    /// 등급표에서 값을 꺼내는 공용 도우미. 표가 null 이어도 안전하게 기본값을 돌려줍니다.
    /// (보통은 유니티가 직렬화하면서 채워 주지만, 코드로 CreateInstance 한 경우 등을 대비)
    /// </summary>
    protected static float ByGrade(GradeFloat table, float normalValue, CompanionGrade grade)
        => table != null ? table.Get(normalValue, grade) : normalValue;

    /// <summary>시전자의 성급. 시전자가 없거나 성급 정보를 읽을 수 없으면 1성으로 봅니다.</summary>
    public static int StarOf(Companion caster)
    {
        if (caster == null || caster.Data == null) return CompanionStar.MIN_STAR;
        CompanionFragment growth = CompanionFragment.Instance;
        return growth != null ? growth.GetStar(caster.Data) : CompanionStar.MIN_STAR;
    }

    // 성급 표 → (꺼져 있으면) 등급별 값 → (꺼져 있으면) 원래 숫자 순서로 찾습니다.
    private static float ByGradeStar(GradeStarFloat starTable, GradeFloat gradeTable, float normalValue,
                                     CompanionGrade grade, int star)
    {
        float byGrade = ByGrade(gradeTable, normalValue, grade);
        return starTable != null ? starTable.Get(byGrade, grade, star) : byGrade;
    }

    /// <summary>등급·성급에 맞는 스킬 피해. 전투와 도감이 같은 함수를 써서 화면과 실제가 어긋나지 않습니다.</summary>
    public float GetDamage(CompanionGrade grade, int star)
        => ByGradeStar(damageByStar, damageByGrade, damage, grade, star);

    /// <summary>등급·성급에 맞는 재사용 대기(초). 하한 MIN_COOLDOWN.</summary>
    public float GetCooldown(CompanionGrade grade, int star)
        => Mathf.Max(ByGradeStar(cooldownByStar, cooldownByGrade, cooldown, grade, star), MIN_COOLDOWN);

    /// <summary>이 시전자(등급·성급)의 스킬 피해.</summary>
    public float GetDamage(Companion caster) => GetDamage(GradeOf(caster), StarOf(caster));

    /// <summary>
    /// 이 시전자(등급·성급)의 재사용 대기 — ★ 시너지(재사용 대기 감소)까지 반영한 '실제' 값.
    /// Companion.Update 와 쿨다운 인디케이터가 둘 다 이 함수를 써서 게이지와 실제 발사가 어긋나지 않습니다.
    /// (도감은 배치와 무관한 기본값을 보여줘야 하므로 위의 GetCooldown(grade, star) 를 씁니다)
    /// </summary>
    public float GetCooldown(Companion caster)
        => Mathf.Max(GetCooldown(GradeOf(caster), StarOf(caster)) * SynergyCooldownFactor, MIN_COOLDOWN);

    // ══════════════════════════════════════════════
    //  [시너지] 보정 — CompanionManager.Synergy.cs 의 결과를 읽기만 합니다
    // ══════════════════════════════════════════════
    //
    // ★ 시너지는 팀 전체 보너스라 '누가 쐈는지' 와 무관하게 같은 값입니다.
    //   스킬 에셋 값은 여전히 건드리지 않습니다 — 읽어서 곱하기만 합니다.

    /// <summary>재사용 대기 감소 상한. 시너지를 겹쳐 100% 이상이 되면 쿨다운 0 → 매 프레임 발사가 되므로 막습니다.</summary>
    private const float MAX_COOLDOWN_REDUCTION = 0.9f;

    /// <summary>재사용 대기에 곱할 값 (0.1 감소 → 0.9).</summary>
    public static float SynergyCooldownFactor
        => 1f - Mathf.Min(CompanionManager.SynergyValue(SynergyStat.Cooldown), MAX_COOLDOWN_REDUCTION);

    /// <summary>스킬 최종 피해에 곱할 값 (+15% → 1.15). 음수 시너지가 생겨도 0 아래로는 내려가지 않습니다.</summary>
    public static float SynergyDamageFactor
        => Mathf.Max(0f, 1f + CompanionManager.SynergyValue(SynergyStat.SkillDamage));

    /// <summary>비율형 보정 — 지속시간 · 독 피해처럼 '곱하는' 값. (value × (1 + 시너지))</summary>
    protected static float WithSynergyScale(float value, SynergyStat stat)
        => value * Mathf.Max(0f, 1f + CompanionManager.SynergyValue(stat));

    /// <summary>%p 가산 보정 — 둔화율 · 방어력 감소율처럼 원래 0~1 비율인 값. 결과도 0~1 로 자릅니다.</summary>
    protected static float WithSynergyRate(float rate01, SynergyStat stat)
        => Mathf.Clamp01(rate01 + CompanionManager.SynergyValue(stat));

    // ══════════════════════════════════════════════
    //  [도감] 효과 요약
    // ══════════════════════════════════════════════

    /// <summary>
    /// 도감 상세창에 보여줄 '이 스킬만의 효과' 한 줄. 등급에 따라 수치가 다르므로 등급을 받습니다.
    ///
    /// ★ 왜 abstract 가 아니라 virtual 인가
    ///   abstract 로 만들면 모든 자식 클래스가 '반드시' 구현해야 해서,
    ///   나중에 스킬을 새로 만들 때 이걸 안 쓰면 컴파일이 안 됩니다.
    ///   virtual + 빈 기본값이면 안 써도 도감이 정상 동작하고(효과 줄만 비어 보임),
    ///   쓰고 싶은 스킬만 override 하면 됩니다.
    ///
    /// ★ 왜 도감 UI 가 아니라 스킬 쪽에 두나
    ///   도감에서 "if (skill is SkillSlow) ... else if (skill is SkillStun) ..." 로 분기하면
    ///   스킬을 추가할 때마다 도감 코드를 고쳐야 합니다. 빼먹어도 에러가 안 나고요.
    ///   "자기 설명은 자기가 한다" 로 두면 새 스킬 파일 하나에서 끝납니다.
    ///
    /// ※ 기본 피해 / 쿨다운은 모든 스킬 공통이라 도감이 따로 그립니다. 여기엔 넣지 마세요.
    /// </summary>
    public virtual string GetEffectSummary(CompanionGrade grade) => "";

    // 요약 문장에서 같이 쓰는 표기 도우미 — 숫자 표기 규칙을 한 곳에 둡니다.
    // "0.#" 은 소수 첫째 자리까지, 0 이면 생략 (0.4 → 40%, 0.125 → 12.5%)
    protected static string FormatPercent(float rate01) => $"{rate01 * 100f:0.#}%";
    protected static string FormatSeconds(float sec)    => $"{sec:0.#}초";

    // ══════════════════════════════════════════════
    //  사용
    // ══════════════════════════════════════════════

    /// <summary>
    /// 스킬 사용 진입점. Execute() 실행 후 쿨다운 UI를 자동으로 띄웁니다.
    /// 호출부에서 skill.Execute(target, this) 대신 skill.Cast(target, this) 를 쓰면
    /// 모든 스킬이 쿨다운 표시를 공짜로 얻습니다.
    /// </summary>
    public void Cast(Enemy target, Companion caster)
    {
        Execute(target, caster);
        NotifyCooldown(caster);
    }

    /// <summary>시전자 머리 위에 이 스킬의 쿨다운 게이지를 시작합니다.</summary>
    protected void NotifyCooldown(Companion caster)
    {
        if (caster == null) return;
        // ★ 게이지 길이도 '시전자 등급·성급의' 쿨다운이어야 실제 재사용 시간과 맞습니다.
        SkillCooldownIndicator.Begin(caster, icon, GetCooldown(caster));
    }

    /// <summary>
    /// 최종 피해 = (플레이어 최종 공격력 + 시전자 등급·성급의 스킬 피해) × (치명타면 최종 치명타 배율)
    /// </summary>
    protected (float finalDamage, bool isCritical) CalcDamage(Companion caster)
    {
        PlayerStat stat = StatOf(caster);
        bool crit = stat != null && Random.Range(0f, 100f) < stat.FinalCritical;
        return (WithPlayerAttack(caster, GetDamage(caster), crit), crit);
    }

    /// <summary>
    /// (플레이어 최종 공격력 + extra) 에 치명타면 최종 치명타 배율을 곱한 값.
    /// 즉발 피해와 독(DoT) 피해가 같은 공식을 씁니다.
    ///
    /// 플레이어 공격력은 PlayerStat 의 Final* 값을 씁니다 → 증강(공격력 · 치명타 대미지)과
    /// 각성 영구 버프가 플레이어 평타와 똑같이 스킬에도 반영됩니다. 버프는 '플레이어 공격력' 몫에만
    /// 곱해지고, 스킬 자체 피해(extra)는 등급별 고정값 그대로입니다.
    ///
    /// Player 가 없으면(씬 전환 중 등) Stat 이 null 이라 extra 만 돌려줍니다 (NullReferenceException 방지).
    ///
    /// ★ [시너지] 스킬 피해 보너스는 여기서 '최종 값 전체' 에 곱합니다.
    ///   스킬 자체 피해(extra)에만 곱하면 플레이어 공격력이 큰 후반에는 +40% 가 거의 티가 나지 않습니다.
    ///   즉발 피해(CalcDamage)와 독 피해(SkillPoison)가 모두 이 함수를 지나므로 둘 다 같은 보너스를 받습니다.
    /// </summary>
    protected static float WithPlayerAttack(Companion caster, float extra, bool isCritical)
    {
        PlayerStat stat = StatOf(caster);
        if (stat == null) return extra * SynergyDamageFactor;

        float value = (stat.FinalDamage + extra) * SynergyDamageFactor;
        return isCritical ? value * stat.FinalCriticalMultiplier : value;
    }

    private static PlayerStat StatOf(Companion caster) => caster != null ? caster.Stat : null;

    protected void PlayEffect(Vector3 position)
    {
        if (effectPrefab == null) return;

        // ★ [프레임 드랍 수정] 시전마다 Instantiate/Destroy 하지 않고 풀에서 꺼내 씁니다 (PooledEffect 참고).
        ObjectPoolManager pool = ObjectPoolManager.Instance;
        if (pool == null)
        {
            GameObject once = Instantiate(effectPrefab, position, Quaternion.identity);
            SetEffectSorting(once);
            Destroy(once, effectDuration);
            return;
        }

        GameObject fx = pool.GetInactive(effectPrefab, position, Quaternion.identity);
        if (fx == null) return;

        SetEffectSorting(fx);
        fx.SetActive(true);
        PooledEffect.ReturnAfter(fx, effectDuration);
    }

    private static void SetEffectSorting(GameObject fx)
    {
        if (fx.TryGetComponent(out SpriteRenderer sr)) sr.sortingOrder = 10;
    }

    // ══════════════════════════════════════════════
    //  에디터 편의
    // ══════════════════════════════════════════════
#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터 값을 바꿀 때마다 유니티가 불러 줍니다. (빌드에서는 호출되지 않음 → #if 로 감쌈)
    /// 등급별 체크를 막 켰을 때 빈 칸을 일반 값으로 채웁니다.
    ///
    /// ★ 자식 스킬은 override 하고 반드시 base.OnValidate() 를 먼저 부르세요.
    ///   안 부르면 damage / cooldown 쪽 채우기가 빠집니다.
    /// </summary>
    protected virtual void OnValidate()
    {
        damageByGrade?.FillIfEmpty(damage);
        cooldownByGrade?.FillIfEmpty(cooldown);

        // 쿨타임 칸 하나만 0 으로 남는 실수는 FillIfEmpty 가 못 잡습니다 (다른 칸에 값이 있으므로).
        //   그 등급 동료만 0.1초마다 스킬을 쏘게 되는데 에러가 없어서, 여기서 경고로 알려줍니다.
        //   (피해 0 은 '디버프 전용 스킬' 처럼 일부러 그럴 수 있어서 경고하지 않습니다)
        if (cooldownByGrade != null && cooldownByGrade.HasZeroSlot())
            Debug.LogWarning($"[ActiveSkill] '{name}' 등급별 쿨타임에 0 인 칸이 있습니다. 그 등급은 {MIN_COOLDOWN}초마다 발사됩니다.", this);

        // 성급 표를 처음 켜면 각 줄을 '그 등급의 지금 값' 으로 채웁니다 (켜는 순간 0 이 되지 않게).
        damageByStar?.FillIfEmpty(g => ByGrade(damageByGrade, damage, g));
        cooldownByStar?.FillIfEmpty(g => ByGrade(cooldownByGrade, cooldown, g));

        if (cooldownByStar != null && cooldownByStar.HasZeroSlot())
            Debug.LogWarning($"[ActiveSkill] '{name}' 성급별 쿨타임에 0 인 칸이 있습니다. 그 칸은 {MIN_COOLDOWN}초마다 발사됩니다.", this);
    }
#endif
}

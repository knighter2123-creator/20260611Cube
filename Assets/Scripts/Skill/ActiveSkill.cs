using UnityEngine;

/// <summary>액티브 스킬 베이스 — CalcDamage 로 플레이어 스탯 기반 최종 피해·치명타 계산, 에셋은 여러 동료가 공유하므로 값을 바꾸지 않고 GetDamage/GetCooldown(caster) 로 시전자 등급·성급 값을 읽기만 함.</summary>
// ⚠ skill.damage = x 처럼 에셋 값 대입 금지 (공유 동료 전체 + 에디터에선 에셋 파일까지 바뀜)
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

    /// <summary>쿨다운 하한(0 이면 매 프레임 발사) — 플레이어 공격 하한과 같은 상수.</summary>
    public const float MIN_COOLDOWN = PlayerStat.MIN_ATTACK_COOLDOWN;

    public abstract void Execute(Enemy target, Companion caster);

    // ── [등급별 수치] 조회 ──

    /// <summary>시전자의 등급. 시전자가 없거나 데이터가 비어 있으면 일반으로 봅니다.</summary>
    public static CompanionGrade GradeOf(Companion caster)
    {
        // UnityEngine.Object 라 ?. 대신 != null (파괴된 오브젝트도 걸러냄)
        if (caster == null || caster.Data == null) return CompanionGrade.Normal;
        return caster.Data.grade;
    }

    /// <summary>등급표 값 조회 — 표가 null 이어도(코드로 CreateInstance 등) 기본값 반환.</summary>
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

    /// <summary>시전자의 실제 재사용 대기(시너지 감소 포함) — 발사와 게이지가 같은 함수를 써서 일치 (도감은 기본값 GetCooldown(grade, star)).</summary>
    public float GetCooldown(Companion caster)
        => Mathf.Max(GetCooldown(GradeOf(caster), StarOf(caster)) * SynergyCooldownFactor, MIN_COOLDOWN);

    // ── [시너지] 보정 — 팀 전체 보너스(시전자 무관)를 읽어 곱하기만, 에셋 값은 불변 ──

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

    // ── [도감] 효과 요약 ──

    /// <summary>도감용 '이 스킬만의 효과' 한 줄(등급별) — 스킬이 자기 설명을 해서 도감 분기 불필요, virtual 빈 기본값이라 원하는 스킬만 override (기본 피해·쿨다운은 도감이 따로 그리니 넣지 말 것).</summary>
    public virtual string GetEffectSummary(CompanionGrade grade) => "";

    // 요약 문장 표기 도우미 — "0.#" 은 소수 첫째 자리까지 (0.4 → 40%, 0.125 → 12.5%)
    protected static string FormatPercent(float rate01) => $"{rate01 * 100f:0.#}%";
    protected static string FormatSeconds(float sec)    => $"{sec:0.#}초";

    // ── 사용 ──

    /// <summary>스킬 사용 진입점 — Execute 후 쿨다운 UI 자동 표시 (호출부는 Execute 대신 Cast).</summary>
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

    /// <summary>최종 피해 = (플레이어 최종 공격력 + 시전자 스킬 피해) × (치명타면 치명타 배율).</summary>
    protected (float finalDamage, bool isCritical) CalcDamage(Companion caster)
    {
        PlayerStat stat = StatOf(caster);
        bool crit = stat != null && Random.Range(0f, 100f) < stat.FinalCritical;
        return (WithPlayerAttack(caster, GetDamage(caster), crit), crit);
    }

    /// <summary>(플레이어 Final 공격력 + extra) × 치명타 배율 × 시너지 스킬 피해 보너스(최종 값 전체에 곱해 후반에도 체감) — 즉발·독 공용, 증강·각성 버프는 플레이어 몫에만 반영, Player 가 없으면 extra 만.</summary>
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

    // ── 에디터 편의 ──
#if UNITY_EDITOR
    /// <summary>인스펙터 변경 시(에디터 전용) 등급별 빈 칸을 일반 값으로 채움.</summary>
    // ⚠ 자식 override 는 base.OnValidate() 를 먼저 호출 (안 하면 damage/cooldown 채우기 누락)
    protected virtual void OnValidate()
    {
        damageByGrade?.FillIfEmpty(damage);
        cooldownByGrade?.FillIfEmpty(cooldown);

        // 쿨타임 칸 하나만 0 인 실수 경고 (FillIfEmpty 가 못 잡음, 피해 0 은 디버프 전용일 수 있어 제외)
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

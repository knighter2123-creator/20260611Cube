using UnityEngine;

[CreateAssetMenu(fileName = "SkillPoison", menuName = "Skills/Poison")]
public class SkillPoison : ActiveSkill
{
    [Header("독 설정 (일반 등급 값)")]
    public float dotDamage   = 10f;
    public GradeFloat dotDamageByGrade = new GradeFloat();

    public float dotInterval = 1f;
    public GradeFloat dotIntervalByGrade = new GradeFloat();

    public float dotDuration = 5f;
    public GradeFloat dotDurationByGrade = new GradeFloat();

    /// <summary>
    /// 독 간격 하한.
    /// ★ Enemy.DotRoutine 은 'elapsed += dotInterval' 로 시간을 셉니다. 간격이 0 이면 elapsed 가 영원히 안 늘어서
    ///   적이 죽을 때까지 '매 프레임' 독 피해가 들어갑니다. 등급 칸에 0 을 실수로 넣는 경우를 막습니다.
    /// </summary>
    private const float MIN_DOT_INTERVAL = 0.05f;

    public float GetDotDamage(CompanionGrade g)   => ByGrade(dotDamageByGrade, dotDamage, g);
    public float GetDotInterval(CompanionGrade g) => Mathf.Max(ByGrade(dotIntervalByGrade, dotInterval, g), MIN_DOT_INTERVAL);
    public float GetDotDuration(CompanionGrade g) => Mathf.Max(0f, ByGrade(dotDurationByGrade, dotDuration, g));

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        CompanionGrade g = GradeOf(caster);

        var (finalDamage, isCritical) = CalcDamage(caster);

        // 독 피해도 즉발 피해와 같은 공식 — '플레이어 공격력 + 독 피해' 에 같은 치명타 판정을 적용합니다.
        float finalDot = WithPlayerAttack(caster, GetDotDamage(g), isCritical);

        target.TakeDamage(finalDamage, isCritical);
        target.ApplyDot(finalDot, GetDotInterval(g), GetDotDuration(g));
    }

    // 효과 요약 — 위 Execute 의 독 피해 공식(플레이어 공격력 + dotDamage)과 같은 말로 적습니다.
    public override string GetEffectSummary(CompanionGrade grade)
        => $"적 1체에게 피해 + {FormatSeconds(GetDotDuration(grade))} 동안 {FormatSeconds(GetDotInterval(grade))}마다 " +
           $"독 피해(플레이어 공격력 + {GetDotDamage(grade):0.#})";

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        dotDamageByGrade?.FillIfEmpty(dotDamage);
        dotIntervalByGrade?.FillIfEmpty(dotInterval);
        dotDurationByGrade?.FillIfEmpty(dotDuration);

        // 독 간격 0 칸은 하한(0.05초)으로 막히지만, 초당 20번 독 피해라 사실상 실수 — 경고로 알려줍니다.
        if (dotIntervalByGrade != null && dotIntervalByGrade.HasZeroSlot())
            Debug.LogWarning($"[SkillPoison] '{name}' 등급별 독 간격에 0 인 칸이 있습니다. 그 등급은 {MIN_DOT_INTERVAL}초마다 독 피해가 들어갑니다.", this);
    }
#endif
}
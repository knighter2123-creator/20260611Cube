using UnityEngine;

[CreateAssetMenu(fileName = "SkillSlow", menuName = "Skills/Slow")]
public class SkillSlow : ActiveSkill
{
    [Header("둔화 설정 (일반 등급 값)")]
    [Range(0f, 1f)]
    public float slowRate     = 0.5f;
    public GradeFloat slowRateByGrade = new GradeFloat();

    public float slowDuration = 3f;
    public GradeFloat slowDurationByGrade = new GradeFloat();

    // [Range] 는 중첩 칸에 적용되지 않으므로 읽을 때 0~1 로 자릅니다. (1 = 완전 정지)
    public float GetSlowRate(CompanionGrade g)     => Mathf.Clamp01(ByGrade(slowRateByGrade, slowRate, g));
    public float GetSlowDuration(CompanionGrade g) => Mathf.Max(0f, ByGrade(slowDurationByGrade, slowDuration, g));

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        CompanionGrade g = GradeOf(caster);

        var (finalDamage, isCritical) = CalcDamage(caster);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplySlow(GetSlowRate(g), GetSlowDuration(g));
    }

    // 효과 요약
    //   Enemy.SlowRoutine 이 SetSlowMultiplier(1 - rate) 로 쓰므로 slowRate 는 '감소율' 이 맞습니다.
    //   (0.5 → 이동속도 50% 감소, 값이 클수록 강함)
    public override string GetEffectSummary(CompanionGrade grade)
        => $"적 1체에게 피해 + {FormatSeconds(GetSlowDuration(grade))} 동안 이동속도 {FormatPercent(GetSlowRate(grade))} 감소";

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        slowRateByGrade?.FillIfEmpty(slowRate);
        slowDurationByGrade?.FillIfEmpty(slowDuration);
    }
#endif
}
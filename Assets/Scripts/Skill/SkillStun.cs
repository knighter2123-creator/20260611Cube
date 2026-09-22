using UnityEngine;

[CreateAssetMenu(fileName = "SkillStun", menuName = "Skills/Stun")]
public class SkillStun : ActiveSkill
{
    [Header("스턴 설정 (일반 등급 값)")]
    [Tooltip("행동 불능 지속시간")]
    public float stunDuration = 2f;
    public GradeFloat stunDurationByGrade = new GradeFloat();

    [Header("스턴 연출")]
    [Tooltip("스턴 동안 몬스터를 회색으로 표시")]
    public bool grayscaleOnStun = true;

    // 연출값이라 등급별로 나누지 않았습니다 (게임 규칙이 아님)
    [Tooltip("회색 지속시간을 스턴 시간보다 살짝 길게(0이면 동일)")]
    public float grayscaleExtra = 0f;

    public float GetStunDuration(CompanionGrade g)
        => Mathf.Max(0f, ByGrade(stunDurationByGrade, stunDuration, g));

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        float duration = GetStunDuration(GradeOf(caster));

        var (finalDamage, isCritical) = CalcDamage(caster);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyStun(duration);

        // 스턴 동안 몬스터를 회색으로 — ★ 회색 시간도 '등급별 스턴 시간' 기준이어야 스턴과 같이 풀립니다
        if (grayscaleOnStun && !target.isDead)
            GrayscaleEffect.Apply(target.gameObject, duration + grayscaleExtra);
    }

    // ★ [도감] 효과 요약 (회색 연출 시간은 게임 규칙이 아니라 연출이라 적지 않습니다)
    public override string GetEffectSummary(CompanionGrade grade)
        => $"적 1체에게 피해 + {FormatSeconds(GetStunDuration(grade))} 동안 기절";

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        stunDurationByGrade?.FillIfEmpty(stunDuration);
    }
#endif
}
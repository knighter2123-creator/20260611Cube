using UnityEngine;

[CreateAssetMenu(fileName = "SkillAttack", menuName = "Skills/Attack")]
public class SkillAttack : ActiveSkill
{
    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster);
        target.TakeDamage(finalDamage, isCritical);
    }

    // 효과 요약 — 등급과 상관없는 문장이라 grade 를 쓰지 않습니다.
    public override string GetEffectSummary(CompanionGrade grade) => "적 1체에게 피해를 줍니다.";
}
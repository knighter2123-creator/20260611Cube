using UnityEngine;

[CreateAssetMenu(fileName = "SkillAttack", menuName = "Skills/Attack")]
public class SkillAttack : ActiveSkill
{
    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
    }

    // ★ [도감] 효과 요약
    public override string GetEffectSummary() => "적 1체에게 피해를 줍니다.";
}
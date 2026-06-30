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
}
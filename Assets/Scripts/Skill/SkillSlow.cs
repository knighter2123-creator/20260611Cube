using UnityEngine;

[CreateAssetMenu(fileName = "SkillSlow", menuName = "Skills/Slow")]
public class SkillSlow : ActiveSkill
{
    [Range(0f, 1f)]
    public float slowRate     = 0.5f;
    public float slowDuration = 3f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplySlow(slowRate, slowDuration);
    }
}
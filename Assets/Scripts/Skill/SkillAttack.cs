using UnityEngine;

[CreateAssetMenu(fileName = "SkillAttack", menuName = "Skills/Attack")]
public class SkillAttack : ActiveSkill
{
    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        target.TakeDamage(damage);
        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} → {damage} 데미지");
    }
}
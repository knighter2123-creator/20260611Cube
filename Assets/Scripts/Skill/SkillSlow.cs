using UnityEngine;

[CreateAssetMenu(fileName = "SkillSlow", menuName = "Skills/Slow")]
public class SkillSlow : ActiveSkill
{
    [Range(0f, 1f)]
    public float slowRate     = 0.5f;   // 50% 감속
    public float slowDuration = 3f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        target.TakeDamage(damage);
        target.ApplySlow(slowRate, slowDuration); // Enemy에 ApplySlow 추가 필요
        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} → 둔화 {slowRate * 100}%");
    }
}
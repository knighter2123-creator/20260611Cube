using UnityEngine;

[CreateAssetMenu(fileName = "SkillStun", menuName = "Skills/Stun")]
public class SkillStun : ActiveSkill
{
    [Header("스턴 설정")]
    [Tooltip("행동 불능 지속시간")]
    public float stunDuration = 2f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyStun(stunDuration);

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} → {finalDamage} 데미지{(isCritical ? " (크리티컬!)" : "")} + {stunDuration}s 스턴");
    }
}
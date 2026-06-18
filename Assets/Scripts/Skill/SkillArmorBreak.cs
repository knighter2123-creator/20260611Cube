using UnityEngine;

[CreateAssetMenu(fileName = "SkillArmorBreak", menuName = "Skills/ArmorBreak")]
public class SkillArmorBreak : ActiveSkill
{
    [Header("방어력 감소 설정")]
    [Range(0f, 1f)]
    public float armorBreakRate     = 0.4f;
    public float armorBreakDuration = 4f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyArmorBreak(armorBreakRate, armorBreakDuration);

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} → {finalDamage} 데미지{(isCritical ? " (크리티컬!)" : "")} + 방어력 {armorBreakRate * 100}% 감소 ({armorBreakDuration}s)");
    }
}
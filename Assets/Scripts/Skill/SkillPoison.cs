using UnityEngine;

[CreateAssetMenu(fileName = "SkillPoison", menuName = "Skills/Poison")]
public class SkillPoison : ActiveSkill
{
    [Header("독 설정")]
    public float dotDamage   = 10f;
    public float dotInterval = 1f;
    public float dotDuration = 5f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);

        // DoT 데미지도 크리티컬 시 배율 적용
        float finalDot = isCritical
            ? (caster.Stat.baseDamage + dotDamage) * caster.Stat.CriticalMultiplier
            : (caster.Stat.baseDamage + dotDamage);

        target.TakeDamage(finalDamage, isCritical);
        target.ApplyDot(finalDot, dotInterval, dotDuration);

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} → {finalDamage} 즉시{(isCritical ? " (크리티컬!)" : "")} + {finalDot}/s 독 ({dotDuration}s)");
    }
}
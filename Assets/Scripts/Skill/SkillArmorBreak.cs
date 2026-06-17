using System.Collections;
using UnityEngine;

/// <summary>
/// 방어력 감소 스킬 — 적의 방어력을 일정 시간 동안 낮춥니다.
/// Enemy에 ApplyArmorBreak(float rate, float duration) 메서드가 필요합니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillArmorBreak", menuName = "Skills/ArmorBreak")]
public class SkillArmorBreak : ActiveSkill
{
    [Header("방어력 감소 설정")]
    [Range(0f, 1f)]
    public float armorBreakRate     = 0.4f;   // 방어력 40% 감소
    public float armorBreakDuration = 4f;     // 지속 시간(초)

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        target.TakeDamage(damage);
        target.ApplyArmorBreak(armorBreakRate, armorBreakDuration);

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} " +
                  $"→ {damage} 데미지 + 방어력 {armorBreakRate * 100}% 감소 ({armorBreakDuration}s)");
    }
}

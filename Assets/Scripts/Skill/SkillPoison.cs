using UnityEngine;

/// <summary>
/// 독 스킬 — 대미지를 주고 일정 시간 동안 매 초 추가 대미지를 줍니다.
/// Enemy에 ApplyDot(float dotDamage, float interval, float duration) 메서드가 필요합니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillPoison", menuName = "Skills/Poison")]
public class SkillPoison : ActiveSkill
{
    [Header("독 설정")]
    public float dotDamage   = 10f;   // 매 틱당 데미지
    public float dotInterval = 1f;    // 틱 간격(초)
    public float dotDuration = 5f;    // 지속 시간(초)

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        target.TakeDamage(damage);
        target.ApplyDot(dotDamage, dotInterval, dotDuration);

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} " +
                  $"→ {damage} 즉시 데미지 + {dotDamage}/s 독 ({dotDuration}s)");
    }
}

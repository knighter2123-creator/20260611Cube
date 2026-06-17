using UnityEngine;

/// <summary>
/// 스턴 스킬 — 대미지를 주고 적을 일정 시간 멈춥니다.
/// Enemy에 ApplyStun(float duration) 메서드가 필요합니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillStun", menuName = "Skills/Stun")]
public class SkillStun : ActiveSkill
{
    [Header("스턴 설정")]
    [Tooltip("행동 불능 지속시간")]
    public float stunDuration = 2f;   // 행동 불능 지속 시간(초)

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        target.TakeDamage(damage);
        target.ApplyStun(stunDuration);

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} " +
                  $"→ {damage} 데미지 + {stunDuration}s 스턴");
    }
}

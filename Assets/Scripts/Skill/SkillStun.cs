using UnityEngine;

[CreateAssetMenu(fileName = "SkillStun", menuName = "Skills/Stun")]
public class SkillStun : ActiveSkill
{
    [Header("스턴 설정")]
    [Tooltip("행동 불능 지속시간")]
    public float stunDuration = 2f;

    [Header("스턴 연출")]
    [Tooltip("스턴 동안 몬스터를 회색으로 표시")]
    public bool grayscaleOnStun = true;

    [Tooltip("회색 지속시간을 스턴 시간보다 살짝 길게(0이면 동일)")]
    public float grayscaleExtra = 0f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyStun(stunDuration);

        // 스턴 동안 몬스터를 회색으로
        if (grayscaleOnStun && !target.isDead)
            GrayscaleEffect.Apply(target.gameObject, stunDuration + grayscaleExtra);

    }

    // ★ [도감] 효과 요약 (회색 연출 시간은 게임 규칙이 아니라 연출이라 적지 않습니다)
    public override string GetEffectSummary()
        => $"적 1체에게 피해 + {FormatSeconds(stunDuration)} 동안 기절";
}
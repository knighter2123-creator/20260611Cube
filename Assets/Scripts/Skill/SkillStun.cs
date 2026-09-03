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

        // 시전자 머리 위 쿨다운 표시
        // (호출부를 skill.Cast(...) 로 바꿨다면 이 줄은 지워도 됩니다 — 중복 호출돼도 동작엔 문제 없음)
        NotifyCooldown(caster);
    }
}
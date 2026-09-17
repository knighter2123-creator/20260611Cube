using UnityEngine;

[CreateAssetMenu(fileName = "SkillSlow", menuName = "Skills/Slow")]
public class SkillSlow : ActiveSkill
{
    [Range(0f, 1f)]
    public float slowRate     = 0.5f;
    public float slowDuration = 3f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplySlow(slowRate, slowDuration);
    }

    // ★ [도감] 효과 요약
    //   ※ slowRate 0.5 를 '이동속도 50% 감소' 로 표기합니다.
    //     Enemy.ApplySlow 가 이 값을 '속도 배율(×slowRate)' 로 쓴다면 0.3 일 때 실제로는 70% 감소입니다.
    //     그 경우 FormatPercent(1f - slowRate) 로 바꾸세요. (Enemy.Debuffs.cs 에서 확인)
    public override string GetEffectSummary()
        => $"적 1체에게 피해 + {FormatSeconds(slowDuration)} 동안 이동속도 {FormatPercent(slowRate)} 감소";
}
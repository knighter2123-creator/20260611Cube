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
    }

    // ★ [도감] 효과 요약 — 위 Execute 의 독 피해 공식(플레이어 공격력 + dotDamage)과 같은 말로 적습니다.
    public override string GetEffectSummary()
        => $"적 1체에게 피해 + {FormatSeconds(dotDuration)} 동안 {FormatSeconds(dotInterval)}마다 " +
           $"독 피해(플레이어 공격력 + {dotDamage:0.#})";
}
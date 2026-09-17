using UnityEngine;

[CreateAssetMenu(fileName = "SkillArmorBreak", menuName = "Skills/ArmorBreak")]
public class SkillArmorBreak : ActiveSkill
{
    [Header("방어력 감소 설정")]
    [Range(0f, 1f)]
    public float armorBreakRate     = 0.4f;
    public float armorBreakDuration = 4f;

    [Header("피격 시각 효과")]
    [SerializeField] private GameObject armorBreakEffectPrefab; // SpriteRenderer를 가진 프리팹

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        var (finalDamage, isCritical) = CalcDamage(caster.Stat);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyArmorBreak(armorBreakRate, armorBreakDuration);

        // 지속시간 동안 적에게 스프라이트 부착, 끝나면 자동 제거
        if (!target.isDead)
            TimedAttachEffect.Spawn(armorBreakEffectPrefab, target.transform,
                armorBreakDuration, "ArmorBreak");
    }

    // ★ [도감] 효과 요약 — 실제로 쓰는 필드를 그대로 읽으므로 인스펙터 값을 바꾸면 도감도 따라 바뀝니다.
    //   ※ armorBreakRate 0.4 를 '40% 감소' 로 표기합니다. Enemy.ApplyArmorBreak 가 이 값을
    //     '남는 비율(×0.4)' 로 쓴다면 문구를 FormatPercent(1f - armorBreakRate) 로 바꾸세요.
    public override string GetEffectSummary()
        => $"적 1체에게 피해 + {FormatSeconds(armorBreakDuration)} 동안 방어력 {FormatPercent(armorBreakRate)} 감소";
}
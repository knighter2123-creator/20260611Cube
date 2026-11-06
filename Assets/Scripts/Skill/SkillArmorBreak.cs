using UnityEngine;

[CreateAssetMenu(fileName = "SkillArmorBreak", menuName = "Skills/ArmorBreak")]
public class SkillArmorBreak : ActiveSkill
{
    [Header("방어력 감소 설정 (일반 등급 값)")]
    [Range(0f, 1f)]
    public float armorBreakRate     = 0.4f;
    public GradeFloat armorBreakRateByGrade = new GradeFloat();

    public float armorBreakDuration = 4f;
    public GradeFloat armorBreakDurationByGrade = new GradeFloat();

    [Header("피격 시각 효과")]
    [SerializeField] private GameObject armorBreakEffectPrefab; // SpriteRenderer를 가진 프리팹

    // ── [등급] 조회 (Range 는 중첩 GradeFloat 칸엔 안 먹어 읽을 때 Clamp01) ──
    public float GetArmorBreakRate(CompanionGrade g)
        => Mathf.Clamp01(ByGrade(armorBreakRateByGrade, armorBreakRate, g));

    public float GetArmorBreakDuration(CompanionGrade g)
        => Mathf.Max(0f, ByGrade(armorBreakDurationByGrade, armorBreakDuration, g));

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        // 등급은 한 번만 구해 공유, 시너지는 감소율 %p 가산(0~1)·지속시간 비율 (부착 이펙트도 같은 duration)
        CompanionGrade g        = GradeOf(caster);
        float          rate     = WithSynergyRate(GetArmorBreakRate(g), SynergyStat.ArmorBreakRate);
        float          duration = WithSynergyScale(GetArmorBreakDuration(g), SynergyStat.DebuffDuration);

        var (finalDamage, isCritical) = CalcDamage(caster);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyArmorBreak(rate, duration);

        // 지속시간 동안 스프라이트 부착 (효과와 같은 시간이라 그림만 남지 않음)
        if (!target.isDead)
            TimedAttachEffect.Spawn(armorBreakEffectPrefab, target.transform, duration, "ArmorBreak");
    }

    // 효과 요약 — rate 는 감소율 (0.4 → 방어력 40% 감소, 배율 = 1 − rate)
    public override string GetEffectSummary(CompanionGrade grade)
        => $"적 1체에게 피해 + {FormatSeconds(GetArmorBreakDuration(grade))} 동안 " +
           $"방어력 {FormatPercent(GetArmorBreakRate(grade))} 감소";

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();   // ★ 빼먹으면 피해/쿨다운 칸 자동 채우기가 안 됩니다
        armorBreakRateByGrade?.FillIfEmpty(armorBreakRate);
        armorBreakDurationByGrade?.FillIfEmpty(armorBreakDuration);
    }
#endif
}
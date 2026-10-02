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

    // ── [등급] 조회 ──
    // ★ [Range(0,1)] 은 인스펙터 슬라이더일 뿐, 중첩된 GradeFloat 칸에는 적용되지 않습니다.
    //   전설 칸에 1.4 를 적어도 막을 방법이 없으니, 읽을 때 Clamp01 로 0~1 을 보장합니다.
    public float GetArmorBreakRate(CompanionGrade g)
        => Mathf.Clamp01(ByGrade(armorBreakRateByGrade, armorBreakRate, g));

    public float GetArmorBreakDuration(CompanionGrade g)
        => Mathf.Max(0f, ByGrade(armorBreakDurationByGrade, armorBreakDuration, g));

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        // 등급은 한 번만 구해서 아래에서 같이 씁니다 (피해·효과가 서로 다른 등급으로 계산되는 실수 방지)
        CompanionGrade g        = GradeOf(caster);
        float          rate     = GetArmorBreakRate(g);
        float          duration = GetArmorBreakDuration(g);

        var (finalDamage, isCritical) = CalcDamage(caster);
        target.TakeDamage(finalDamage, isCritical);
        target.ApplyArmorBreak(rate, duration);

        // 지속시간 동안 적에게 스프라이트 부착, 끝나면 자동 제거
        // ★ 부착 시간도 등급별 지속시간과 같은 값을 넘겨야 '효과는 끝났는데 그림만 남는' 어긋남이 없습니다.
        if (!target.isDead)
            TimedAttachEffect.Spawn(armorBreakEffectPrefab, target.transform, duration, "ArmorBreak");
    }

    // 효과 요약
    //   Enemy.ArmorBreakRoutine 이 _armorBreakMultiplier = 1 - rate 로 쓰므로 rate 는 '감소율' 이 맞습니다.
    //   (0.4 → 방어력 40% 감소)
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
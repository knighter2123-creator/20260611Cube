using UnityEngine;

[CreateAssetMenu(fileName = "SkillExplosion", menuName = "Skills/Explosion")]
public class SkillExplosion : ActiveSkill
{
    [Header("범위 설정 (일반 등급 값)")]
    public float explosionRadius = 3f;
    public GradeFloat explosionRadiusByGrade = new GradeFloat();

    public float GetExplosionRadius(CompanionGrade g)
        => Mathf.Max(0f, ByGrade(explosionRadiusByGrade, explosionRadius, g));

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        float radius = GetExplosionRadius(GradeOf(caster));

        PlayEffect(target.transform.position);
        // ※ 이펙트 크기는 반경과 따로 놉니다. 전설 반경을 크게 늘렸다면 이펙트가 작아 보일 수 있습니다
        //   (셋업 문서 '나중에 확장할 거리' 참고).

        Collider2D[] hits = Physics2D.OverlapCircleAll(target.transform.position, radius);

        foreach (Collider2D col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy == null || enemy.isDead) continue;

            // 적마다 개별 크리티컬 판정
            var (finalDamage, isCritical) = CalcDamage(caster);
            enemy.TakeDamage(finalDamage, isCritical);
        }
        // ※ 원래 있던 hitCount 는 어디서도 읽지 않아 지웠습니다 (동작 차이 없음).
    }

    // ★ [도감] 효과 요약
    public override string GetEffectSummary(CompanionGrade grade)
        => $"대상 주변 반경 {GetExplosionRadius(grade):0.#} 안의 모든 적에게 피해 (적마다 치명타 따로 판정)";

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        explosionRadiusByGrade?.FillIfEmpty(explosionRadius);
    }
#endif
}
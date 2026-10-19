using System.Collections.Generic;
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

        // ★ [프레임 드랍 수정] OverlapCircleAll 은 부를 때마다 새 배열을 만듭니다(GC).
        //   결과 목록을 재사용하는 OverlapCircle(…, List) 로 바꿨습니다.
        //   필터는 OverlapCircleAll 의 기본 동작(기본 레이어 · 트리거 포함 여부는 프로젝트 설정)과 똑같이 맞췄습니다.
        HitFilter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.OverlapCircle(target.transform.position, radius, HitFilter, HitBuffer);

        // 공유 목록이라 다 쓰고 나면 비워서, 이미 풀로 돌아간 적의 콜라이더를 붙잡고 있지 않게 합니다.
        for (int i = 0; i < HitBuffer.Count; i++)
        {
            Collider2D col = HitBuffer[i];
            if (col == null || !col.TryGetComponent(out Enemy enemy) || enemy.isDead) continue;

            // 적마다 개별 크리티컬 판정
            var (finalDamage, isCritical) = CalcDamage(caster);
            enemy.TakeDamage(finalDamage, isCritical);
        }

        HitBuffer.Clear();
    }

    // 폭발 판정 결과를 담는 공용 목록과 필터 (모든 폭발 스킬 에셋이 함께 씀 — 메인 스레드 전용이라 안전)
    private static readonly List<Collider2D> HitBuffer = new List<Collider2D>(32);
    private static ContactFilter2D HitFilter = CreateFilter();

    private static ContactFilter2D CreateFilter()
    {
        var f = new ContactFilter2D();
        f.SetLayerMask(Physics2D.DefaultRaycastLayers);   // OverlapCircleAll 의 기본 layerMask 와 동일
        return f;
    }

    // 효과 요약
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
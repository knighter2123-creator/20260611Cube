using UnityEngine;

/// <summary>
/// 범위 폭발 스킬 — 타겟 주변 적들에게 동시에 대미지를 줍니다.
/// </summary>
[CreateAssetMenu(fileName = "SkillExplosion", menuName = "Skills/Explosion")]
public class SkillExplosion : ActiveSkill
{
    [Header("범위 설정")]
    public float explosionRadius = 3f;   // 폭발 반경

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        // 타겟 위치 기준 범위 내 모든 적 탐색 (2D)
        Collider2D[] hits = Physics2D.OverlapCircleAll(target.transform.position, explosionRadius);
        int hitCount = 0;

        foreach (Collider2D col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy == null || enemy.isDead) continue;

            enemy.TakeDamage(damage);
            hitCount++;
        }

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} " +
                  $"→ 반경 {explosionRadius}m 내 {hitCount}명에게 {damage} 데미지");
    }
}
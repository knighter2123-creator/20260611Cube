using UnityEngine;

[CreateAssetMenu(fileName = "SkillExplosion", menuName = "Skills/Explosion")]
public class SkillExplosion : ActiveSkill
{
    [Header("범위 설정")]
    public float explosionRadius = 3f;

    public override void Execute(Enemy target, Companion caster)
    {
        if (target == null || target.isDead) return;

        PlayEffect(target.transform.position); 
        
        Collider2D[] hits = Physics2D.OverlapCircleAll(target.transform.position, explosionRadius);
        int hitCount = 0;

        foreach (Collider2D col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy == null || enemy.isDead) continue;

            // 적마다 개별 크리티컬 판정
            var (finalDamage, isCritical) = CalcDamage(caster.Stat);
            enemy.TakeDamage(finalDamage, isCritical);
            hitCount++;
        }

        Debug.Log($"[Skill] {caster.CompanionName} → {skillName} → 반경 {explosionRadius}m 내 {hitCount}명 적중");
    }
}
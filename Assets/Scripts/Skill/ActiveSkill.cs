using UnityEngine;

/// <summary>
/// 모든 액티브 스킬의 베이스 클래스.
/// Execute() 호출 전 CalcDamage()로 플레이어 스탯 기반 최종 데미지와 크리티컬 여부를 계산합니다.
/// </summary>
public abstract class ActiveSkill : ScriptableObject
{
    [Header("스킬 기본 정보")]
    public string skillName   = "스킬";
    public float  damage      = 20f;   // 스킬 기본 배율 (플레이어 baseDamage에 곱해짐)
    public float  cooldown    = 5f;

    public abstract void Execute(Enemy target, Companion caster);

    /// <summary>
    /// 플레이어 스탯을 반영한 최종 데미지와 크리티컬 여부를 반환합니다.
    /// damage 필드를 baseDamage 배율로 사용합니다.
    /// </summary>
    protected (float finalDamage, bool isCritical) CalcDamage(PlayerStat stat)
    {
        float base_  = stat.baseDamage + damage;
        bool  crit   = Random.Range(0f, 100f) < stat.Critical;
        float final  = crit ? base_ * stat.CriticalMultiplier : base_;
        return (final, crit);
    }
}
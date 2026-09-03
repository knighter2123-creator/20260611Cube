using UnityEngine;

/// <summary>
/// 모든 액티브 스킬의 베이스 클래스.
/// Execute() 호출 전 CalcDamage()로 플레이어 스탯 기반 최종 데미지와 크리티컬 여부를 계산합니다.
/// </summary>
public abstract class ActiveSkill : ScriptableObject
{
    [Header("스킬 기본 정보")]
    public string skillName = "스킬";
    public float  damage    = 20f;
    public float  cooldown  = 5f;

    [Tooltip("동료 머리 위 쿨다운 인디케이터에 표시할 아이콘 (없으면 원형만 표시)")]
    public Sprite icon;

    [Header("이펙트")]
    public GameObject effectPrefab;
    public float  effectDuration = 0.5f;

    public abstract void Execute(Enemy target, Companion caster);

    /// <summary>
    /// 스킬 사용 진입점. Execute() 실행 후 쿨다운 UI를 자동으로 띄웁니다.
    /// 호출부에서 skill.Execute(target, this) 대신 skill.Cast(target, this) 를 쓰면
    /// 모든 스킬이 쿨다운 표시를 공짜로 얻습니다.
    /// </summary>
    public void Cast(Enemy target, Companion caster)
    {
        Execute(target, caster);
        NotifyCooldown(caster);
    }

    /// <summary>시전자 머리 위에 이 스킬의 쿨다운 게이지를 시작합니다.</summary>
    protected void NotifyCooldown(Companion caster)
    {
        if (caster == null) return;
        SkillCooldownIndicator.Begin(caster, icon, cooldown);
    }

    protected (float finalDamage, bool isCritical) CalcDamage(PlayerStat stat)
    {
        float base_ = stat.baseDamage + damage;
        bool  crit  = Random.Range(0f, 100f) < stat.Critical;
        float final = crit ? base_ * stat.CriticalMultiplier : base_;
        return (final, crit);
    }

    protected void PlayEffect(Vector3 position)
    {
        if (effectPrefab == null) return;

        GameObject fx = Instantiate(effectPrefab, position, Quaternion.identity);

        SpriteRenderer sr = fx.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 10;

        Destroy(fx, effectDuration);
    }
}
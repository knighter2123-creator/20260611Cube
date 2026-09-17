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

    // ★ [도감] 신규 필드. 기존 스킬 에셋에는 빈 문자열로 채워지므로 아무것도 깨지지 않습니다.
    //   (세이브 파일과도 무관합니다 — 스킬 에셋은 저장 대상이 아닙니다)
    [Header("도감 표시")]
    [Tooltip("도감 상세창에 추가로 보여줄 설명 (선택). 비워두면 효과 요약만 표시됩니다.")]
    [TextArea(2, 4)]
    public string description = "";

    public abstract void Execute(Enemy target, Companion caster);

    // ══════════════════════════════════════════════
    //  [도감] 효과 요약
    // ══════════════════════════════════════════════

    /// <summary>
    /// 도감 상세창에 보여줄 '이 스킬만의 효과' 한 줄.
    ///
    /// ★ 왜 abstract 가 아니라 virtual 인가
    ///   abstract 로 만들면 모든 자식 클래스가 '반드시' 구현해야 해서,
    ///   나중에 스킬을 새로 만들 때 이걸 안 쓰면 컴파일이 안 됩니다.
    ///   virtual + 빈 기본값이면 안 써도 도감이 정상 동작하고(효과 줄만 비어 보임),
    ///   쓰고 싶은 스킬만 override 하면 됩니다.
    ///
    /// ★ 왜 도감 UI 가 아니라 스킬 쪽에 두나
    ///   도감에서 "if (skill is SkillSlow) ... else if (skill is SkillStun) ..." 로 분기하면
    ///   스킬을 추가할 때마다 도감 코드를 고쳐야 합니다. 빼먹어도 에러가 안 나고요.
    ///   "자기 설명은 자기가 한다" 로 두면 새 스킬 파일 하나에서 끝납니다.
    ///
    /// ※ 기본 피해 / 쿨다운은 모든 스킬 공통이라 도감이 따로 그립니다. 여기엔 넣지 마세요.
    /// </summary>
    public virtual string GetEffectSummary() => "";

    // 요약 문장에서 같이 쓰는 표기 도우미 — 숫자 표기 규칙을 한 곳에 둡니다.
    // "0.#" 은 소수 첫째 자리까지, 0 이면 생략 (0.4 → 40%, 0.125 → 12.5%)
    protected static string FormatPercent(float rate01) => $"{rate01 * 100f:0.#}%";
    protected static string FormatSeconds(float sec)    => $"{sec:0.#}초";

    // ══════════════════════════════════════════════
    //  사용
    // ══════════════════════════════════════════════

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
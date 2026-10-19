using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시너지 1종의 정의 (예: 딜러, 제어, 약화, 조화).
/// Create → Companion → Synergy Definition 으로 에셋을 만들고 Resources/Synergy 폴더에 두면 자동으로 읽힙니다.
///
/// [규칙]
///   · 조건을 만족한 인원 수(Count)를 세고, requiredCount 이하인 단계 중 '가장 높은 단계' 하나만 적용합니다.
///     → 단계 값은 '누적 합계' 로 적으세요. (2명 +15%, 4명 +40% 이면 4명일 때 +40%. +55% 가 아님)
///   · 여러 시너지의 같은 능력치는 더해집니다.
///
/// [조건 종류]
///   TagCount — 이 계열(tag) 동료가 배치된 수
///   EveryTag — 모든 계열(SynergyTags.All)에서 '가장 적게 배치된 계열의 수'. 예) 딜러2 제어2 약화1 → 1
/// </summary>
[CreateAssetMenu(fileName = "Synergy", menuName = "Companion/Synergy Definition")]
public class SynergyDefinition : ScriptableObject
{
    public enum ConditionType
    {
        TagCount = 0,
        EveryTag = 1,
    }

    [Header("표시")]
    public string displayName = "시너지";
    [Tooltip("HUD 에 표시되는 순서 (작을수록 위)")]
    public int sortOrder = 0;

    [Header("조건")]
    public ConditionType conditionType = ConditionType.TagCount;
    [Tooltip("TagCount 일 때 셀 계열. EveryTag 에서는 쓰지 않습니다")]
    public SynergyTag tag = SynergyTag.Dealer;

    [Header("단계 (인원 수 오름차순)")]
    public List<SynergyTier> tiers = new List<SynergyTier>();

    /// <summary>HUD · 도감에서 쓰는 이 시너지의 색.</summary>
    public Color DisplayColor
        => conditionType == ConditionType.EveryTag ? SynergyTags.HarmonyColor : SynergyTags.GetColor(tag);

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tiers == null) return;

        // 단계를 인원 수 순서로 정렬해 둡니다. 계산기는 순서에 기대지 않지만, HUD 의 "다음 단계" 표시가 읽기 쉬워집니다.
        tiers.Sort((a, b) => (a?.requiredCount ?? 0).CompareTo(b?.requiredCount ?? 0));

        var seen = new HashSet<int>();
        foreach (SynergyTier t in tiers)
        {
            if (t == null) continue;
            if (!seen.Add(t.requiredCount))
                Debug.LogWarning($"[SynergyDefinition] '{name}' 에 같은 인원 수({t.requiredCount}) 단계가 두 개 있습니다. 하나만 적용됩니다.", this);
        }

        if (conditionType == ConditionType.TagCount && tag == SynergyTag.None)
            Debug.LogWarning($"[SynergyDefinition] '{name}' 의 계열이 None 입니다. 계열이 없는 동료 수를 세게 됩니다.", this);
    }
#endif
}

[Serializable]
public class SynergyTier
{
    [Min(1)] public int requiredCount = 2;
    public List<SynergyEffect> effects = new List<SynergyEffect>();
}

[Serializable]
public struct SynergyEffect
{
    public SynergyStat stat;
    [Tooltip("0.15 = +15%  (SlowRate / ArmorBreakRate 는 %p 가산)")]
    public float value;
}

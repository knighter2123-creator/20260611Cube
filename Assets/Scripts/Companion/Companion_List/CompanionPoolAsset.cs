using System.Collections.Generic;
using UnityEngine;

/// <summary>등급별 동료 풀 에셋(Create → Companion → CompanionPool) — 가챠와 도감이 같은 에셋을 직접 참조해 동료 추가가 양쪽에 동시 반영 (GachaSystem 은 가챠 씬 전엔 없어 도감이 비었음).</summary>
[CreateAssetMenu(fileName = "CompanionPool", menuName = "Companion/CompanionPool")]
public class CompanionPoolAsset : ScriptableObject
{
    [Header("등급별 동료 풀  ※ 각 동료의 grade 와 같은 칸에 넣으세요")]
    [SerializeField] private List<CompanionData> normal    = new List<CompanionData>();
    [SerializeField] private List<CompanionData> rare      = new List<CompanionData>();
    [SerializeField] private List<CompanionData> epic      = new List<CompanionData>();
    [SerializeField] private List<CompanionData> legendary = new List<CompanionData>();

    // 중복 시 받는 조각 수 — 가챠·도감(다른 씬)이 같은 값을 쓰도록 이 에셋에
    [Header("중복 시 조각 전환량 (등급별) — 가챠 중복 · 진화 중복 공용")]
    [Min(0)] [SerializeField] private int fragmentNormal    = 5;
    [Min(0)] [SerializeField] private int fragmentRare      = 10;
    [Min(0)] [SerializeField] private int fragmentEpic      = 15;
    [Min(0)] [SerializeField] private int fragmentLegendary = 20;

    /// <summary>이미 가진 동료를 또 얻었을 때 대신 받는 조각 수.</summary>
    public int DuplicateFragments(CompanionGrade grade) => grade switch
    {
        CompanionGrade.Rare      => fragmentRare,
        CompanionGrade.Epic      => fragmentEpic,
        CompanionGrade.Legendary => fragmentLegendary,
        _                        => fragmentNormal
    };

    /// <summary>진화 대상 = 바로 위 등급 풀에서 같은 스킬 에셋을 가진 동료(없으면 null, 여럿이면 먼저 넣은 쪽).</summary>
    public CompanionData FindEvolution(CompanionData from)
    {
        if (from == null || from.ownedSkill == null) return null;
        if (from.grade >= CompanionGrade.Legendary) return null;

        foreach (CompanionData d in GetPool(from.grade + 1))
            if (d != null && d.ownedSkill == from.ownedSkill) return d;

        return null;
    }

    /// <summary>등급별 풀(확률은 뒤끝 차트가 정함) — IReadOnlyList 로 내줌 (SO 는 플레이 중 수정이 에셋에 영구히 남아서).</summary>
    public IReadOnlyList<CompanionData> GetPool(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => normal,
            CompanionGrade.Rare      => rare,
            CompanionGrade.Epic      => epic,
            CompanionGrade.Legendary => legendary,
            _                        => normal
        };
    }

    /// <summary>모든 등급 동료를 차례로(도감용) — yield return 이라 합친 리스트를 새로 만들지 않음.</summary>
    public IEnumerable<CompanionData> All()
    {
        foreach (var d in normal)    yield return d;
        foreach (var d in rare)      yield return d;
        foreach (var d in epic)      yield return d;
        foreach (var d in legendary) yield return d;
    }

#if UNITY_EDITOR
    /// <summary>에디터 검증: 등급 칸과 동료 grade 가 다르면 경고 (진화는 칸 기준, 도감·조각은 grade 기준이라 어긋남).</summary>
    private void OnValidate()
    {
        CheckGrade(normal,    CompanionGrade.Normal);
        CheckGrade(rare,      CompanionGrade.Rare);
        CheckGrade(epic,      CompanionGrade.Epic);
        CheckGrade(legendary, CompanionGrade.Legendary);

        CheckDuplicateSkill(rare,      CompanionGrade.Rare);
        CheckDuplicateSkill(epic,      CompanionGrade.Epic);
        CheckDuplicateSkill(legendary, CompanionGrade.Legendary);

        CheckDuplicateEntry();
    }

    // 같은 동료 중복 경고 (도감에 두 번 나올 수 있음 — Ctrl+D 복제 실수)
    private void CheckDuplicateEntry()
    {
        var seen = new HashSet<CompanionData>();
        foreach (var d in All())
        {
            if (d == null) continue;
            if (!seen.Add(d))
                Debug.LogWarning($"[CompanionPool] '{d.companionName}' 이(가) 풀에 두 번 이상 들어 있습니다. " +
                                 "도감에 두 번 보이니 하나를 지우세요.", this);
        }
    }

    // 한 등급에 같은 스킬 동료가 둘이면 진화 대상이 모호해집니다 (FindEvolution 은 앞쪽만 씀).
    private void CheckDuplicateSkill(List<CompanionData> list, CompanionGrade grade)
    {
        if (list == null) return;
        var seen = new HashSet<ActiveSkill>();
        foreach (var d in list)
        {
            if (d == null || d.ownedSkill == null) continue;
            if (!seen.Add(d.ownedSkill))
                Debug.LogWarning($"[CompanionPool] {grade} 칸에 스킬 '{d.ownedSkill.name}' 을 가진 동료가 둘 이상입니다. " +
                                 $"아래 등급에서 진화할 때는 먼저 넣은 동료로만 진화합니다 ('{d.companionName}' 은 대상이 아님).", this);
        }
    }

    private void CheckGrade(List<CompanionData> list, CompanionGrade expected)
    {
        if (list == null) return;
        foreach (var d in list)
        {
            if (d == null) continue;   // 아직 비워둔 칸은 무시
            if (d.grade != expected)
                Debug.LogWarning($"[CompanionPool] '{d.companionName}'의 등급은 {d.grade} 인데 {expected} 칸에 들어 있습니다.", this);
        }
    }
#endif
}

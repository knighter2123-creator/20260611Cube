using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등급별 동료 풀을 담는 에셋. (신규)
///
/// ★ 왜 GachaSystem 의 리스트를 그대로 읽지 않고 에셋으로 뺐나
///   GachaSystem 은 가챠 씬에서 태어나 DontDestroyOnLoad 로 살아남는 싱글턴입니다.
///   유저가 앱을 켜고 가챠 씬에 한 번도 안 갔다면 MainScene 에서 GachaSystem.Instance 는 null 입니다.
///   그러면 도감이 텅 비는데, 에러도 안 납니다.
///
///   리스트를 에셋으로 옮기면 가챠와 도감이 '같은 에셋'을 각자 직접 참조합니다.
///   누가 살아 있든 상관없이 같은 목록을 보게 되고,
///   이 에셋에 동료를 하나 추가하면 가챠와 도감에 동시에 들어갑니다. ← "자동 등록"의 정체
///
/// 만드는 법: Project 창 우클릭 → Create → Companion → CompanionPool
/// </summary>
[CreateAssetMenu(fileName = "CompanionPool", menuName = "Companion/CompanionPool")]
public class CompanionPoolAsset : ScriptableObject
{
    [Header("등급별 동료 풀  ※ 각 동료의 grade 와 같은 칸에 넣으세요")]
    [SerializeField] private List<CompanionData> normal    = new List<CompanionData>();
    [SerializeField] private List<CompanionData> rare      = new List<CompanionData>();
    [SerializeField] private List<CompanionData> epic      = new List<CompanionData>();
    [SerializeField] private List<CompanionData> legendary = new List<CompanionData>();

    // 가챠 중복 / 진화 중복 때 받는 조각 수. 가챠(ShopScene)와 도감(StageScene)이 같은 값을 써야 해서
    // 어느 씬에서든 읽을 수 있는 이 에셋에 둡니다.
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

    /// <summary>
    /// 진화 대상 — 바로 위 등급 풀에서 '같은 스킬 에셋' 을 가진 동료. 없으면 null (최고 등급이거나 짝이 없음).
    ///   예) nm_atk(SkillAttack) → ra_atk(SkillAttack),  ra_Slow(SkillSlow) → 영웅 SkillSlow 가 없으면 null
    /// 같은 스킬이 여럿이면 풀에 먼저 넣은 쪽 (OnValidate 가 경고합니다).
    /// </summary>
    public CompanionData FindEvolution(CompanionData from)
    {
        if (from == null || from.ownedSkill == null) return null;
        if (from.grade >= CompanionGrade.Legendary) return null;

        foreach (CompanionData d in GetPool(from.grade + 1))
            if (d != null && d.ownedSkill == from.ownedSkill) return d;

        return null;
    }

    /// <summary>
    /// 해당 등급의 풀. 가챠가 뽑을 때 씁니다.
    ///
    /// ★ 반환 타입이 List 가 아니라 IReadOnlyList 인 이유
    ///   List 를 그대로 내주면 받는 쪽이 .Add / .Clear 로 에셋 내용을 바꿀 수 있습니다.
    ///   ScriptableObject 는 에디터 플레이 중에 바뀐 내용이 '에셋 파일에 그대로 남기' 때문에
    ///   실수 한 번이 영구적인 데이터 손상이 됩니다. 읽기 전용으로 막아 둡니다.
    ///   (Count 와 [i] 는 그대로 쓸 수 있어서 가챠 코드는 거의 안 바뀝니다)
    /// </summary>
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

    /// <summary>
    /// 모든 등급의 동료를 차례대로 돌려줍니다. 도감이 씁니다.
    ///
    /// ★ yield return 은 '리스트를 새로 만들지 않고' 하나씩 꺼내 주는 문법입니다.
    ///   네 리스트를 합친 새 List 를 만들면 호출할 때마다 메모리를 할당하지만,
    ///   이 방식은 foreach 가 요청할 때마다 다음 원소를 건네주기만 합니다.
    /// </summary>
    public IEnumerable<CompanionData> All()
    {
        foreach (var d in normal)    yield return d;
        foreach (var d in rare)      yield return d;
        foreach (var d in epic)      yield return d;
        foreach (var d in legendary) yield return d;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터에서 값을 바꿀 때마다 에디터가 부릅니다. (빌드에는 포함되지 않음)
    ///
    /// ★ 등급 칸과 동료의 grade 가 다르면 경고합니다.
    ///   가챠는 '칸' 기준으로 확률을 매기고, 도감은 동료의 'grade' 로 표시합니다.
    ///   둘이 다르면 "전설이라고 적혀 있는데 일반 확률로 나오는" 동료가 생깁니다.
    /// </summary>
    private void OnValidate()
    {
        CheckGrade(normal,    CompanionGrade.Normal);
        CheckGrade(rare,      CompanionGrade.Rare);
        CheckGrade(epic,      CompanionGrade.Epic);
        CheckGrade(legendary, CompanionGrade.Legendary);

        CheckDuplicateSkill(rare,      CompanionGrade.Rare);
        CheckDuplicateSkill(epic,      CompanionGrade.Epic);
        CheckDuplicateSkill(legendary, CompanionGrade.Legendary);
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

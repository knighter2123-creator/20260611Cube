using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 동료 시너지 — 보관 · 재계산 · 조회. (계산 규칙 자체는 SynergyCalculator)
///
/// ★ 왜 별도 매니저가 아니라 CompanionManager 의 partial 인가
///   시너지는 '배치' 로 정해지고, 배치 데이터(occupied)의 주인이 CompanionManager 입니다.
///   배치가 바뀌는 바로 그 메서드 끝에서 다시 계산하면 놓치는 경로가 생기지 않고,
///   매니저끼리의 실행 순서(Awake/Start)도 신경 쓸 필요가 없습니다.
///
/// ★ 시너지는 저장하지 않습니다.
///   배치 + CompanionData 로 언제든 다시 계산되는 '파생 상태' 라서 SaveData 에 필드가 없습니다.
///   (저장해 두면 세이브 값과 실제 배치가 어긋나는 순간이 생길 뿐입니다)
///
/// 재계산 시점 — CompanionManager.cs 에서 "★ 시너지" 로 검색하세요.
///   PlaceCompanion / RetrieveCompanion / RestoreIntoScene / ReplaceCompanion / RemoveCompanion / ApplyFrom
/// </summary>
public partial class CompanionManager
{
    /// <summary>시너지 정의 에셋을 자동으로 읽어 올 Resources 하위 폴더.</summary>
    public const string SYNERGY_RESOURCES_PATH = "Synergy";

    [Header("시너지 정의 (비워 두면 Resources/Synergy 폴더에서 자동으로 읽음)")]
    [SerializeField] private List<SynergyDefinition> synergyDefinitions = new List<SynergyDefinition>();

    private List<SynergyDefinition> resolvedSynergyDefs;
    private readonly List<CompanionData> synergyBuffer = new List<CompanionData>(8);

    /// <summary>지금 배치 기준의 시너지 결과. 배치가 바뀔 때마다 새 객체로 교체됩니다.</summary>
    public SynergyResult Synergy { get; private set; } = SynergyResult.Empty;

    /// <summary>시너지를 다시 계산할 때마다 발생합니다 (값이 같아도 발생 — 받는 쪽은 그냥 다시 그리면 됩니다).</summary>
    public event Action<SynergyResult> OnSynergyChanged;

    /// <summary>사용 중인 시너지 정의 목록 (처음 필요할 때 한 번 정합니다).</summary>
    public IReadOnlyList<SynergyDefinition> SynergyDefinitions
    {
        get
        {
            if (resolvedSynergyDefs != null) return resolvedSynergyDefs;

            resolvedSynergyDefs = new List<SynergyDefinition>();
            if (synergyDefinitions != null)
                foreach (var d in synergyDefinitions)
                    if (d != null) resolvedSynergyDefs.Add(d);

            if (resolvedSynergyDefs.Count == 0)
                resolvedSynergyDefs.AddRange(Resources.LoadAll<SynergyDefinition>(SYNERGY_RESOURCES_PATH));

            if (resolvedSynergyDefs.Count == 0)
                Debug.LogWarning($"[CompanionManager] 시너지 정의가 없습니다. Resources/{SYNERGY_RESOURCES_PATH} 폴더에 " +
                                 "SynergyDefinition 에셋을 두거나 인스펙터에 연결하세요. (시너지 없이 동작합니다)");

            return resolvedSynergyDefs;
        }
    }

    /// <summary>
    /// 스킬 · 스탯이 읽는 창구. 매니저가 없으면(씬 단독 테스트 등) 0.
    /// ★ static 인 이유: 호출하는 쪽마다 "Instance 가 있나" 를 검사하면 한 곳은 반드시 빠뜨립니다.
    /// </summary>
    public static float SynergyValue(SynergyStat stat)
    {
        CompanionManager cm = Instance;
        return cm != null ? cm.Synergy.Get(stat) : 0f;   // 유니티식 null 검사 (파괴된 매니저도 걸러냄)
    }

    /// <summary>지금 맵에 배치된 동료(occupied)로 시너지를 다시 계산하고 알립니다.</summary>
    private void RecalculateSynergy()
    {
        synergyBuffer.Clear();
        foreach (var kv in occupied)
        {
            Companion c = kv.Value;
            if (c != null && c.Data != null) synergyBuffer.Add(c.Data);
        }

        Synergy = SynergyCalculator.Compute(synergyBuffer, SynergyDefinitions);
        GameLog.Info($"[CompanionManager] 시너지 재계산 — 배치 {synergyBuffer.Count}명 / 활성 {DescribeActive(Synergy)}");

        // ★ 구독자(HUD · 스탯창)에서 예외가 나도 배치 흐름은 끝까지 가야 합니다.
        //   이 메서드는 PlaceCompanion '안에서' 불리므로, 예외가 그대로 올라가면
        //   배치 컨트롤러의 저장(SaveManager.Save) 같은 뒤쪽 처리가 통째로 건너뛰어집니다.
        //   구독자마다 따로 감싸서 하나가 터져도 나머지는 받게 합니다.
        Action<SynergyResult> handlers = OnSynergyChanged;
        if (handlers == null) return;

        foreach (Delegate d in handlers.GetInvocationList())
        {
            try { ((Action<SynergyResult>)d)(Synergy); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }

    private static string DescribeActive(SynergyResult r)
    {
        if (!r.AnyActive) return "없음";

        var parts = new List<string>();
        foreach (SynergyState s in r.States)
            if (s.IsActive) parts.Add($"{s.Definition.displayName}({s.Count})");
        return string.Join(", ", parts);
    }
}

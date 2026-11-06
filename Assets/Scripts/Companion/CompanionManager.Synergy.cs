using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>동료 시너지 보관·재계산·조회(규칙은 SynergyCalculator) — 배치 데이터의 주인인 CompanionManager 의 partial 이라 배치가 바뀌는 메서드 끝에서 바로 재계산, 배치로 언제든 다시 구해지는 파생 상태라 저장 안 함.</summary>
// 재계산 시점: PlaceCompanion / RetrieveCompanion / RestoreIntoScene / ReplaceCompanion / RemoveCompanion / ApplyFrom
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

    /// <summary>스킬·스탯이 읽는 창구(매니저가 없으면 0) — static 이라 호출하는 쪽마다 Instance 검사를 빠뜨릴 일이 없음.</summary>
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

        // 구독자마다 따로 감쌈 — 배치 도중 불리므로 예외가 올라가면 뒤쪽 저장까지 건너뜀
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

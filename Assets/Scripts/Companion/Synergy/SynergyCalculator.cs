using System.Collections.Generic;

/// <summary>배치된 동료 → 시너지 결과(상태 없는 static 순수 함수) — 실행 순서와 무관하고 가상 배치 미리보기·씬 없는 테스트도 가능, 보관·이벤트는 CompanionManager.Synergy.</summary>
public static class SynergyCalculator
{
    /// <param name="placed">맵에 배치된 동료 데이터. null · 중복 id 는 무시합니다.</param>
    /// <param name="defs">시너지 정의 목록. null 항목은 무시합니다.</param>
    public static SynergyResult Compute(IEnumerable<CompanionData> placed, IReadOnlyList<SynergyDefinition> defs)
    {
        if (defs == null || defs.Count == 0) return SynergyResult.Empty;

        // ── 1) 계열별 인원 수 — id 기준 한 번씩만 (같은 동료 두 명으로 단계가 오르지 않게) ──
        var counts  = new Dictionary<SynergyTag, int>();
        var seenIds = new HashSet<string>();

        if (placed != null)
        {
            foreach (CompanionData data in placed)
            {
                if (data == null) continue;
                if (!string.IsNullOrEmpty(data.id) && !seenIds.Add(data.id)) continue;

                SynergyTag tag = SynergyTags.Of(data);
                counts.TryGetValue(tag, out int c);
                counts[tag] = c + 1;
            }
        }

        // ── 2) 시너지마다 단계 판정 + 능력치 합산 ──
        float[] stats  = new float[SynergyResult.StatCount];
        var     states = new List<SynergyState>(defs.Count);

        foreach (SynergyDefinition def in defs)
        {
            if (def == null) continue;

            int count = CountFor(def, counts);
            PickTier(def, count, out SynergyTier active, out int next);

            if (active != null && active.effects != null)
            {
                foreach (SynergyEffect e in active.effects)
                {
                    int i = (int)e.stat;
                    if ((uint)i < (uint)stats.Length) stats[i] += e.value;
                }
            }

            states.Add(new SynergyState(def, count, active, next));
        }

        // HUD 순서 — List.Sort 는 불안정 정렬이라 sortOrder 가 같으면 이름으로 한 번 더 비교
        states.Sort((a, b) =>
        {
            int c = a.Definition.sortOrder.CompareTo(b.Definition.sortOrder);
            return c != 0 ? c : string.CompareOrdinal(a.Definition.name, b.Definition.name);
        });

        return new SynergyResult(stats, states.ToArray());
    }

    private static int CountFor(SynergyDefinition def, Dictionary<SynergyTag, int> counts)
    {
        if (def.conditionType == SynergyDefinition.ConditionType.EveryTag)
        {
            // 가장 적게 배치된 계열의 수 — 하나라도 0 이면 0
            int min = int.MaxValue;
            foreach (SynergyTag t in SynergyTags.All)
            {
                counts.TryGetValue(t, out int c);
                if (c < min) min = c;
            }
            return min == int.MaxValue ? 0 : min;
        }

        counts.TryGetValue(def.tag, out int n);
        return n;
    }

    /// <summary>인원 수로 적용 단계·다음 단계 선택 (단계 목록 순서가 섞여도 맞게 동작).</summary>
    private static void PickTier(SynergyDefinition def, int count, out SynergyTier active, out int next)
    {
        active = null;
        next   = -1;
        if (def.tiers == null) return;

        int activeReq = 0;
        foreach (SynergyTier t in def.tiers)
        {
            if (t == null) continue;

            int req = t.requiredCount < 1 ? 1 : t.requiredCount;   // 0 이하로 적으면 '아무도 없어도 켜짐' 이 되므로 1 로 봅니다

            if (req <= count)
            {
                if (active == null || req > activeReq) { active = t; activeReq = req; }
            }
            else if (next < 0 || req < next)
            {
                next = req;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public class CompanionFragment : MonoBehaviour
{
    public static CompanionFragment Instance;

    // ★ id 기준으로 조각 관리 (이름 X)
    private Dictionary<string, int> fragments = new Dictionary<string, int>();

    public event Action<string, int> OnFragmentChanged; // (companionId, 현재 조각 수)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddFragment(CompanionData data, int amount = 1)
    {
        if (data == null) return;

        if (!fragments.ContainsKey(data.id))
            fragments[data.id] = 0;

        fragments[data.id] += amount;
        OnFragmentChanged?.Invoke(data.id, fragments[data.id]);

        Debug.Log($"[Fragment] {data.companionName}({data.id}) 조각 +{amount} → 현재 {fragments[data.id]}개");
    }

    public int GetFragment(CompanionData data)
    {
        if (data == null) return 0;
        return fragments.TryGetValue(data.id, out int count) ? count : 0;
    }

    public Dictionary<string, int> GetAllFragments() => fragments;

    // ── 세이브 연동 ────────────────────────────────

    public void CaptureTo(SaveData d)
    {
        // ★ 빈 스냅샷 가드: 조각이 하나도 없으면 덮어쓰지 않음
        if (fragments.Count == 0) return;

        d.companionFragments.Clear();
        foreach (var kv in fragments)
            d.companionFragments.Add(new FragmentEntry { companionId = kv.Key, count = kv.Value });
    }

    public void ApplyFrom(SaveData d)
    {
        if (d == null || d.companionFragments == null) return;

        fragments.Clear();
        foreach (var e in d.companionFragments)
        {
            if (string.IsNullOrEmpty(e.companionId)) continue;
            fragments[e.companionId] = e.count;
            OnFragmentChanged?.Invoke(e.companionId, e.count);
        }
    }
}
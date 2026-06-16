using System;
using System.Collections.Generic;
using UnityEngine;

public class CompanionFragment : MonoBehaviour
{
    public static CompanionFragment Instance;

    // 동료 이름 기준으로 조각 수량 관리
    private Dictionary<string, int> fragments = new Dictionary<string, int>();

    public event Action<string, int> OnFragmentChanged; // 동료 이름, 현재 조각 수

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void AddFragment(CompanionData data, int amount = 1)
    {
        if (!fragments.ContainsKey(data.companionName))
            fragments[data.companionName] = 0;

        fragments[data.companionName] += amount;
        OnFragmentChanged?.Invoke(data.companionName, fragments[data.companionName]);

        Debug.Log($"[Fragment] {data.companionName} 조각 +{amount} → 현재 {fragments[data.companionName]}개");
    }

    public int GetFragment(CompanionData data)
    {
        return fragments.TryGetValue(data.companionName, out int count) ? count : 0;
    }

    public Dictionary<string, int> GetAllFragments() => fragments;
}
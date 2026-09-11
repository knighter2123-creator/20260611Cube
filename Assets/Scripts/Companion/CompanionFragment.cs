using System;
using System.Collections.Generic;
using UnityEngine;

public class CompanionFragment : MonoBehaviour
{
    public static CompanionFragment Instance;

    // ★ id 기준으로 조각 관리 (이름 X)
    private Dictionary<string, int> fragments = new Dictionary<string, int>();

    /// <summary>세이브를 한 번이라도 확인했는가. CaptureTo 가드용.</summary>
    private bool loaded;

    public event Action<string, int> OnFragmentChanged; // (companionId, 현재 조각 수)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ★ 추가 — 자기 데이터는 자기가 불러온다.
        //
        //   원래는 CompanionManager.Awake 가 대신 불러주고 있었습니다.
        //     CompanionFragment.Instance?.ApplyFrom(SaveManager.Instance.Current);
        //
        //   그런데 Awake 끼리는 실행 순서가 보장되지 않습니다.
        //   CompanionManager 가 먼저 돌면 그 시점에 Instance 가 아직 null 이라
        //   ?. 가 조용히 건너뛰고, 조각은 그 세션 내내 복원되지 않습니다.
        //   다시 불러줄 곳도 없습니다.
        //
        //   ★ CompanionManager.Awake 의 그 줄은 삭제하세요.
        //     여기서 스스로 처리하면 두 매니저 사이의 순서 의존이 통째로 사라집니다.
        if (SaveManager.Instance != null)
        {
            if (SaveManager.Instance.HasSave())
                ApplyFrom(SaveManager.Instance.Current);

            // 세이브가 없어도 "확인은 끝났다". 신규 유저의 0개도 정상 상태다.
            loaded = true;
        }
        else
        {
            Debug.LogError("[Fragment] SaveManager 가 아직 준비되지 않아 조각을 복원하지 못했습니다. " +
                           "Project Settings → Script Execution Order 에서 SaveManager 를 -100 으로 지정하세요.");
        }
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
        if (d == null) return;

        // ★ 기존 가드(fragments.Count == 0)를 loaded 로 바꾼 이유
        //
        //   개수 가드는 "0개일 때"만 막아줍니다.
        //   복원에 실패해 0개인 상태에서 새 조각을 1개 얻으면 Count 가 1이 되어
        //   가드를 통과하고, 아래 Clear() 가 저장돼 있던 조각 전체를 지운 뒤 그 1개만 씁니다.
        //   → "조각이 안 보인다"가 "조각이 전부 사라졌다"로 번집니다.
        //
        //   loaded 는 "세이브를 확인했는가"를 묻기 때문에 개수가 변해도 판단이 흔들리지 않습니다.
        //   상태(개수)가 아니라 사건(불러왔는가)을 기준으로 삼는 것이 핵심입니다.
        if (!loaded) return;

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

        loaded = true;
    }
}
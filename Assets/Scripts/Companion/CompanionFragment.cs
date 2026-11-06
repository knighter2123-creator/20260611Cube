using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>동료 성장 매니저: 중복 가챠 조각 → 조각 100개로 성급 +1(최대까지) → 최대 성급에서 조각으로 위 등급·같은 스킬 동료로 진화 — 조각 차감과 성급 증가가 한 번에 저장되게 한 매니저에.</summary>
public class CompanionFragment : MonoBehaviour
{
    public static CompanionFragment Instance;

    // ★ id 기준으로 관리 (이름 X)
    private readonly Dictionary<string, int> fragments = new Dictionary<string, int>();
    private readonly Dictionary<string, int> stars     = new Dictionary<string, int>();   // 2성 이상만 들어 있음

    /// <summary>세이브를 한 번이라도 확인했는가. CaptureTo 가드용.</summary>
    private bool loaded;

    public event Action<string, int> OnFragmentChanged; // (companionId, 현재 조각 수)
    public event Action<string, int> OnStarChanged;     // (companionId, 현재 성급)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 자기 데이터는 자기가 불러옴 (다른 매니저가 대신 부르면 Awake 순서에 따라 조용히 건너뛸 수 있음)
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

    // ── 조각 ──

    public void AddFragment(CompanionData data, int amount = 1)
    {
        if (data == null || string.IsNullOrEmpty(data.id) || amount <= 0) return;

        int next = GetFragment(data) + amount;
        SetFragment(data.id, next);
        GameLog.Info($"[Fragment] {data.companionName}({data.id}) 조각 +{amount} → 현재 {next}개");
    }

    public int GetFragment(CompanionData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return 0;
        return fragments.TryGetValue(data.id, out int count) ? count : 0;
    }


    private void SetFragment(string id, int count)
    {
        fragments[id] = count;
        OnFragmentChanged?.Invoke(id, count);
    }

    // ── 성급 ──

    /// <summary>현재 성급 (1 ~ MAX_STAR). 기록이 없으면 1성.</summary>
    public int GetStar(CompanionData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return CompanionStar.MIN_STAR;
        return stars.TryGetValue(data.id, out int star) ? star : CompanionStar.MIN_STAR;
    }

    public bool IsMaxStar(CompanionData data) => GetStar(data) >= CompanionStar.MAX_STAR;

    /// <summary>지금 성급을 올릴 수 있는가 — 보유 중 + 최대 성급 아님 + 조각 충분.</summary>
    public bool CanStarUp(CompanionData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return false;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null || !cm.IsOwned(data.id)) return false;   // 조각만 있고 동료는 없는 경우는 막음

        return !IsMaxStar(data) && GetFragment(data) >= CompanionStar.FRAGMENTS_PER_STAR;
    }

    /// <summary>조각을 써서 성급 +1 하고 바로 저장 (조건이 안 되면 아무것도 안 바꾸고 false) — 스킬 피해·쿨다운에 즉시 반영.</summary>
    public bool TryStarUp(CompanionData data)
    {
        if (!CanStarUp(data)) return false;

        int nextStar = GetStar(data) + 1;

        // 차감과 상승을 같은 프레임에 하고, 저장도 한 번에 — 둘 중 하나만 남는 일이 없게
        SetFragment(data.id, GetFragment(data) - CompanionStar.FRAGMENTS_PER_STAR);
        stars[data.id] = nextStar;
        OnStarChanged?.Invoke(data.id, nextStar);

        SaveManager.Instance?.Save();

        GameLog.Info($"[Fragment] {data.companionName}({data.id}) 성급 상승 → {nextStar}성 " +
                  $"(조각 -{CompanionStar.FRAGMENTS_PER_STAR}, 남은 조각 {GetFragment(data)})");
        return true;
    }

    // ── 진화 — 최대 성급에서 조각 FRAGMENTS_PER_STAR 개로 '바로 위 등급' 동료가 됨 ──

    /// <summary>진화 가능한가(보유·최대 성급·조각 충분·진화 대상 있음) — target 은 조각이 모자라도 채워 줌(버튼 미리보기용).</summary>
    public bool CanEvolve(CompanionData data, CompanionPoolAsset pool, out CompanionData target)
    {
        target = pool != null ? pool.FindEvolution(data) : null;
        if (target == null || data == null) return false;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null || !cm.IsOwned(data.id)) return false;

        return IsMaxStar(data) && GetFragment(data) >= CompanionStar.FRAGMENTS_PER_STAR;
    }

    /// <summary>진화: 조각을 쓰고 원래 동료는 사라짐 — 대상이 없으면 같은 자리(칸)에서 대상 1성으로, 있으면 대상에 중복 조각 지급 (남은 조각은 원래 동료 기록에 보존).</summary>
    public bool TryEvolve(CompanionData data, CompanionPoolAsset pool, out CompanionData target)
    {
        if (!CanEvolve(data, pool, out target)) return false;

        CompanionManager cm = CompanionManager.Instance;
        bool alreadyOwned = cm.IsOwned(target.id);

        // 동료 목록부터 바꿉니다 — 실패하면 조각·성급은 건드리지 않은 채 끝나야 하므로
        bool changed = alreadyOwned ? cm.RemoveCompanion(data) : cm.ReplaceCompanion(data, target);
        if (!changed) return false;

        SetFragment(data.id, GetFragment(data) - CompanionStar.FRAGMENTS_PER_STAR);

        // 원래 동료의 성급 기록을 지웁니다 — 다시 뽑으면 1성부터 시작
        if (stars.Remove(data.id)) OnStarChanged?.Invoke(data.id, CompanionStar.MIN_STAR);

        int gained = 0;
        if (alreadyOwned)
        {
            gained = pool.DuplicateFragments(target.grade);
            AddFragment(target, gained);
        }

        SaveManager.Instance?.Save();

        GameLog.Info(alreadyOwned
            ? $"[Fragment] {data.companionName} 진화 → 이미 보유한 {target.companionName} 조각 +{gained}"
            : $"[Fragment] {data.companionName} 진화 → {target.companionName} 1성 획득");
        return true;
    }

    // ── 세이브 연동 ──

    public void CaptureTo(SaveData d)
    {
        if (d == null) return;

        // 개수가 아닌 loaded 로 막음 — '0개일 때만'이면 복원 실패 후 조각 1개를 얻는 순간 저장된 전체를 덮어씀
        if (!loaded) return;

        d.companionFragments.Clear();
        foreach (var kv in fragments)
            d.companionFragments.Add(new FragmentEntry { companionId = kv.Key, count = kv.Value });

        if (d.companionStars == null) d.companionStars = new List<StarEntry>();
        d.companionStars.Clear();
        foreach (var kv in stars)
            d.companionStars.Add(new StarEntry { companionId = kv.Key, star = kv.Value });
    }

    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;

        fragments.Clear();
        if (d.companionFragments != null)
        {
            foreach (var e in d.companionFragments)
            {
                if (string.IsNullOrEmpty(e.companionId)) continue;
                SetFragment(e.companionId, e.count);
            }
        }

        stars.Clear();
        if (d.companionStars != null)
        {
            foreach (var e in d.companionStars)
            {
                if (string.IsNullOrEmpty(e.companionId)) continue;

                // 손상된 값이나 MAX_STAR 를 낮춘 뒤의 옛 세이브도 범위 안으로 맞춥니다.
                int star = Mathf.Clamp(e.star, CompanionStar.MIN_STAR, CompanionStar.MAX_STAR);
                if (star <= CompanionStar.MIN_STAR) continue;   // 1성은 기록하지 않음

                stars[e.companionId] = star;
                OnStarChanged?.Invoke(e.companionId, star);
            }
        }

        loaded = true;
    }
}

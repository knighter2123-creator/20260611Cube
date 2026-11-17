using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>동료 성장 — 조각(인벤토리) 100개로 성급 +1, 최대 성급에서 위 등급으로 진화. 성급은 세이브에.</summary>
public class CompanionFragment : MonoBehaviour
{
    public static CompanionFragment Instance;

    private readonly Dictionary<string, int> stars = new Dictionary<string, int>();   // id → 성급 (2성 이상만)

    /// <summary>세이브를 확인했는가 (CaptureTo 가드).</summary>
    private bool loaded;

    public event Action<string, int> OnStarChanged;     // (companionId, 현재 성급)

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 자기 데이터는 자기가 불러옴
        if (SaveManager.Instance != null)
        {
            if (SaveManager.Instance.HasSave())
                ApplyFrom(SaveManager.Instance.Current);

            // 세이브가 없어도 "확인은 끝났다". 신규 유저의 1성도 정상 상태다.
            loaded = true;
        }
        else
        {
            Debug.LogError("[Fragment] SaveManager 가 아직 준비되지 않아 성급을 복원하지 못했습니다. " +
                           "Project Settings → Script Execution Order 에서 SaveManager 를 -100 으로 지정하세요.");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 조각 (인벤토리) ──

    public void AddFragment(CompanionData data, int amount = 1)
    {
        if (data == null || string.IsNullOrEmpty(data.id) || amount <= 0) return;

        Inventory.Add(ItemIds.Fragment(data.id), amount);
        GameLog.Info($"[Fragment] {data.companionName}({data.id}) 조각 +{amount} → 현재 {GetFragment(data)}개");
    }

    public int GetFragment(CompanionData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return 0;
        return Inventory.Count(ItemIds.Fragment(data.id));
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

    /// <summary>조각을 써서 성급 +1 하고 바로 저장.</summary>
    public bool TryStarUp(CompanionData data)
    {
        if (!CanStarUp(data)) return false;
        if (!Inventory.TrySpend(ItemIds.Fragment(data.id), CompanionStar.FRAGMENTS_PER_STAR)) return false;

        int nextStar = GetStar(data) + 1;
        stars[data.id] = nextStar;
        OnStarChanged?.Invoke(data.id, nextStar);

        SaveManager.Instance?.Save();
        Inventory.FlushSoon();   // 조각 차감도 서버에 바로

        GameLog.Info($"[Fragment] {data.companionName}({data.id}) 성급 상승 → {nextStar}성 " +
                  $"(조각 -{CompanionStar.FRAGMENTS_PER_STAR}, 남은 조각 {GetFragment(data)})");
        return true;
    }

    // ── 진화 ──

    /// <summary>진화 가능한가 — target 은 조각이 모자라도 채워 줌 (버튼 표시용).</summary>
    public bool CanEvolve(CompanionData data, CompanionPoolAsset pool, out CompanionData target)
    {
        target = pool != null ? pool.FindEvolution(data) : null;
        if (target == null || data == null) return false;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null || !cm.IsOwned(data.id)) return false;

        return IsMaxStar(data) && GetFragment(data) >= CompanionStar.FRAGMENTS_PER_STAR;
    }

    /// <summary>원래 동료는 사라지고 대상 1성이 됨 (이미 있으면 대상 조각 지급).</summary>
    public bool TryEvolve(CompanionData data, CompanionPoolAsset pool, out CompanionData target)
    {
        if (!CanEvolve(data, pool, out target)) return false;

        CompanionManager cm = CompanionManager.Instance;
        bool alreadyOwned = cm.IsOwned(target.id);

        // 동료 목록부터 — 실패하면 조각·성급은 그대로
        bool changed = alreadyOwned ? cm.RemoveCompanion(data) : cm.ReplaceCompanion(data, target);
        if (!changed) return false;

        Inventory.TrySpend(ItemIds.Fragment(data.id), CompanionStar.FRAGMENTS_PER_STAR);

        // 다시 뽑으면 1성부터
        if (stars.Remove(data.id)) OnStarChanged?.Invoke(data.id, CompanionStar.MIN_STAR);

        int gained = 0;
        if (alreadyOwned)
        {
            gained = pool.DuplicateFragments(target.grade);
            AddFragment(target, gained);
        }

        SaveManager.Instance?.Save();
        Inventory.FlushSoon();

        GameLog.Info(alreadyOwned
            ? $"[Fragment] {data.companionName} 진화 → 이미 보유한 {target.companionName} 조각 +{gained}"
            : $"[Fragment] {data.companionName} 진화 → {target.companionName} 1성 획득");
        return true;
    }

    // ── 세이브 (성급만) ──

    public void CaptureTo(SaveData d)
    {
        if (d == null || !loaded) return;   // 복원 전 저장이 성급을 덮어쓰지 않게

        if (d.companionStars == null) d.companionStars = new List<StarEntry>();
        d.companionStars.Clear();
        foreach (var kv in stars)
            d.companionStars.Add(new StarEntry { companionId = kv.Key, star = kv.Value });
    }

    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;

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

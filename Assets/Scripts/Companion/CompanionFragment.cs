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

    public enum StarUpOutcome { NotAllowed, Success, Failed }

    /// <summary>성급 올리기 결과 — Failed 면 spent 개 소모, refunded 개는 돌려받음(애초에 빼지 않음).</summary>
    public struct StarUpResult
    {
        public StarUpOutcome outcome;
        public int newStar;
        public int spent;
        public int refunded;
    }

    /// <summary>지금 성급을 올릴 수 있는가 — 보유 중 + 최대 성급 아님 + 조각 충분 + 서버 확률을 받음.</summary>
    public bool CanStarUp(CompanionData data)
    {
        if (data == null || string.IsNullOrEmpty(data.id)) return false;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null || !cm.IsOwned(data.id)) return false;   // 조각만 있고 동료는 없는 경우는 막음

        return !IsMaxStar(data)
            && GetFragment(data) >= CompanionStar.FRAGMENTS_PER_STAR
            && StarUpChart.TryGet(data.grade, GetStar(data), out _);
    }

    /// <summary>이번 성급 올리기의 성공 확률 % (차트에 없으면 false).</summary>
    public bool TryGetStarUpRate(CompanionData data, out float rate)
    {
        rate = 0f;
        if (data == null || !StarUpChart.TryGet(data.grade, GetStar(data), out StarUpRow row)) return false;
        rate = row.rate;
        return true;
    }

    /// <summary>
    /// 조각 FRAGMENTS_PER_STAR 개를 걸고 차트 확률로 성급 +1 시도 후 바로 저장.
    /// 실패하면 refundMin~refundMax 중 랜덤 개수를 돌려받고 나머지만 소모.
    /// </summary>
    public StarUpResult TryStarUp(CompanionData data)
    {
        var result = new StarUpResult { outcome = StarUpOutcome.NotAllowed };
        if (!CanStarUp(data) || !StarUpChart.TryGet(data.grade, GetStar(data), out StarUpRow row)) return result;

        int  cost    = CompanionStar.FRAGMENTS_PER_STAR;
        bool success = UnityEngine.Random.value * 100f < row.rate;   // rate 100 이면 항상, 0 이면 절대 성공 안 함

        int refund = 0;
        if (!success)
        {
            int min = Mathf.Clamp(row.refundMin, 0, cost);
            int max = Mathf.Clamp(row.refundMax, min, cost);
            refund  = UnityEngine.Random.Range(min, max + 1);
        }
        int spent = cost - refund;

        // 환급분은 빼지 않음 — 뺐다가 다시 넣으면 인벤토리 '새 아이템' 레드닷이 켜짐
        if (spent > 0 && !Inventory.TrySpend(ItemIds.Fragment(data.id), spent)) return result;

        result.spent    = spent;
        result.refunded = refund;

        if (success)
        {
            int nextStar = GetStar(data) + 1;
            stars[data.id] = nextStar;
            OnStarChanged?.Invoke(data.id, nextStar);
            result.outcome = StarUpOutcome.Success;
            result.newStar = nextStar;
        }
        else
        {
            result.outcome = StarUpOutcome.Failed;
            result.newStar = GetStar(data);
        }

        SaveManager.Instance?.Save();
        Inventory.FlushSoon();   // 조각 차감도 서버에 바로

        GameLog.Info(success
            ? $"[Fragment] {data.companionName}({data.id}) 성급 상승 성공({row.rate}%) → {result.newStar}성 (조각 -{spent}, 남은 조각 {GetFragment(data)})"
            : $"[Fragment] {data.companionName}({data.id}) 성급 상승 실패({row.rate}%) — 조각 -{spent}, 환급 {refund} (남은 조각 {GetFragment(data)})");
        return result;
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

    /// <summary>
    /// 대상 1성을 얻음 (이미 있으면 대상 조각 지급).
    /// 진화 뒤에도 원래 동료 조각이 남으면 원래 동료는 1성으로 남고, 조각이 0개가 될 때만 사라짐.
    /// </summary>
    public bool TryEvolve(CompanionData data, CompanionPoolAsset pool, out CompanionData target)
    {
        if (!CanEvolve(data, pool, out target)) return false;

        CompanionManager cm = CompanionManager.Instance;
        bool alreadyOwned = cm.IsOwned(target.id);
        bool keepOriginal = GetFragment(data) - CompanionStar.FRAGMENTS_PER_STAR > 0;   // 남은 조각을 계속 쓸 수 있게

        // 동료 목록부터 — 실패하면 조각·성급은 그대로
        bool changed;
        if (keepOriginal) changed = alreadyOwned || cm.AddCompanion(target);   // 원래 동료는 그대로, 대상만 추가
        else              changed = alreadyOwned ? cm.RemoveCompanion(data) : cm.ReplaceCompanion(data, target);
        if (!changed) return false;

        Inventory.TrySpend(ItemIds.Fragment(data.id), CompanionStar.FRAGMENTS_PER_STAR);

        // 남았든 사라졌든 원래 동료는 1성부터
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
        if (keepOriginal)
            GameLog.Info($"[Fragment] {data.companionName} 조각 {GetFragment(data)}개가 남아 1성으로 유지");
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

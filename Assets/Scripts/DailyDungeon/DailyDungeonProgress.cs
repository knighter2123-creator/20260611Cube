using System;
using UnityEngine;

/// <summary>
/// 일일 던전 기록 — 오늘 남은 입장 횟수 / 클리어한 최고 난이도.
///
/// [저장 — 매니저 오브젝트가 없는 이유]
///   GachaTicket 과 같은 방식입니다. 값은 SaveData.dailyDungeon 에만 있고 SaveManager.Current 를 직접 읽고 씁니다.
///   StageScene(입장 UI)과 DailyDungeonScene(클리어 기록) 양쪽에서 쓰기 때문에
///   씬 오브젝트로 두면 어느 씬에 둘지 애매해지고, 사본이 생기면 둘이 어긋납니다.
///
/// [하루의 기준]
///   미션과 같은 오전 6시입니다 (MissionManager.GetDailyPeriodStart 를 그대로 씀).
///   기록을 읽을 때마다 "지금 기간의 시작 시각" 과 비교해서, 기간이 넘어갔으면 횟수를 0으로 돌립니다.
///   그래서 앱을 켜 둔 채 6시를 넘겨도, 다음에 입장 UI 가 갱신될 때 바로 반영됩니다.
///
/// [입장 횟수 추가]
///   던전마다 하루 extraEntryCosts 길이만큼 재화로 횟수를 추가할 수 있습니다 (기본 300 → 500 → 700 → 1000 → 1500).
///   추가한 횟수도 오전 6시에 함께 초기화됩니다.
///
/// [클리어 / 소탕]
///   실제 클리어(DailyDungeonManager)와 소탕은 둘 다 CompleteClear 를 거칩니다.
///   → 입장 횟수 1회 차감 + 미션 진행 + 보상 지급 + 클리어 기록 + 저장이 한 곳에서 같은 순서로 일어납니다.
///   소탕은 이미 클리어한 난이도(최고 클리어 이하)만 가능합니다.
///
/// [난이도 해금]
///   난이도 1은 항상 열려 있고, N 난이도를 깨면 N+1 이 열립니다.
/// </summary>
public static class DailyDungeonProgress
{
    /// <summary>기록이 바뀔 때 (클리어 / 소탕 / 횟수 추가 / 일일 초기화). UI 갱신용.</summary>
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    private static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Current : null;

    // ── 조회 ───────────────────────────────────────

    /// <summary>오늘 남은 입장 횟수 (기본 + 추가 - 사용). 세이브를 읽을 수 없으면 0.</summary>
    public static int RemainingEntries(DailyDungeonData dungeon)
    {
        if (dungeon == null || Data == null) return 0;
        DailyDungeonRecord r = GetRecord(dungeon, create: false);
        int used = r != null ? r.usedToday : 0;
        return Mathf.Max(0, TotalEntries(dungeon) - used);
    }

    /// <summary>오늘 입장 가능한 총 횟수 = 하루 기본 횟수 + 오늘 추가한 횟수. "남은 횟수 2/4" 의 분모.</summary>
    public static int TotalEntries(DailyDungeonData dungeon)
        => dungeon != null ? dungeon.dailyEntries + PurchasedToday(dungeon) : 0;

    /// <summary>오늘 재화로 추가한 입장 횟수.</summary>
    public static int PurchasedToday(DailyDungeonData dungeon)
    {
        DailyDungeonRecord r = GetRecord(dungeon, create: false);
        return r != null ? r.purchasedToday : 0;
    }

    /// <summary>오늘 더 추가할 수 있는 횟수.</summary>
    public static int RemainingPurchases(DailyDungeonData dungeon)
        => dungeon != null ? Mathf.Max(0, dungeon.MaxExtraEntries - PurchasedToday(dungeon)) : 0;

    /// <summary>다음 1회 추가 비용. 오늘 더 추가할 수 없으면 -1.</summary>
    public static int NextPurchaseCost(DailyDungeonData dungeon)
        => dungeon != null ? dungeon.ExtraEntryCost(PurchasedToday(dungeon)) : -1;

    /// <summary>다음 1회 추가 비용을 낼 재화가 있는지.</summary>
    public static bool CanAffordNextPurchase(DailyDungeonData dungeon)
    {
        int cost = NextPurchaseCost(dungeon);
        if (cost < 0) return false;
        CurrencyManager cm = CurrencyManager.Instance;
        return cm != null && cm.Has(dungeon.extraEntryCostType, cost);
    }

    /// <summary>클리어한 최고 난이도 (0 = 없음).</summary>
    public static int HighestCleared(DailyDungeonData dungeon)
    {
        DailyDungeonRecord r = GetRecord(dungeon, create: false);
        return r != null ? r.highestCleared : 0;
    }

    /// <summary>도전할 수 있는 최고 난이도 = 최고 클리어 + 1 (최대 난이도에서 멈춤).</summary>
    public static int MaxUnlockedLevel(DailyDungeonData dungeon)
        => dungeon != null ? dungeon.ClampLevel(HighestCleared(dungeon) + 1) : 1;

    public static bool IsLevelUnlocked(DailyDungeonData dungeon, int level)
        => dungeon != null && level >= 1 && level <= MaxUnlockedLevel(dungeon);

    /// <summary>소탕 가능한지 — 이미 클리어한 난이도이고 남은 입장 횟수가 있어야 합니다.</summary>
    public static bool CanSweep(DailyDungeonData dungeon, int level)
        => dungeon != null && level >= 1 && level <= HighestCleared(dungeon) && RemainingEntries(dungeon) > 0;

    // ── 변경 ───────────────────────────────────────

    /// <summary>
    /// 클리어 처리 (실제 클리어 / 소탕 공통): 입장 횟수 1회 차감 + 미션 진행 + 보상 지급 + 클리어 기록 + 저장.
    /// 반환값은 입장 횟수를 차감했는지입니다. 남은 횟수가 없어도 보상과 기록은 남깁니다
    /// (던전 씬을 단독 실행해 테스트할 때를 위해 — 정상 경로에서는 입장 UI 가 횟수를 확인하고 들여보냄).
    /// </summary>
    public static bool CompleteClear(DailyDungeonData dungeon, int level)
    {
        if (dungeon == null || Data == null) return false;

        bool consumed = ConsumeEntry(dungeon);

        CurrencyManager cm = CurrencyManager.Instance;
        if (cm != null) cm.AddCurrency(dungeon.rewardType, dungeon.RewardFor(level));
        else Debug.LogWarning("[DailyDungeon] CurrencyManager 가 없어 보상을 지급하지 못했습니다. LoginScene 부터 실행하세요.");

        DailyDungeonRecord r = GetRecord(dungeon, create: true);
        if (level > r.highestCleared) r.highestCleared = dungeon.ClampLevel(level);

        // 재화(CurrencyManager.CaptureTo) / 소환권(SaveData 직접) / 미션(MissionManager.CaptureTo) 모두 Save() 한 번으로 기록
        SaveManager.Instance?.Save();

        OnChanged?.Invoke();
        return consumed;
    }

    /// <summary>
    /// 소탕 — 던전을 진행하지 않고 CompleteClear 합니다. 조건(CanSweep)이 안 맞거나 보상을 줄 수 없으면
    /// 아무것도 바꾸지 않고 false.
    /// </summary>
    public static bool TrySweep(DailyDungeonData dungeon, int level)
    {
        if (dungeon == null || Data == null) return false;
        if (!CanSweep(dungeon, level))
        {
            Debug.Log($"[DailyDungeon] 소탕 불가 — {dungeon.id} Lv.{level} " +
                      $"(최고 클리어 {HighestCleared(dungeon)}, 남은 횟수 {RemainingEntries(dungeon)})");
            return false;
        }

        // 실제 클리어와 달리, 보상을 못 주는 상태에서는 횟수만 날아가지 않도록 미리 막습니다.
        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning("[DailyDungeon] CurrencyManager 가 없어 소탕 보상을 지급할 수 없습니다.");
            return false;
        }

        CompleteClear(dungeon, level);
        Debug.Log($"[DailyDungeon] 소탕 — {dungeon.displayName} Lv.{level} : {dungeon.DescribeReward(level)} " +
                  $"(남은 횟수 {RemainingEntries(dungeon)}/{TotalEntries(dungeon)})");
        return true;
    }

    /// <summary>
    /// 재화를 내고 오늘 입장 횟수를 1회 추가합니다. 하루 최대 횟수를 넘었거나 재화가 부족하면 아무것도 바꾸지 않고 false.
    /// 차감과 기록이 함께 남도록 여기서 바로 저장합니다 (ShopManager.TryPurchase 와 같은 방식).
    /// </summary>
    public static bool TryPurchaseEntry(DailyDungeonData dungeon)
    {
        if (dungeon == null || Data == null) return false;

        int cost = NextPurchaseCost(dungeon);
        if (cost < 0)
        {
            Debug.Log($"[DailyDungeon] 횟수 추가 불가 — 오늘 최대({dungeon.MaxExtraEntries}회)까지 추가함 ({dungeon.id})");
            return false;
        }

        CurrencyManager cm = CurrencyManager.Instance;
        if (cost > 0 && (cm == null || !cm.TrySpendCurrency(dungeon.extraEntryCostType, cost)))
        {
            Debug.Log($"[DailyDungeon] 횟수 추가 실패 — {dungeon.DescribeCost(cost)} 부족 ({dungeon.id})");
            return false;
        }

        GetRecord(dungeon, create: true).purchasedToday++;
        SaveManager.Instance?.Save();

        Debug.Log($"[DailyDungeon] 입장 횟수 추가 — {dungeon.id} ({dungeon.DescribeCost(cost)}, " +
                  $"오늘 {PurchasedToday(dungeon)}/{dungeon.MaxExtraEntries}회)");
        OnChanged?.Invoke();
        return true;
    }

    // ── 내부 ───────────────────────────────────────

    /// <summary>입장 횟수 1회 차감 + 미션 진행. 남은 횟수가 없으면 아무것도 바꾸지 않고 false. (이벤트/저장은 부른 쪽이)</summary>
    private static bool ConsumeEntry(DailyDungeonData dungeon)
    {
        if (RemainingEntries(dungeon) <= 0) return false;

        GetRecord(dungeon, create: true).usedToday++;
        MissionManager.Instance?.ReportDailyDungeonClear();
        return true;
    }

    private static DailyDungeonRecord GetRecord(DailyDungeonData dungeon, bool create)
    {
        SaveData data = Data;
        if (dungeon == null || data == null) return null;

        if (data.dailyDungeon == null) data.dailyDungeon = new DailyDungeonSaveData();
        DailyDungeonSaveData dd = data.dailyDungeon;

        CheckDailyReset(dd);

        foreach (DailyDungeonRecord r in dd.records)
            if (r != null && r.dungeonId == dungeon.id) return r;

        if (!create) return null;

        var added = new DailyDungeonRecord { dungeonId = dungeon.id };
        dd.records.Add(added);
        return added;
    }

    /// <summary>기간(오전 6시 ~ 다음 날 6시)이 바뀌었으면 모든 던전의 오늘 입장/추가 횟수를 0으로.</summary>
    private static void CheckDailyReset(DailyDungeonSaveData dd)
    {
        long periodStart = MissionManager.GetDailyPeriodStart(DateTime.Now).Ticks;
        if (dd.lastResetTicks >= periodStart) return;

        foreach (DailyDungeonRecord r in dd.records)
            if (r != null) { r.usedToday = 0; r.purchasedToday = 0; }

        dd.lastResetTicks = periodStart;
        Debug.Log("[DailyDungeon] 일일 입장 횟수 초기화");
        OnChanged?.Invoke();
    }
}

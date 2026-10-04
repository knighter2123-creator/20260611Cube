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
///   미션과 같은 오전 6시입니다 (MissionManager.ResetHour).
///   기록을 읽을 때마다 "지금 기간의 시작 시각" 과 비교해서, 기간이 넘어갔으면 횟수를 0으로 돌립니다.
///   그래서 앱을 켜 둔 채 6시를 넘겨도, 다음에 입장 UI 가 갱신될 때 바로 반영됩니다.
///
/// [난이도 해금]
///   난이도 1은 항상 열려 있고, N 난이도를 깨면 N+1 이 열립니다.
/// </summary>
public static class DailyDungeonProgress
{
    private const int ResetHour = 6;   // MissionManager.ResetHour 와 맞춤

    /// <summary>기록이 바뀔 때 (입장 / 클리어 / 일일 초기화). UI 갱신용.</summary>
    public static event Action OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    private static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Current : null;

    // ── 조회 ───────────────────────────────────────

    /// <summary>오늘 남은 입장 횟수. 세이브를 읽을 수 없으면 0.</summary>
    public static int RemainingEntries(DailyDungeonData dungeon)
    {
        DailyDungeonRecord r = GetRecord(dungeon, create: false);
        if (dungeon == null || Data == null) return 0;
        int used = r != null ? r.usedToday : 0;
        return Mathf.Max(0, dungeon.dailyEntries - used);
    }

    /// <summary>클리어한 최고 난이도 (0 = 없음).</summary>
    public static int HighestCleared(DailyDungeonData dungeon)
    {
        DailyDungeonRecord r = GetRecord(dungeon, create: false);
        return r != null ? r.highestCleared : 0;
    }

    /// <summary>도전할 수 있는 최고 난이도 = 최고 클리어 + 1 (최대 난이도에서 멈춤).</summary>
    public static int MaxUnlockedLevel(DailyDungeonData dungeon)
    {
        if (dungeon == null) return 1;
        return dungeon.ClampLevel(HighestCleared(dungeon) + 1);
    }

    public static bool IsLevelUnlocked(DailyDungeonData dungeon, int level)
        => dungeon != null && level >= 1 && level <= MaxUnlockedLevel(dungeon);

    // ── 변경 ───────────────────────────────────────

    /// <summary>
    /// 입장 횟수 1회 차감 (클리어 시 DailyDungeonManager 가 호출). 남은 횟수가 없으면 아무것도 바꾸지 않고 false.
    /// 저장은 부른 쪽이 합니다 (보상 지급과 함께 Save() 한 번으로 기록).
    /// </summary>
    public static bool TryConsumeEntry(DailyDungeonData dungeon)
    {
        if (dungeon == null || Data == null) return false;
        if (RemainingEntries(dungeon) <= 0) return false;

        GetRecord(dungeon, create: true).usedToday++;
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>클리어 기록. 더 높은 난이도일 때만 갱신합니다.</summary>
    public static void RecordClear(DailyDungeonData dungeon, int level)
    {
        if (dungeon == null || Data == null) return;

        DailyDungeonRecord r = GetRecord(dungeon, create: true);
        if (level > r.highestCleared) r.highestCleared = dungeon.ClampLevel(level);
        OnChanged?.Invoke();
    }

    // ── 내부 ───────────────────────────────────────

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

    /// <summary>기간(오전 6시 ~ 다음 날 6시)이 바뀌었으면 모든 던전의 오늘 입장 횟수를 0으로.</summary>
    private static void CheckDailyReset(DailyDungeonSaveData dd)
    {
        long periodStart = GetDailyPeriodStart(DateTime.Now).Ticks;
        if (dd.lastResetTicks >= periodStart) return;

        foreach (DailyDungeonRecord r in dd.records)
            if (r != null) r.usedToday = 0;

        dd.lastResetTicks = periodStart;
        Debug.Log("[DailyDungeon] 일일 입장 횟수 초기화");
        OnChanged?.Invoke();
    }

    // 가장 최근의 오전 6시 (그 전이면 어제 6시) — MissionManager.GetDailyPeriodStart 와 같은 계산
    private static DateTime GetDailyPeriodStart(DateTime now)
    {
        DateTime resetToday = new DateTime(now.Year, now.Month, now.Day, ResetHour, 0, 0);
        return now < resetToday ? resetToday.AddDays(-1) : resetToday;
    }
}

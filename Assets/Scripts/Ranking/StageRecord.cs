using System;
using UnityEngine;

/// <summary>랭킹용 최고 클리어 기록(진행도 1-1=1, 2-1=11…) — 실패 시 내려가는 currentStage 대신 '가장 멀리 깬 곳'을 SaveData 에 따로 둠.</summary>
public static class StageRecord
{
    /// <summary>최고 기록이 갱신된 순간 호출됩니다. (RankingManager 업로드용)</summary>
    public static event Action OnImproved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnImproved = null;
    }

    private static SaveData Data => SaveManager.Instance != null ? SaveManager.Instance.Current : null;

    /// <summary>최고 클리어 진행도(0=없음) — 필드가 없던 옛 세이브도 맞도록 '현재 스테이지 앞까지 깬 개수'와 비교해 큰 값.</summary>
    public static int BestProgress
    {
        get
        {
            SaveData d = Data;
            if (d == null) return 0;
            return Mathf.Max(d.bestClearedProgress, StageFormula.Progress(d.currentWorld, d.currentStage));
        }
    }

    public static bool HasRecord => BestProgress > 0;

    /// <summary>기록을 세운 시각 (UTC unix ms). 이관된 기록처럼 시각이 없으면 지금으로 채웁니다.</summary>
    public static long BestClearedAt
    {
        get
        {
            SaveData d = Data;
            if (d == null) return 0;
            if (d.bestClearedAt == 0 && HasRecord) d.bestClearedAt = NowMs();
            return d.bestClearedAt;
        }
    }

    /// <summary>StageClear 에서 방금 깬 스테이지로(currentStage++ 전) 호출 — 기록이 갱신되면 OnImproved 를 알리고 true.</summary>
    public static bool ReportClear(int world, int stage)
    {
        SaveData d = Data;
        if (d == null) return false;

        int cleared = StageFormula.Progress(world, stage) + 1;
        if (cleared <= BestProgress) return false;

        d.bestClearedProgress = cleared;
        d.bestClearedAt       = NowMs();

        OnImproved?.Invoke();
        return true;
    }

    // ───────── 표시 ─────────

    /// <summary>진행도 → "월드-스테이지" (예: 13 → "2-3"). 0 이면 "-".</summary>
    public static string Format(int progress)
    {
        if (progress <= 0) return "-";
        int index = progress - 1;   // 0 = 1-1
        int world = index / StageFormula.MaxStagePerWorld + 1;
        int stage = index % StageFormula.MaxStagePerWorld + 1;
        return $"{world}-{stage}";
    }

    public static void Split(int progress, out int world, out int stage)
    {
        int index = Mathf.Max(0, progress - 1);
        world = index / StageFormula.MaxStagePerWorld + 1;
        stage = index % StageFormula.MaxStagePerWorld + 1;
    }

    private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

using System;
using UnityEngine;

/// <summary>
/// 최고 클리어 스테이지 기록 — 랭킹에 올라가는 값.
///
/// [왜 currentWorld/currentStage 를 그대로 쓰지 않나]
///   스테이지 실패 시 현재 스테이지가 한 단계 내려갑니다(StageManager.StageFail).
///   그 값을 올리면 실패할 때마다 랭킹이 떨어지므로, "가장 멀리 깬 곳" 을 따로 기록합니다.
///
/// 저장 위치: SaveData.bestClearedProgress / bestClearedAt
///   PlayerProfile 과 같은 방식 — 값을 따로 들고 있지 않고 매번 SaveManager.Current 를 읽습니다.
///   StageClear 직후 StageManager 가 Save() 를 부르므로 따로 저장하지 않습니다.
///
/// [진행도 숫자]  1-1 클리어 = 1, 1-10 클리어 = 10, 2-1 클리어 = 11 … (0 = 아직 없음)
/// </summary>
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

    /// <summary>
    /// 최고 클리어 진행도. 0 = 없음.
    ///
    /// ★ 기존 유저 이관: 이 필드가 생기기 전 세이브는 0 입니다.
    ///   지금 스테이지에 와 있다는 건 그 앞 스테이지를 전부 깼다는 뜻이므로
    ///   StageFormula.Progress(현재) (= 현재 앞까지 깬 개수) 와 비교해 큰 값을 씁니다.
    /// </summary>
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

    /// <summary>
    /// StageManager.StageClear 에서 "방금 깬" 월드·스테이지로 호출합니다 (currentStage++ 이전).
    /// 기록이 갱신되면 true.
    /// </summary>
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

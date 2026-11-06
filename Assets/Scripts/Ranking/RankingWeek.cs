using System;

/// <summary>랭킹 주차 번호(매주 월 06:00 KST 에 +1) — 모두 같은 순간에 정산하도록 기기 시간대가 아닌 KST 고정, RankingManager 의 재업로드 판단용.</summary>
// 뒤끝 콘솔 주간 랭킹 초기화 시각(월 06:00)과 맞춰 둘 것
public static class RankingWeek
{
    /// <summary>0주차 시작 = 2024-01-01(월) 06:00 KST = 2023-12-31 21:00 UTC</summary>
    public const long AnchorMs = 1704056400000L;
    public const long WeekMs   = 7L * 24 * 60 * 60 * 1000;

    private static readonly TimeSpan Kst = TimeSpan.FromHours(9);

    /// <summary>지금(기기 시계)의 주차. 기기 시계가 틀려도 보안 규칙이 서버 시각으로 다시 확인합니다.</summary>
    public static int Current => Of(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    public static int Of(long utcMs) => (int)Math.Floor((utcMs - AnchorMs) / (double)WeekMs);

    /// <summary>그 주차가 시작된 시각 (한국 시간).</summary>
    public static DateTimeOffset StartOf(int week)
        => DateTimeOffset.FromUnixTimeMilliseconds(AnchorMs + week * WeekMs).ToOffset(Kst);
}

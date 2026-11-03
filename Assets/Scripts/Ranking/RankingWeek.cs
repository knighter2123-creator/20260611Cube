using System;

/// <summary>
/// 랭킹 주차 번호 — 매주 월요일 오전 6시(한국 시간)에 1씩 오릅니다.
///
/// [왜 MissionManager 의 주간 계산을 쓰지 않나]
///   미션은 기기 시간대(DateTime.Now) 기준이라 기기마다 경계가 다를 수 있습니다.
///   랭킹은 모든 플레이어가 같은 순간에 정산해야 하므로 한국 시간(UTC+9)으로 고정합니다.
///
/// 지금은 RankingManager 가 "주차가 바뀌면 이번 주 뒤끝 주간 랭킹에 기록을 다시 올림" 판단에 씁니다.
///   뒤끝 콘솔의 주간 랭킹 초기화 시각(월요일 06:00)과 맞춰 두면 초기화 직후 첫 접속 때 바로 다시 올라갑니다.
/// </summary>
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

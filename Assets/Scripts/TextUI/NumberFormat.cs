using System;

/// <summary>큰 숫자 축약 도구(45600000 → "45.6M") — 도메인을 몰라도 되는 일이라 공용 static 순수 함수로 분리해 HUD·상점·보상 등이 한 형식을 공유.</summary>
public static class NumberFormat
{
    // 1,000 배 단위표 — K M B T 다음은 방치형 관행대로 aa, ab, ac …
    private static readonly string[] Units =
    {
        "", "K", "M", "B", "T",
        "aa", "ab", "ac", "ad", "ae", "af", "ag", "ah", "ai", "aj",
        "ak", "al", "am", "an", "ao", "ap", "aq", "ar", "as", "at"
    };

    /// <summary>축약 시작 기준 100,000 — 1,000 부터 줄이면 초반에 강화해도 숫자가 안 바뀌어 보임, 화면이 좁아지는 건 여섯 자리부터.</summary>
    public const double DEFAULT_ABBREVIATE_FROM = 100_000d;

    /// <summary>읽기 좋은 문자열로 — 20 → "20", 1776.25 → "1,776"(콤마·소수 버림), 45600000 → "45.6M"(기준 이상 축약).</summary>
    /// <param name="value">변환할 값</param>
    /// <param name="decimals">축약됐을 때 보여줄 소수점 자리수 (기본 2)</param>
    /// <param name="abbreviateFrom">이 값 이상일 때부터 K/M/B 로 축약 (기본 100,000)</param>
    public static string Short(double value, int decimals = 2,
                               double abbreviateFrom = DEFAULT_ABBREVIATE_FROM)
    {
        // 음수는 부호를 떼고 계산 후 다시 붙임 (자리수 계산 어긋남 방지)
        bool negative = value < 0;
        if (negative) value = -value;

        if (value < abbreviateFrom)
        {
            // 1,000 이상은 소수 버리고 콤마, 미만은 소수 유지 (치명타 1.75배·확률 3.5% 등)
            string small = value >= 1000d
                ? ((long)Math.Round(value)).ToString("N0")
                : value.ToString("0.##");

            return negative ? "-" + small : small;
        }

        int unitIndex = 0;

        // 1000 으로 계속 나누면서 몇 번 나눴는지를 셉니다. 그게 단위표의 인덱스가 됩니다.
        while (value >= 1000d && unitIndex < Units.Length - 1)
        {
            value /= 1000d;
            unitIndex++;
        }

        string body = value.ToString("F" + decimals);

        // "1.00K" 처럼 소수점이 의미 없을 때 꼬리를 잘라 "1K" 로 만듭니다.
        if (body.Contains("."))
            body = body.TrimEnd('0').TrimEnd('.');

        string result = body + Units[unitIndex];
        return negative ? "-" + result : result;
    }

    /// <summary>천 단위 콤마만 넣습니다. 레벨·경험치처럼 정확한 값을 보여줘야 할 때.</summary>
    public static string Comma(long value) => value.ToString("N0");

    /// <summary>"1,234 / 5,000" 형태. 경험치 바 라벨용.</summary>
    public static string Ratio(long current, long max) => $"{Comma(current)} / {Comma(max)}";

    /// <summary>항상 부호를 붙임("+500", 0 도 "+0" — 자리가 비면 레이아웃이 흔들림).</summary>
    public static string Signed(double value, int decimals = 2)
        => (value < 0 ? "-" : "+") + Short(Math.Abs(value), decimals);
}

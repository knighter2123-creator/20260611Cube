using System;

/// <summary>
/// 큰 숫자를 사람이 읽을 수 있게 줄여주는 범용 도구.
///   1234        → "1.23K"
///   45600000    → "45.6M"
///
/// ─────────────────────────────────────────────────────────────
/// [왜 별도 static 클래스로 빼는가]
///
/// 원래 이 기능은 AugmentCards.Currency.cs 안의 AugmentReward.FormatNumber() 에만
/// 있었습니다. 그런데 "1234567 을 1.23M 으로 바꾸는 일"은 증강이라는 개념을
/// 전혀 몰라도 되는 작업입니다.
///
///   판단 기준 — 이 함수가 우리 도메인(증강 / 스탯창)을 몰라도 되는가?
///              몰라도 된다면 밖으로 빼는 게 맞습니다.
///
/// AugmentUIFactory 를 뺀 것과 정확히 같은 판단입니다.
/// 앞으로 골드 HUD, 스탯창, 상점, 오프라인 보상 정산 전부 같은 형식이 필요합니다.
/// 각자 자기 파일에 복사해두면 나중에 "K 를 천으로 바꾸자"는 요구가 왔을 때
/// 고쳐야 할 곳이 다섯 군데가 됩니다.
///
/// MonoBehaviour 가 아니라 static 클래스인 이유: 상태가 없으니까요.
/// 입력이 같으면 출력도 항상 같은 순수 함수(pure function)라서
/// 씬에 올릴 필요도, 인스턴스를 만들 필요도 없습니다.
/// ─────────────────────────────────────────────────────────────
/// </summary>
public static class NumberFormat
{
    // 1,000 배씩 올라가는 단위표.
    // K(천) M(백만) B(십억) T(조) 이후는 방치형 관행대로 aa, ab, ac … 를 씁니다.
    private static readonly string[] Units =
    {
        "", "K", "M", "B", "T",
        "aa", "ab", "ac", "ad", "ae", "af", "ag", "ah", "ai", "aj",
        "ak", "al", "am", "an", "ao", "ap", "aq", "ar", "as", "at"
    };

    /// <summary>
    /// 축약을 시작하는 기준값.
    ///
    /// [왜 1,000 이 아니라 100,000 인가]
    /// 1,000 부터 줄이면 `1776 → "1.78K"` 가 됩니다. 자릿수는 줄지만
    /// 초반 플레이어에게는 76 이 사라진 셈이고, 강화를 한 번 눌렀을 때
    /// 숫자가 안 바뀌는 것처럼 보이기도 합니다.
    /// 화면이 좁아지는 건 여섯 자리부터라서, 그때까지는 정확한 값을 보여주는 편이 낫습니다.
    /// (이 게임은 배율이 1.5^n 이라 후반에는 어차피 K/M/B 로 넘어갑니다)
    /// </summary>
    public const double DEFAULT_ABBREVIATE_FROM = 100_000d;

    /// <summary>
    /// 숫자를 읽기 좋게 문자열로 만듭니다.
    ///   20        → "20"
    ///   1776.25   → "1,776"        (천 단위 콤마, 소수점 버림)
    ///   45600000  → "45.6M"        (기준값 이상은 축약)
    /// </summary>
    /// <param name="value">변환할 값</param>
    /// <param name="decimals">축약됐을 때 보여줄 소수점 자리수 (기본 2)</param>
    /// <param name="abbreviateFrom">이 값 이상일 때부터 K/M/B 로 축약 (기본 100,000)</param>
    public static string Short(double value, int decimals = 2,
                               double abbreviateFrom = DEFAULT_ABBREVIATE_FROM)
    {
        // 음수도 안전하게 처리합니다. 부호를 떼어내고 계산한 뒤 다시 붙입니다.
        // (음수를 신경 쓰지 않으면 자리수 계산이 어긋납니다)
        bool negative = value < 0;
        if (negative) value = -value;

        if (value < abbreviateFrom)
        {
            // 1,000 이상은 소수점이 의미 없으니 버리고 콤마만 넣습니다. (1,776)
            // 1,000 미만은 소수점을 살립니다. 치명타 배수 1.75, 확률 3.5% 같은 값이 여기 옵니다.
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

    /// <summary>
    /// 부호를 항상 붙입니다. 세부 내역에서 "+500" 처럼 기여분을 보여줄 때 씁니다.
    /// 0 이면 "+0" 이 되도록 두었습니다 — 자리가 비면 레이아웃이 흔들리기 때문입니다.
    /// </summary>
    public static string Signed(double value, int decimals = 2)
        => (value < 0 ? "-" : "+") + Short(Math.Abs(value), decimals);
}

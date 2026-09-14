using System.Text;

/// <summary>
/// 큰 수를 "73만 2800", "12억 3456만" 처럼 한국식 만 단위로 표시합니다.
/// 강화 비용뿐 아니라 골드 HUD, 상점, 보상 팝업에서도 그대로 쓸 수 있습니다.
/// </summary>
public static class KoreanNumberFormatter
{
    private const long MAN = 10_000L;              // 만
    private const long EOK = MAN * MAN;            // 억 (1e8)
    private const long JO  = EOK * MAN;            // 조 (1e12)

    // ★ 매 프레임 호출될 수 있으므로 StringBuilder 를 재사용합니다.
    //   (UI 갱신은 전부 메인 스레드에서만 일어나므로 static 공유로 안전합니다.
    //    혹시 다른 스레드에서 부를 일이 생기면 이 캐시를 지역 변수로 바꾸세요.)
    private static readonly StringBuilder sb = new StringBuilder(24);

    private static readonly long[]   Units   = { JO, EOK, MAN };
    private static readonly string[] Suffixes = { "조", "억", "만" };

    /// <param name="value">표시할 값</param>
    /// <param name="maxSegments">보여줄 자리 개수. 2면 "12억 3456만"까지만 (그 아래는 생략)</param>
    public static string Format(long value, int maxSegments = 2)
    {
        if (value == 0) return "0";
        if (value < 0)  return "-" + Format(-value, maxSegments);
        if (value < MAN) return value.ToString();

        sb.Clear();
        long rest = value;
        int  used = 0;

        for (int i = 0; i < Units.Length; i++)
        {
            if (rest < Units[i]) continue;

            long q = rest / Units[i];
            rest  %= Units[i];

            if (used > 0) sb.Append(' ');
            sb.Append(q).Append(Suffixes[i]);
            used++;

            if (used >= maxSegments) return sb.ToString();
        }

        // 만 미만 나머지 (예: 73만 "2800")
        if (rest > 0 && used < maxSegments)
        {
            if (used > 0) sb.Append(' ');
            sb.Append(rest);
        }

        return sb.ToString();
    }
}

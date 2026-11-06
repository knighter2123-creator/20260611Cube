using TMPro;

/// <summary>자주 바뀌는 숫자를 TMP 에 넣는 도우미 — 문자열 보간 대신 할당 없는 SetText("{0} / {1}") 사용(초당 수십 번 갱신의 GC 방지), 2^24 초과만 string.Format.</summary>
public static class TmpTextExtensions
{
    private const long ExactFloatLimit = 16_777_216;

    private static bool FitsFloat(long v) => v > -ExactFloatLimit && v < ExactFloatLimit;

    /// <summary>서식 예: "Lv. {0}"</summary>
    public static void SetNumber(this TMP_Text label, string format, long a)
    {
        if (label == null) return;

        if (FitsFloat(a)) label.SetText(format, a);
        else              label.text = string.Format(format, a);
    }

    /// <summary>서식 예: "{0} / {1}"</summary>
    public static void SetNumbers(this TMP_Text label, string format, long a, long b)
    {
        if (label == null) return;

        if (FitsFloat(a) && FitsFloat(b)) label.SetText(format, a, b);
        else                              label.text = string.Format(format, a, b);
    }
}

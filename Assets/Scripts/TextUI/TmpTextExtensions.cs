using TMPro;

/// <summary>
/// 자주 바뀌는 숫자를 TMP 텍스트에 넣을 때 쓰는 도우미.
///
/// ★ [프레임 드랍 수정] $"{a} / {b}" 는 부를 때마다 새 문자열을 만듭니다.
///   처치 수 · 경험치 · 퀘스트 진행도처럼 적을 잡을 때마다 바뀌는 텍스트는 초당 수십 번 불려 GC 가 자주 돕니다.
///   TMP 의 SetText("{0} / {1}", a, b) 는 숫자를 내부 버퍼에 직접 써서 문자열을 만들지 않습니다.
///
///   단, SetText 는 인자를 float 로 받아 2^24(16,777,216) 를 넘는 정수는 끝자리가 틀어집니다.
///   (DamageText.SetAmount 와 같은 이유) 그 범위를 넘으면 string.Format 으로 정확하게 그립니다.
/// </summary>
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

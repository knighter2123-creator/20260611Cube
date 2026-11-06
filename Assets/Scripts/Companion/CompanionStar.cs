using System.Text;

/// <summary>동료 성급 규칙의 한 곳(획득 = 1성, 조각 FRAGMENTS_PER_STAR 개로 +1, 최대 MAX_STAR) — 스킬 수치는 등급×성급 표(GradeStarFloat), 보관·상승은 CompanionFragment.</summary>
public static class CompanionStar
{
    public const int MIN_STAR           = 1;
    public const int MAX_STAR           = 5;
    public const int FRAGMENTS_PER_STAR = 100;

    /// <summary>조각 문구(상세·목록 공용) — 쓸 곳이 남았으면 "조각 37 / 100", 아니면 "조각 37 (최대 성급)".</summary>
    public static string FragmentLabel(int fragments, bool hasNext = true)
        => hasNext
            ? $"조각 {fragments} / {FRAGMENTS_PER_STAR}"
            : $"조각 {fragments} <color=#AAAAAA>(최대 성급)</color>";

    /// <summary>★★★☆☆ 처럼 채운 별 + 빈 별. (폰트에 ★☆ 글리프가 있어야 합니다)</summary>
    public static string ToStars(int star)
    {
        var sb = new StringBuilder(MAX_STAR);
        for (int i = 1; i <= MAX_STAR; i++)
            sb.Append(i <= star ? '★' : '☆');
        return sb.ToString();
    }
}

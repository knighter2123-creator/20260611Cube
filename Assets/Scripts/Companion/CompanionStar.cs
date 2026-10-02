using System.Text;

/// <summary>
/// 동료 성급(★) 규칙. 숫자가 여러 파일에 흩어지지 않게 한 곳에 둡니다.
///
///   획득 = 1성. 조각 FRAGMENTS_PER_STAR 개를 쓰면 1성 오름. 최대 MAX_STAR 성.
///   성급이 오르면 스킬 피해 · 재사용 대기가 스킬 에셋의 '등급×성급 표' 값으로 바뀝니다 (GradeStarFloat).
///
/// 성급 보관·상승은 CompanionFragment(조각과 같은 매니저 — 조각을 쓰는 일과 성급을 올리는 일이 한 번에 일어나야 함)가 합니다.
/// </summary>
public static class CompanionStar
{
    public const int MIN_STAR           = 1;
    public const int MAX_STAR           = 5;
    public const int FRAGMENTS_PER_STAR = 100;

    /// <summary>
    /// 조각 표시 문구. 상세 패널과 동료 목록 탭이 같은 형식을 쓰도록 한 곳에 둡니다.
    ///   hasNext = 조각을 쓸 곳이 남았는가 (성급 상승 또는 진화)
    ///     true  → "조각 37 / 100"
    ///     false → "조각 37 (최대 성급)"
    /// </summary>
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

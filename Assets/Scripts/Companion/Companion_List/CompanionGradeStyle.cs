using UnityEngine;

/// <summary>
/// 동료 등급의 표시 이름과 색. (신규)
///
/// ★ 도감 아이템과 상세창이 둘 다 등급을 그립니다.
///   각자 switch 문을 가지면 나중에 "에픽 색 좀 바꾸자" 할 때 한쪽만 바뀝니다.
///   같은 규칙은 한 곳에 — KoreanNumberFormatter 와 같은 판단입니다.
///   CompanionListItem 도 이제 이 색을 씁니다. (가챠 결과 화면에 등급 색이 따로 있다면 그쪽도 바꾸는 걸 권합니다)
/// </summary>
public static class CompanionGradeStyle
{
    public static string GetName(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => "일반",
            CompanionGrade.Rare      => "희귀",
            CompanionGrade.Epic      => "영웅",
            CompanionGrade.Legendary => "전설",
            _                        => grade.ToString()   // 등급이 새로 추가돼도 최소한 영문 이름은 뜸
        };
    }

    public static Color GetColor(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => new Color(1.00f, 1.00f, 1.00f),  // 흰색 (RGB 255, 255, 255)
            CompanionGrade.Rare      => new Color(0.30f, 0.62f, 1.00f),  // 파랑
            CompanionGrade.Epic      => new Color(0.72f, 0.42f, 1.00f),  // 보라
            CompanionGrade.Legendary => new Color(1.00f, 0.66f, 0.16f),  // 주황
            _                        => Color.black
        };
    }

    /// <summary>
    /// 밝은 배경(동료 목록의 흰 카드) 위에 쓸 등급 색.
    /// GetColor 는 어두운 배경 기준이라 일반(흰색)이 흰 카드 위에서 보이지 않습니다.
    /// 같은 계열의 색을 유지하되 흰 배경에서 읽히도록 어둡게 맞췄습니다.
    /// </summary>
    public static Color GetColorOnLight(CompanionGrade grade)
    {
        return grade switch
        {
            CompanionGrade.Normal    => new Color(0.20f, 0.20f, 0.22f),  // 짙은 회색
            CompanionGrade.Rare      => new Color(0.12f, 0.42f, 0.90f),  // 파랑
            CompanionGrade.Epic      => new Color(0.52f, 0.22f, 0.86f),  // 보라
            CompanionGrade.Legendary => new Color(0.88f, 0.45f, 0.00f),  // 주황
            _                        => Color.black
        };
    }

    /// <summary>TMP 리치 텍스트용 "등급 이름에 색 입히기" — &lt;color=#RRGGBB&gt;전설&lt;/color&gt;</summary>
    /// <remarks>
    /// ★ 결과가 등급마다 항상 같으므로 처음 한 번만 만들고 재사용합니다.
    ///   (예전에는 부를 때마다 색 → 16진수 변환 + 문자열 조립으로 새 문자열이 생겼습니다)
    /// </remarks>
    public static string GetColoredName(CompanionGrade grade)
    {
        if (ColoredNames.TryGetValue(grade, out string cached)) return cached;

        string s = $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor(grade))}>{GetName(grade)}</color>";
        ColoredNames[grade] = s;
        return s;
    }

    private static readonly System.Collections.Generic.Dictionary<CompanionGrade, string> ColoredNames = new();
}

using UnityEngine;

/// <summary>동료 등급 표시 이름·색의 한 곳 — 도감·상세창·목록이 각자 switch 를 가지면 한쪽만 바뀌어서.</summary>
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

    /// <summary>흰 카드 위용 등급 색 — GetColor 는 어두운 배경 기준이라 일반(흰색)이 안 보여 같은 계열을 어둡게.</summary>
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
    /// <remarks>등급마다 결과가 같아 처음 한 번만 만들어 재사용 (매번 만들면 문자열 할당).</remarks>
    public static string GetColoredName(CompanionGrade grade)
    {
        if (ColoredNames.TryGetValue(grade, out string cached)) return cached;

        string s = $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor(grade))}>{GetName(grade)}</color>";
        ColoredNames[grade] = s;
        return s;
    }

    private static readonly System.Collections.Generic.Dictionary<CompanionGrade, string> ColoredNames = new();
}

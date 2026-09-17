using UnityEngine;

/// <summary>
/// 동료 등급의 표시 이름과 색. (신규)
///
/// ★ 도감 아이템과 상세창이 둘 다 등급을 그립니다.
///   각자 switch 문을 가지면 나중에 "에픽 색 좀 바꾸자" 할 때 한쪽만 바뀝니다.
///   같은 규칙은 한 곳에 — KoreanNumberFormatter 와 같은 판단입니다.
///   (가챠 결과 화면이나 CompanionListItem 에 등급 색이 따로 있다면 이걸 쓰게 바꾸는 걸 권합니다)
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
            CompanionGrade.Normal    => new Color(0.78f, 0.78f, 0.78f),  // 회색
            CompanionGrade.Rare      => new Color(0.30f, 0.62f, 1.00f),  // 파랑
            CompanionGrade.Epic      => new Color(0.72f, 0.42f, 1.00f),  // 보라
            CompanionGrade.Legendary => new Color(1.00f, 0.66f, 0.16f),  // 주황
            _                        => Color.white
        };
    }

    /// <summary>TMP 리치 텍스트용 "등급 이름에 색 입히기" — &lt;color=#RRGGBB&gt;전설&lt;/color&gt;</summary>
    public static string GetColoredName(CompanionGrade grade)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor(grade))}>{GetName(grade)}</color>";
}

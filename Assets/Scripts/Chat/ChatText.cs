using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>채팅·친구 UI 공용 글자 도우미(리치 텍스트 색, 남의 글 감싸기, {이름} 채우기) — 색은 기본값, 실제 색은 각 컴포넌트 인스펙터.</summary>
public static class ChatText
{
    public static readonly Color ProgressColor = new Color(1f, 217f / 255f, 51f / 255f);              // #FFD933 진행도 · 구역 제목
    public static readonly Color MyNameColor   = new Color(127f / 255f, 216f / 255f, 1f);             // #7FD8FF
    public static readonly Color NameColor     = Color.white;                                         // #FFFFFF
    public static readonly Color BodyColor     = new Color(216f / 255f, 216f / 255f, 216f / 255f);    // #D8D8D8
    public static readonly Color UnreadColor   = new Color(1f, 123f / 255f, 123f / 255f);             // #FF7B7B 안 읽은 수 · 받은 요청 배지

    public static string NoManager => GameMessages.Chat.noManager;   // 문장은 Resources/GameMessages 에셋에서 고침

    /// <summary>Color → TMP 리치 텍스트 색 ("#RRGGBBAA").</summary>
    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGBA(c);

    public static string Colored(Color color, string text) => $"<color={Hex(color)}>{text}</color>";

    public static StringBuilder AppendColored(this StringBuilder sb, Color color, string text)
        => sb.Append("<color=").Append(Hex(color)).Append('>').Append(text).Append("</color>");

    /// <summary>문구의 {이름} 자리를 값으로 (string.Format 과 달리 중괄호 오타에도 예외 없음) — 예: Fill("친구 {count}/{max}", "count", "3", "max", "50").</summary>
    public static string Fill(string template, params string[] pairs)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        for (int i = 0; i + 1 < pairs.Length; i += 2)
            template = template.Replace("{" + pairs[i] + "}", pairs[i + 1]);
        return template;
    }

    private static readonly Regex NoParseTag = new Regex("</?noparse>", RegexOptions.IgnoreCase);

    /// <summary>남의 글을 noparse 로 감싸 리치 텍스트 장난(size=300 등) 방지 — 글 속 noparse 태그는 다시 생기지 않을 때까지 반복 제거.</summary>
    public static string Plain(string s)
    {
        s = s ?? string.Empty;
        string prev;
        do { prev = s; s = NoParseTag.Replace(s, string.Empty); } while (s != prev);
        return "<noparse>" + s + "</noparse>";
    }
}

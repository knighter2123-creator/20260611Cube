using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 채팅·친구 UI 가 함께 쓰는 글자 도우미 — 색(TMP 리치 텍스트), 다른 사람 글 감싸기, 공통 문구.
/// </summary>
public static class ChatText
{
    public const string ProgressHex = "#FFD933";   // 진행도 · 구역 제목
    public const string MyNameHex   = "#7FD8FF";
    public const string NameHex     = "#FFFFFF";
    public const string BodyHex     = "#D8D8D8";
    public const string UnreadHex   = "#FF7B7B";   // 안 읽은 수 · 받은 요청 배지
    public const string LeftHex     = "#8A8A8A";   // (나감)

    public const string NoManager = "채팅 시스템을 찾을 수 없습니다.";

    public static string Colored(string hex, string text) => $"<color={hex}>{text}</color>";

    public static StringBuilder AppendColored(this StringBuilder sb, string hex, string text)
        => sb.Append("<color=").Append(hex).Append('>').Append(text).Append("</color>");

    private static readonly Regex NoParseTag = new Regex("</?noparse>", RegexOptions.IgnoreCase);

    /// <summary>
    /// 다른 사람이 보낸 글을 리치 텍스트로 해석하지 않게 감쌉니다. (예: "&lt;size=300&gt;" 로 화면을 덮는 장난 방지)
    /// 글 안의 noparse 태그는 지웁니다 — 지운 뒤 다시 태그가 생기는 경우("&lt;nopa&lt;noparse&gt;rse&gt;")까지 반복.
    /// </summary>
    public static string Plain(string s)
    {
        s = s ?? string.Empty;
        string prev;
        do { prev = s; s = NoParseTag.Replace(s, string.Empty); } while (s != prev);
        return "<noparse>" + s + "</noparse>";
    }
}

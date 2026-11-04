/// <summary>
/// 채팅 한 줄. 보낸 순간의 닉네임·진행도를 그대로 담습니다 (나중에 이름을 바꾸거나 기록이 올라도 옛 메시지는 그대로).
/// 어느 서버(IChatService 구현)를 쓰든 이 모양으로 주고받습니다.
/// </summary>
public struct ChatMessage
{
    /// <summary>시스템 메시지(공지)의 Uid.</summary>
    public const string SystemUid = "system";

    /// <summary>보낸 사람 uid 를 모를 때(다른 앱·콘솔에서 보낸 메시지) Uid = 이 접두사 + 닉네임 — 진짜 유저와 겹치지 않게.</summary>
    public const string NameOnlyPrefix = "name:";

    public string Id;         // 서버 메시지 ID (중복 제거용)
    public string Uid;        // 보낸 사람 뒤끝 UID (내 메시지 강조 · 친구 · 차단)
    public string Nickname;   // PlayerProfile.DisplayName
    public int    Progress;   // StageRecord.BestProgress → StageRecord.Format 으로 "2-3"
    public string Text;
    public long   SentAt;     // 보낸 시각 (UTC ms) — 정렬 기준

    /// <summary>진짜 유저의 uid 인가 — 아니면(공지 · uid 모름) 친구 요청 · 개인 메시지 · 차단을 할 수 없음.</summary>
    public bool HasPlayerUid
        => !string.IsNullOrEmpty(Uid) && Uid != SystemUid && !Uid.StartsWith(NameOnlyPrefix, System.StringComparison.Ordinal);
}

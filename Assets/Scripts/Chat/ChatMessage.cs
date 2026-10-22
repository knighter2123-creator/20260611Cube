/// <summary>
/// 채팅 한 줄. 보낸 순간의 닉네임·진행도를 그대로 담습니다 (나중에 이름을 바꾸거나 기록이 올라도 옛 메시지는 그대로).
/// 어느 서버(IChatService 구현)를 쓰든 이 모양으로 주고받습니다.
/// </summary>
public struct ChatMessage
{
    public string Id;         // 서버 문서 ID (중복 제거용)
    public string Uid;        // 보낸 사람 Firebase UID (내 메시지 강조용)
    public string Nickname;   // PlayerProfile.DisplayName
    public int    Progress;   // StageRecord.BestProgress → StageRecord.Format 으로 "2-3"
    public string Text;
    public long   SentAt;     // 보낸 시각 (UTC ms) — 정렬 기준
}

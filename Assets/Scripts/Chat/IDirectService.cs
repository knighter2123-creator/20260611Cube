using System;
using System.Collections.Generic;

/// <summary>대화방에서의 한 사람의 상태.</summary>
public enum RoomState
{
    None,   // 아직 들어온 적 없음 — 첫 메시지를 받으면 들어옴
    In,     // 대화 중 — 탭이 보이고 메시지를 주고받음
    Left,   // [나가기] 또는 차단 — 탭이 사라지고, 상대는 더 보낼 수 없음
}

/// <summary>나와 한 사람 사이의 1:1 대화방. 서버 문서 하나 = 두 사람 사이 대화방 하나.</summary>
public struct DirectRoom
{
    public string    PeerUid;
    public string    PeerName;
    public RoomState MyState;
    public RoomState PeerState;
    public int       Unread;       // 내가 안 읽은 수 (서버에 저장 — 다른 기기에서도 같음)
    public long      LastSentAt;   // 마지막 메시지 시각 (UTC ms), 없으면 방을 연 시각 — 탭 정렬 기준
}

/// <summary>
/// 1:1 개인 메시지를 서버와 주고받는 부분. IChatService.Direct 로 얻습니다 (채팅과 같은 서버·같은 UID).
///
/// [받기]  대화방 목록은 항상 받고(Connect), 메시지는 지금 보고 있는 방 하나만 받습니다(Watch) — 읽기 비용을 줄이려고.
///   다른 방의 새 메시지는 대화방 목록의 Unread / LastSentAt 으로 알 수 있습니다.
/// [약속]  IChatService 와 같음 — 모든 이벤트·콜백은 메인 스레드.
/// </summary>
public interface IDirectService
{
    /// <summary>내 대화방 전체 목록 (나간 방 포함). 바뀔 때마다 전체가 옵니다.</summary>
    event Action<IReadOnlyList<DirectRoom>> RoomsReceived;

    /// <summary>Watch 중인 방의 새 메시지 (peerUid, 메시지). 같은 메시지가 다시 올 수 있습니다 (받는 쪽이 Id 로 거름).</summary>
    event Action<string, IReadOnlyList<ChatMessage>> MessagesReceived;

    /// <summary>Watch 중인 방에서 빠진 메시지 Id (서버가 거절한 내 메시지, 최근 N개 밖으로 밀려난 메시지).</summary>
    event Action<string, IReadOnlyList<string>> MessagesRemoved;

    /// <summary>목록 또는 메시지를 받지 못하게 됐을 때. 받은 뒤엔 목록·메시지 모두 끊긴 상태입니다.</summary>
    event Action<string> Failed;

    void Connect(string myUid);
    void Disconnect();

    /// <summary>그 방의 최근 count 개 메시지를 받기 시작합니다 (이전에 보던 방은 멈춤).</summary>
    void Watch(string myUid, string peerUid, int count);
    void Unwatch();

    /// <summary>대화방을 엽니다 — 없으면 만들고, 내가 나갔던 방이면 다시 들어갑니다. 상대 상태는 바꾸지 않습니다.</summary>
    void Open(string myUid, string myName, string peerUid, string peerName, Action<string> onDone);

    /// <summary>나가기 — 내 탭이 사라지고, 상대는 더 보낼 수 없게 됩니다.</summary>
    void Leave(string myUid, string peerUid, Action<string> onDone);

    /// <summary>보내기. invitePeer = 상대가 아직 들어온 적 없음(None) → 이 메시지로 상대를 방에 넣음.</summary>
    void Send(string myUid, string peerUid, ChatMessage message, bool invitePeer, Action<string> onDone);

    /// <summary>내 안 읽은 수를 0 으로.</summary>
    void MarkRead(string myUid, string peerUid);
}

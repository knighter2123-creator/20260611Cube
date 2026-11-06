using System;
using System.Collections.Generic;

/// <summary>대화방에서의 내 상태.</summary>
public enum RoomState
{
    In,     // 대화 중 — 탭이 보이고 메시지를 주고받음
    Left,   // [나가기] 또는 차단 — 탭이 사라짐 (그 뒤에 새 메시지가 오면 다시 보임)
}

/// <summary>나와 한 사람 사이의 1:1 대화방.</summary>
public struct DirectRoom
{
    public string    PeerUid;
    public string    PeerName;
    public RoomState MyState;
    public int       Unread;       // 내가 안 읽은 수 (BackendDirectService: 이 기기에서 센 값)
    public long      LastSentAt;   // 마지막 메시지 시각 (UTC ms), 없으면 방을 연 시각 — 탭 정렬 기준
}

/// <summary>1:1 개인 메시지 인터페이스(IChatService.Direct) — 대화방 목록은 항상, 메시지는 보고 있는 방 하나만(Watch), 다른 방 새 글은 Unread/LastSentAt 으로 (콜백은 메인 스레드).</summary>
public interface IDirectService
{
    /// <summary>내 대화방 전체 목록 (나간 방 포함). 바뀔 때마다 전체가 옵니다.</summary>
    event Action<IReadOnlyList<DirectRoom>> RoomsReceived;

    /// <summary>Watch 중인 방의 메시지 (peerUid, 메시지). 같은 메시지가 다시 올 수 있습니다 (받는 쪽이 Id 로 거름).</summary>
    event Action<string, IReadOnlyList<ChatMessage>> MessagesReceived;

    /// <summary>처음 목록을 받지 못했을 때. 받은 뒤엔 끊긴 상태입니다 (ChatManager 가 잠시 뒤 다시 연결).</summary>
    event Action<string> Failed;

    void Connect(string myUid);
    void Disconnect();

    /// <summary>대화방 목록과 보고 있는 방의 메시지를 서버에서 다시 받습니다.</summary>
    void Refresh();

    /// <summary>그 방의 최근 count 개 메시지를 받기 시작합니다 (이전에 보던 방은 멈춤).</summary>
    void Watch(string myUid, string peerUid, int count);
    void Unwatch();

    /// <summary>대화방을 엽니다 — 없으면 만들고, 내가 나갔던 방이면 다시 들어갑니다.</summary>
    void Open(string myUid, string peerUid, string peerName, Action<string> onDone);

    /// <summary>나가기 — 내 탭이 사라집니다.</summary>
    void Leave(string myUid, string peerUid, Action<string> onDone);

    void Send(string myUid, string peerUid, ChatMessage message, Action<string> onDone);

    /// <summary>내 안 읽은 수를 0 으로.</summary>
    void MarkRead(string myUid, string peerUid);
}

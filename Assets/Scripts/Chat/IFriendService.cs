using System;
using System.Collections.Generic;

public enum FriendState
{
    Friend,     // 서로 친구
    Incoming,   // 상대가 나에게 요청 — 내가 수락/거절
    Outgoing,   // 내가 상대에게 요청 — 상대의 수락을 기다림
}

/// <summary>나와 한 사람 사이의 친구 관계 (요청 중 포함). 서버 문서 하나 = 두 사람 사이 관계 하나.</summary>
public struct Friendship
{
    public string      PeerUid;
    public string      PeerName;   // 요청·수락 때 남긴 상대 닉네임 (상대가 이름을 바꾸면 상대 기기가 고쳐 씀)
    public string      MyName;     // 이 문서에 남아 있는 내 닉네임 — 지금 이름과 다르면 ChatManager 가 고쳐 씀
    public FriendState State;
}

/// <summary>
/// 친구 요청·수락·삭제와 채팅 차단 목록을 서버와 주고받는 부분. IChatService.Friends 로 얻습니다 (채팅과 같은 서버·같은 UID).
/// 차단 목록도 서버에 두어, 같은 계정이면 어느 기기에서나 같습니다.
/// [약속]  IChatService 와 같음 — 모든 이벤트·콜백은 메인 스레드.
/// </summary>
public interface IFriendService
{
    /// <summary>내 친구 관계 전체 목록. 바뀔 때마다 전체가 옵니다.</summary>
    event Action<IReadOnlyList<Friendship>> Received;

    /// <summary>내 차단 목록 전체 (uid → 차단할 때의 닉네임). 바뀔 때마다 전체가 옵니다.</summary>
    event Action<IReadOnlyDictionary<string, string>> BlocksReceived;

    /// <summary>목록을 받지 못하게 됐을 때. 받은 뒤엔 친구·차단 목록 모두 끊긴 상태입니다.</summary>
    event Action<string> Failed;

    void Connect(string myUid);
    void Disconnect();

    void Request(string myUid, string myName, string peerUid, string peerName, Action<string> onDone);
    void Accept(string myUid, string myName, string peerUid, Action<string> onDone);

    /// <summary>친구 삭제 · 받은 요청 거절 · 보낸 요청 취소 — 모두 관계 문서를 지우는 것.</summary>
    void Remove(string myUid, string peerUid, Action<string> onDone);

    void UpdateMyName(string myUid, string peerUid, string myName);

    void Block(string myUid, string peerUid, string peerName, Action<string> onDone);
    void Unblock(string myUid, string peerUid, Action<string> onDone);
}

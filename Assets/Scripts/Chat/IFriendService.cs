using System;
using System.Collections.Generic;

public enum FriendState
{
    Friend,     // 서로 친구
    Incoming,   // 상대가 나에게 요청 — 내가 수락/거절
    Outgoing,   // 내가 상대에게 요청 — 상대의 수락을 기다림
}

/// <summary>나와 한 사람 사이의 친구 관계 (요청 중 포함).</summary>
public struct Friendship
{
    public string      PeerUid;
    public string      PeerName;   // 상대의 지금 닉네임 (뒤끝이 목록마다 최신 값을 줌)
    public FriendState State;
}

/// <summary>
/// 친구 요청·수락·삭제와 채팅 차단 목록을 서버와 주고받는 부분. IChatService.Friends 로 얻습니다 (채팅과 같은 서버·같은 UID).
/// 차단 목록도 서버에 두어 같은 계정이면 어느 기기에서나 같습니다 (BackendFriendService → BackendBlockStore).
/// [약속]  IChatService 와 같음 — 모든 이벤트·콜백은 메인 스레드.
/// </summary>
public interface IFriendService
{
    /// <summary>내 친구 관계 전체 목록. 바뀔 때마다 전체가 옵니다.</summary>
    event Action<IReadOnlyList<Friendship>> Received;

    /// <summary>내 차단 목록 전체 (uid → 차단할 때의 닉네임). 바뀔 때마다 전체가 옵니다.</summary>
    event Action<IReadOnlyDictionary<string, string>> BlocksReceived;

    /// <summary>처음 목록을 받지 못했을 때. 받은 뒤엔 친구·차단 목록 모두 끊긴 상태입니다.</summary>
    event Action<string> Failed;

    void Connect(string myUid);
    void Disconnect();

    /// <summary>목록을 서버에서 다시 받아 Received 로 보냅니다.</summary>
    void Refresh();

    /// <summary>닉네임(정확히 일치)으로 유저를 찾습니다. onDone(uid, 오류 문구) — 찾으면 오류가 null.</summary>
    void FindUser(string nickname, Action<string, string> onDone);

    void Request(string myUid, string peerUid, string peerName, Action<string> onDone);
    void Accept(string myUid, string peerUid, Action<string> onDone);

    /// <summary>친구 삭제 · 받은 요청 거절 · 보낸 요청 취소.</summary>
    void Remove(string myUid, string peerUid, Action<string> onDone);

    void Block(string myUid, string peerUid, string peerName, Action<string> onDone);
    void Unblock(string myUid, string peerUid, Action<string> onDone);
}

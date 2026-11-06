using System;
using System.Collections.Generic;

/// <summary>서버 없는 채팅 테스트용(보낸 메시지가 나에게만 돌아옴) — ChatManager 의 Backend 를 Local 로 두면 로그인 없이 에디터에서 확인.</summary>
public class LocalChatService : IChatService
{
    public event Action<IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<IReadOnlyList<string>> MessagesRemoved { add { } remove { } }   // 지울 일이 없음
    public event Action<string> Failed { add { } remove { } }                           // 끊길 일이 없음

    public IFriendService Friends { get; } = new LocalFriendService();
    public IDirectService Direct  { get; } = new LocalDirectService();

    /// <summary>로그인 UID, 아니면 "local" — 계정이 바뀌면 값도 바뀌어 ChatManager 가 이전 계정 목록을 비우고 재연결.</summary>
    public bool TryGetIdentity(out string uid, out string reason)
    {
        AuthManager auth = AuthManager.Instance;
        uid    = auth != null && auth.IsSignedIn && !string.IsNullOrEmpty(auth.Uid) ? auth.Uid : "local";
        reason = null;
        return true;
    }

    public void Connect(int historyCount) { }
    public void Disconnect() { }
    public void Tick() { }

    public void Send(ChatMessage message, Action<string> onDone)
    {
        message.Id = Guid.NewGuid().ToString("N");
        MessagesReceived?.Invoke(new[] { message });
        onDone?.Invoke(null);
    }
}

/// <summary>LocalChatService 의 친구·차단 — 상대가 없으므로 요청을 보내면 바로 수락된 것으로 칩니다 (UI 확인용).</summary>
public class LocalFriendService : IFriendService
{
    public event Action<IReadOnlyList<Friendship>> Received;
    public event Action<IReadOnlyDictionary<string, string>> BlocksReceived;
    public event Action<string> Failed { add { } remove { } }

    private readonly Dictionary<string, Friendship> list   = new Dictionary<string, Friendship>();
    private readonly Dictionary<string, string>     blocks = new Dictionary<string, string>();

    public void Connect(string myUid)
    {
        Publish();
        BlocksReceived?.Invoke(new Dictionary<string, string>(blocks));
    }

    public void Disconnect() { }
    public void Refresh() => Publish();

    // 상대가 없는 테스트용 — 닉네임을 그대로 가짜 uid 로
    public void FindUser(string nickname, Action<string, string> onDone) => onDone?.Invoke("local:" + nickname, null);

    public void Request(string myUid, string peerUid, string peerName, Action<string> onDone)
    {
        list[peerUid] = new Friendship
        {
            PeerUid  = peerUid,
            PeerName = peerName,
            State    = FriendState.Friend,
        };
        Done(onDone);
    }

    public void Accept(string myUid, string peerUid, Action<string> onDone)
    {
        if (list.TryGetValue(peerUid, out Friendship f)) { f.State = FriendState.Friend; list[peerUid] = f; }
        Done(onDone);
    }

    public void Remove(string myUid, string peerUid, Action<string> onDone)
    {
        list.Remove(peerUid);
        Done(onDone);
    }

    public void Block(string myUid, string peerUid, string peerName, Action<string> onDone)
    {
        blocks[peerUid] = peerName;
        BlocksReceived?.Invoke(new Dictionary<string, string>(blocks));
        onDone?.Invoke(null);
    }

    public void Unblock(string myUid, string peerUid, Action<string> onDone)
    {
        blocks.Remove(peerUid);
        BlocksReceived?.Invoke(new Dictionary<string, string>(blocks));
        onDone?.Invoke(null);
    }

    private void Done(Action<string> onDone)
    {
        Publish();
        onDone?.Invoke(null);
    }

    private void Publish() => Received?.Invoke(new List<Friendship>(list.Values));
}

/// <summary>LocalChatService 의 개인 메시지 — 상대가 없으므로 내가 보낸 메시지만 그 방에 쌓입니다 (UI 확인용).</summary>
public class LocalDirectService : IDirectService
{
    public event Action<IReadOnlyList<DirectRoom>>          RoomsReceived;
    public event Action<string, IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<string>                             Failed { add { } remove { } }

    private readonly Dictionary<string, DirectRoom>        rooms    = new Dictionary<string, DirectRoom>();
    private readonly Dictionary<string, List<ChatMessage>> messages = new Dictionary<string, List<ChatMessage>>();
    private string watching;

    public void Connect(string myUid) => Publish();
    public void Disconnect() => watching = null;
    public void Refresh() => Publish();

    public void Watch(string myUid, string peerUid, int count)
    {
        watching = peerUid;
        if (messages.TryGetValue(peerUid, out List<ChatMessage> list) && list.Count > 0)
            MessagesReceived?.Invoke(peerUid, list.ToArray());
    }

    public void Unwatch() => watching = null;

    public void Open(string myUid, string peerUid, string peerName, Action<string> onDone)
    {
        DirectRoom room = rooms.TryGetValue(peerUid, out DirectRoom r) ? r : new DirectRoom
        {
            PeerUid    = peerUid,
            PeerName   = peerName,
            LastSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };
        room.MyState   = RoomState.In;
        rooms[peerUid] = room;
        Publish();
        onDone?.Invoke(null);
    }

    public void Leave(string myUid, string peerUid, Action<string> onDone)
    {
        if (rooms.TryGetValue(peerUid, out DirectRoom room)) { room.MyState = RoomState.Left; rooms[peerUid] = room; }
        Publish();
        onDone?.Invoke(null);
    }

    public void Send(string myUid, string peerUid, ChatMessage message, Action<string> onDone)
    {
        message.Id = Guid.NewGuid().ToString("N");
        if (!messages.TryGetValue(peerUid, out List<ChatMessage> list)) messages[peerUid] = list = new List<ChatMessage>();
        list.Add(message);

        if (rooms.TryGetValue(peerUid, out DirectRoom room))
        {
            room.LastSentAt = message.SentAt;
            rooms[peerUid] = room;
        }

        if (watching == peerUid) MessagesReceived?.Invoke(peerUid, new[] { message });
        Publish();
        onDone?.Invoke(null);
    }

    public void MarkRead(string myUid, string peerUid) { }

    private void Publish() => RoomsReceived?.Invoke(new List<DirectRoom>(rooms.Values));
}

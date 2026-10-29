using System;
using System.Collections.Generic;

/// <summary>
/// 서버 없이 채팅 UI 를 확인하는 테스트용. 보낸 메시지가 나에게만 바로 돌아옵니다.
/// (ChatManager 의 Backend 를 Local 로 두면 로그인 없이도 에디터에서 써 볼 수 있습니다)
/// </summary>
public class LocalChatService : IChatService
{
    public event Action<IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<IReadOnlyList<string>> MessagesRemoved { add { } remove { } }   // 지울 일이 없음
    public event Action<string> Failed { add { } remove { } }                           // 끊길 일이 없음

    public IFriendService Friends { get; } = new LocalFriendService();
    public IDirectService Direct  { get; } = new LocalDirectService();

    public bool TryGetIdentity(out string uid, out string reason)
    {
        uid    = "local";
        reason = null;
        return true;
    }

    public void Connect(int historyCount) { }
    public void Disconnect() { }

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

    public void Request(string myUid, string myName, string peerUid, string peerName, Action<string> onDone)
    {
        list[peerUid] = new Friendship
        {
            PeerUid  = peerUid,
            PeerName = peerName,
            MyName   = myName,
            State    = FriendState.Friend,
        };
        Done(onDone);
    }

    public void Accept(string myUid, string myName, string peerUid, Action<string> onDone)
    {
        if (list.TryGetValue(peerUid, out Friendship f)) { f.State = FriendState.Friend; list[peerUid] = f; }
        Done(onDone);
    }

    public void Remove(string myUid, string peerUid, Action<string> onDone)
    {
        list.Remove(peerUid);
        Done(onDone);
    }

    public void UpdateMyName(string myUid, string peerUid, string myName)
    {
        if (list.TryGetValue(peerUid, out Friendship f)) { f.MyName = myName; list[peerUid] = f; }
        Publish();
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
    public event Action<string, IReadOnlyList<string>>      MessagesRemoved { add { } remove { } }
    public event Action<string>                             Failed { add { } remove { } }

    private readonly Dictionary<string, DirectRoom>        rooms    = new Dictionary<string, DirectRoom>();
    private readonly Dictionary<string, List<ChatMessage>> messages = new Dictionary<string, List<ChatMessage>>();
    private string watching;

    public void Connect(string myUid) => Publish();
    public void Disconnect() => watching = null;

    public void Watch(string myUid, string peerUid, int count)
    {
        watching = peerUid;
        if (messages.TryGetValue(peerUid, out List<ChatMessage> list) && list.Count > 0)
            MessagesReceived?.Invoke(peerUid, list.ToArray());
    }

    public void Unwatch() => watching = null;

    public void Open(string myUid, string myName, string peerUid, string peerName, Action<string> onDone)
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

    public void Send(string myUid, string peerUid, ChatMessage message, bool invitePeer, Action<string> onDone)
    {
        message.Id = Guid.NewGuid().ToString("N");
        if (!messages.TryGetValue(peerUid, out List<ChatMessage> list)) messages[peerUid] = list = new List<ChatMessage>();
        list.Add(message);

        if (rooms.TryGetValue(peerUid, out DirectRoom room))
        {
            room.LastSentAt = message.SentAt;
            if (invitePeer) room.PeerState = RoomState.In;
            rooms[peerUid] = room;
        }

        if (watching == peerUid) MessagesReceived?.Invoke(peerUid, new[] { message });
        Publish();
        onDone?.Invoke(null);
    }

    public void MarkRead(string myUid, string peerUid) { }

    private void Publish() => RoomsReceived?.Invoke(new List<DirectRoom>(rooms.Values));
}

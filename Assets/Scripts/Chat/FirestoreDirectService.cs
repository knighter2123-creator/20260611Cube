using System;
using System.Collections.Generic;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore 로 1:1 개인 메시지를 주고받습니다. FirestoreChatService.Direct 로 만들어지며 채팅과 같은 Firestore(같은 UID)를 씁니다.
///
/// [Firestore 구조]  두 사람 사이 대화방 하나 = 문서 하나
///   dmRooms/{작은 UID}_{큰 UID}
///     members    : [uid, uid]                    — array-contains 로 '내 대화방' 을 조회
///     names      : { uid: 닉네임 }                — 탭에 보일 이름. 각자 열거나 보낼 때 자기 이름을 고쳐 씀
///     state      : { uid: "in" | "left" }        — 없음 = 아직 들어온 적 없음 (첫 메시지를 받으면 "in")
///     unread     : { uid: 안 읽은 수 }             — 보낼 때 상대 칸 +1, 읽으면 내 칸 0
///     lastSentAt : int                           — 마지막 메시지 시각 (UTC ms, 없으면 방을 만든 시각) — 탭 정렬
///     lastFrom   : string
///     createdAt  : timestamp
///   dmRooms/{id}/messages/{자동 ID}   — chat 과 같은 필드 (FirestoreChatService.ToData). expireAt 으로 TTL 삭제 가능
///
/// [나가기]  내 state 를 "left" 로 → 내 탭이 사라지고, 상대는 더 보낼 수 없음 (보낼 때 상대 state 를 확인).
///   다시 열면(Open) 내 state 만 "in" 으로 돌아옵니다. 상대가 나간 방에 내가 상대를 다시 넣을 수는 없습니다.
/// [보내기]  메시지 추가 + 방 갱신(마지막 시각·상대 안 읽은 수)을 한 배치로 — 둘 중 하나만 되는 일이 없게.
///
/// [리스너 비용]  대화방 목록: 연결할 때 내 방 수만큼 + 바뀐 방 1개당 1회 (다른 방에 메시지가 와도 방 문서 1회).
///   메시지: 보고 있는 방 하나만 — 열 때 최근 count 개 + 새 메시지 1개당 1회.
/// </summary>
public class FirestoreDirectService : IDirectService
{
    public const string COLLECTION = "dmRooms";
    public const string MESSAGES   = "messages";

    private const string IN   = "in";
    private const string LEFT = "left";

    public event Action<IReadOnlyList<DirectRoom>>           RoomsReceived;
    public event Action<string, IReadOnlyList<ChatMessage>>  MessagesReceived;
    public event Action<string, IReadOnlyList<string>>       MessagesRemoved;
    public event Action<string>                              Failed;

    private readonly Func<FirebaseFirestore> db;   // 로그인 유저는 기본 앱, 게스트는 채팅 보조 앱 (FirestoreChatService ★)
    private ListenerRegistration roomsListener;
    private ListenerRegistration messagesListener;
    private readonly HashSet<string> knownRooms = new HashSet<string>();   // 서버에 이미 있는 방 (Open 이 새로 만들지 판단)

    public FirestoreDirectService(Func<FirebaseFirestore> db) { this.db = db; }

    private static string PairId(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "_" + b : b + "_" + a;

    private static List<string> Members(string a, string b)
        => string.CompareOrdinal(a, b) < 0 ? new List<string> { a, b } : new List<string> { b, a };

    private DocumentReference Room(string a, string b) => db().Collection(COLLECTION).Document(PairId(a, b));

    // ───────────────────────────── 대화방 목록 ─────────────────────────────

    public void Connect(string myUid)
    {
        if (roomsListener != null) return;

        try
        {
            ListenerRegistration reg = db().Collection(COLLECTION)
                .WhereArrayContains("members", myUid)
                .Listen(snap => OnRooms(snap, myUid));
            roomsListener = reg;

            reg.ListenerTask.ContinueWithOnMainThread(task =>
            {
                if (roomsListener != reg) return;   // Disconnect 로 멈춘 것
                roomsListener = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Direct] 대화방 리스너 끊김: {task.Exception}");
                    Failed?.Invoke("개인 메시지 서버와 연결이 끊겼습니다.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Direct] 대화방 리스너 시작 실패: {e}");
            roomsListener = null;
            Failed?.Invoke("개인 메시지 서버에 연결하지 못했습니다.");
        }
    }

    public void Disconnect()
    {
        Unwatch();
        ListenerRegistration reg = roomsListener;
        roomsListener = null;   // 먼저 비워서 ListenerTask 완료를 '오류' 로 보지 않게 함
        reg?.Stop();
        knownRooms.Clear();
    }

    private void OnRooms(QuerySnapshot snap, string myUid)
    {
        knownRooms.Clear();
        var rooms = new List<DirectRoom>();
        foreach (DocumentSnapshot doc in snap.Documents)
        {
            knownRooms.Add(doc.Id);
            if (TryReadRoom(doc, myUid, out DirectRoom room)) rooms.Add(room);
        }
        RoomsReceived?.Invoke(rooms);
    }

    private static bool TryReadRoom(DocumentSnapshot doc, string myUid, out DirectRoom room)
    {
        room = default;
        if (!doc.TryGetValue("members", out List<object> members) || members == null || members.Count != 2) return false;

        string peer = null;
        foreach (object o in members)
            if (o is string s && s != myUid) peer = s;
        if (peer == null) return false;

        doc.TryGetValue("names",      out Dictionary<string, object> names);
        doc.TryGetValue("state",      out Dictionary<string, object> state);
        doc.TryGetValue("unread",     out Dictionary<string, object> unread);
        doc.TryGetValue("lastSentAt", out long lastSentAt);

        room = new DirectRoom
        {
            PeerUid    = peer,
            PeerName   = names != null && names.TryGetValue(peer, out object n) && n is string ns && ns.Length > 0 ? ns : PlayerProfile.ANONYMOUS_PREFIX,
            MyState    = StateOf(state, myUid),
            PeerState  = StateOf(state, peer),
            Unread     = unread != null && unread.TryGetValue(myUid, out object u) ? (int)Convert.ToInt64(u) : 0,
            LastSentAt = lastSentAt,
        };
        return true;
    }

    private static RoomState StateOf(Dictionary<string, object> state, string uid)
    {
        if (state == null || !state.TryGetValue(uid, out object v)) return RoomState.None;
        return v as string == IN ? RoomState.In : v as string == LEFT ? RoomState.Left : RoomState.None;
    }

    // ───────────────────────────── 메시지 (보고 있는 방 하나) ─────────────────────────────

    public void Watch(string myUid, string peerUid, int count)
    {
        Unwatch();

        try
        {
            ListenerRegistration reg = Room(myUid, peerUid).Collection(MESSAGES)
                .OrderByDescending("sentAt")
                .Limit(count)
                .Listen(snap => OnMessages(snap, peerUid));
            messagesListener = reg;

            reg.ListenerTask.ContinueWithOnMainThread(task =>
            {
                if (messagesListener != reg) return;
                messagesListener = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Direct] 메시지 리스너 끊김: {task.Exception}");
                    Failed?.Invoke("개인 메시지를 불러오지 못했습니다.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Direct] 메시지 리스너 시작 실패: {e}");
            messagesListener = null;
            Failed?.Invoke("개인 메시지를 불러오지 못했습니다.");
        }
    }

    public void Unwatch()
    {
        ListenerRegistration reg = messagesListener;
        messagesListener = null;
        reg?.Stop();
    }

    private void OnMessages(QuerySnapshot snap, string peerUid)
    {
        var added   = new List<ChatMessage>();
        var removed = new List<string>();
        foreach (DocumentChange change in snap.GetChanges())
        {
            switch (change.ChangeType)
            {
                case DocumentChange.Type.Added:
                    if (FirestoreChatService.TryRead(change.Document, out ChatMessage m)) added.Add(m);
                    break;
                case DocumentChange.Type.Removed:   // 서버가 거절한 내 메시지, 최근 N개 밖으로 밀려난 메시지
                    removed.Add(change.Document.Id);
                    break;
            }
        }
        if (added.Count   > 0) MessagesReceived?.Invoke(peerUid, added);
        if (removed.Count > 0) MessagesRemoved?.Invoke(peerUid, removed);
    }

    // ───────────────────────────── 쓰기 ─────────────────────────────

    public void Open(string myUid, string myName, string peerUid, string peerName, Action<string> onDone)
    {
        Dictionary<string, object> data;
        if (knownRooms.Contains(PairId(myUid, peerUid)))
        {
            // 이미 있는 방 — 내 칸만 (상대 이름·상태는 상대가 씀)
            data = new Dictionary<string, object>
            {
                { "names", new Dictionary<string, object> { { myUid, myName } } },
                { "state", new Dictionary<string, object> { { myUid, IN } } },
            };
        }
        else
        {
            data = new Dictionary<string, object>
            {
                { "members",    Members(myUid, peerUid) },
                { "names",      new Dictionary<string, object> { { myUid, myName }, { peerUid, peerName } } },
                { "state",      new Dictionary<string, object> { { myUid, IN } } },
                { "unread",     new Dictionary<string, object> { { myUid, 0 }, { peerUid, 0 } } },
                { "lastSentAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                { "createdAt",  FieldValue.ServerTimestamp },
            };
        }
        Run(() => Room(myUid, peerUid).SetAsync(data, SetOptions.MergeAll), "대화방을 열지 못했습니다.", onDone);
    }

    public void Leave(string myUid, string peerUid, Action<string> onDone)
    {
        var data = new Dictionary<string, object>
        {
            { "state",  new Dictionary<string, object> { { myUid, LEFT } } },
            { "unread", new Dictionary<string, object> { { myUid, 0 } } },
        };
        Run(() => Room(myUid, peerUid).SetAsync(data, SetOptions.MergeAll), "대화방을 나가지 못했습니다.", onDone);
    }

    public void Send(string myUid, string peerUid, ChatMessage m, bool invitePeer, Action<string> onDone)
    {
        var state = new Dictionary<string, object> { { myUid, IN } };
        if (invitePeer) state[peerUid] = IN;

        // members 도 함께 — 방을 만드는 쓰기가 아직 서버에 닿지 않았어도 이 배치만으로 방이 완성되게
        var room = new Dictionary<string, object>
        {
            { "members",    Members(myUid, peerUid) },
            { "names",      new Dictionary<string, object> { { myUid, m.Nickname } } },
            { "state",      state },
            { "unread",     new Dictionary<string, object> { { peerUid, FieldValue.Increment(1) } } },
            { "lastSentAt", m.SentAt },
            { "lastFrom",   myUid },
        };

        Run(() =>
        {
            DocumentReference roomRef = Room(myUid, peerUid);
            WriteBatch batch = db().StartBatch();
            batch.Set(roomRef.Collection(MESSAGES).Document(), FirestoreChatService.ToData(m));
            batch.Set(roomRef, room, SetOptions.MergeAll);
            return batch.CommitAsync();   // 내 화면에는 리스너가 서버 확인 전에 바로 보여 줌
        }, "메시지를 보내지 못했습니다.", onDone);
    }

    public void MarkRead(string myUid, string peerUid)
    {
        var data = new Dictionary<string, object> { { "unread", new Dictionary<string, object> { { myUid, 0 } } } };
        Run(() => Room(myUid, peerUid).SetAsync(data, SetOptions.MergeAll), null, null);
    }

    private static void Run(Func<System.Threading.Tasks.Task> write, string error, Action<string> onDone)
    {
        try
        {
            write().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Direct] 쓰기 실패: {task.Exception}");
                    onDone?.Invoke(error);
                    return;
                }
                onDone?.Invoke(null);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Direct] 쓰기 호출 실패: {e}");
            onDone?.Invoke(error);
        }
    }
}

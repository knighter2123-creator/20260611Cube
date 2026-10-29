using System;
using System.Collections.Generic;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore 로 친구 요청·수락·삭제를 주고받습니다. FirestoreChatService.Friends 로 만들어지며 채팅과 같은 Firestore(같은 UID)를 씁니다.
///
/// [Firestore 구조]  두 사람 사이 관계 하나 = 문서 하나
///   friendships/{작은 UID}_{큰 UID}     — 누가 먼저 보내도 같은 문서 → 서로 동시에 요청해도 하나만 생김
///     members    : [uid, uid]  — 두 사람 (array-contains 로 '내 관계' 를 조회)
///     from       : string      — 요청한 사람
///     to         : string      — 요청 받은 사람 (이 사람만 수락할 수 있음)
///     status     : "pending" | "accepted"
///     names      : { uid: 닉네임, uid: 닉네임 }   — 목록에 보일 이름. 각자 자기 이름만 고쳐 씀
///     sentAt     : int         — 요청 시각 (UTC ms)
///     createdAt  : timestamp   — 서버 시각 (관리용)
///   수락 = status 를 accepted 로, 거절·취소·삭제 = 문서 삭제 → 양쪽 친구 창에서 동시에 바뀜.
///
/// [리스너 비용]  연결할 때 내 관계 문서 수만큼 읽기 + 이후 바뀐 문서 1개당 1회. (친구 최대 ChatManager.MAX_FRIENDS)
///
/// [차단 목록]  사람마다 문서 하나 — 본인만 읽고 씀
///   chatBlocks/{내 UID}
///     users     : { uid: 차단할 때의 닉네임, ... }
///     updatedAt : timestamp
///   차단 = users 에 한 칸 합치기(merge), 해제 = 그 칸 지우기. 리스너로 받으므로 다른 기기에서 바꿔도 바로 반영됩니다.
/// </summary>
public class FirestoreFriendService : IFriendService
{
    public const string COLLECTION       = "friendships";
    public const string BLOCK_COLLECTION = "chatBlocks";

    private const string PENDING  = "pending";
    private const string ACCEPTED = "accepted";

    public event Action<IReadOnlyList<Friendship>> Received;
    public event Action<IReadOnlyDictionary<string, string>> BlocksReceived;
    public event Action<string> Failed;

    private readonly Func<FirebaseFirestore> db;   // 로그인 유저는 기본 앱, 게스트는 채팅 보조 앱 (FirestoreChatService ★)
    private ListenerRegistration listener;
    private ListenerRegistration blockListener;

    public FirestoreFriendService(Func<FirebaseFirestore> db) { this.db = db; }

    private static string PairId(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "_" + b : b + "_" + a;

    private DocumentReference Doc(string a, string b) => db().Collection(COLLECTION).Document(PairId(a, b));

    // ───────────────────────────── 받기 ─────────────────────────────

    public void Connect(string myUid)
    {
        if (listener != null) return;

        try
        {
            ListenerRegistration reg = db().Collection(COLLECTION)
                .WhereArrayContains("members", myUid)
                .Listen(snap => OnSnapshot(snap, myUid));
            listener = reg;

            reg.ListenerTask.ContinueWithOnMainThread(task =>
            {
                if (listener != reg) return;   // Disconnect 로 멈춘 것
                listener = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Friend] 리스너 끊김: {task.Exception}");
                    Failed?.Invoke("친구 목록을 불러오지 못했습니다.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Friend] 리스너 시작 실패: {e}");
            listener = null;
            Failed?.Invoke("친구 목록을 불러오지 못했습니다.");
            return;
        }

        ConnectBlocks(myUid);
    }

    private void ConnectBlocks(string myUid)
    {
        if (blockListener != null) return;

        try
        {
            ListenerRegistration reg = db().Collection(BLOCK_COLLECTION).Document(myUid).Listen(OnBlocksSnapshot);
            blockListener = reg;

            reg.ListenerTask.ContinueWithOnMainThread(task =>
            {
                if (blockListener != reg) return;
                blockListener = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Friend] 차단 목록 리스너 끊김: {task.Exception}");
                    Failed?.Invoke("차단 목록을 불러오지 못했습니다.");
                }
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Friend] 차단 목록 리스너 시작 실패: {e}");
            blockListener = null;
            Failed?.Invoke("차단 목록을 불러오지 못했습니다.");
        }
    }

    public void Disconnect()
    {
        // 먼저 비워서 ListenerTask 완료를 '오류' 로 보지 않게 함
        ListenerRegistration reg = listener;
        listener = null;
        reg?.Stop();

        ListenerRegistration blockReg = blockListener;
        blockListener = null;
        blockReg?.Stop();
    }

    /// <summary>문서가 없으면(아직 아무도 차단 안 함) 빈 목록.</summary>
    private void OnBlocksSnapshot(DocumentSnapshot doc)
    {
        var blocks = new Dictionary<string, string>();
        if (doc.Exists && doc.TryGetValue("users", out Dictionary<string, object> users) && users != null)
            foreach (KeyValuePair<string, object> pair in users)
                blocks[pair.Key] = pair.Value is string s && s.Length > 0 ? s : PlayerProfile.ANONYMOUS_PREFIX;
        BlocksReceived?.Invoke(blocks);
    }

    private void OnSnapshot(QuerySnapshot snap, string myUid)
    {
        var list = new List<Friendship>();
        foreach (DocumentSnapshot doc in snap.Documents)
            if (TryRead(doc, myUid, out Friendship f)) list.Add(f);
        Received?.Invoke(list);
    }

    private static bool TryRead(DocumentSnapshot doc, string myUid, out Friendship f)
    {
        doc.TryGetValue("from",   out string from);
        doc.TryGetValue("to",     out string to);
        doc.TryGetValue("status", out string status);
        doc.TryGetValue("names",  out Dictionary<string, object> names);

        f = default;
        if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return false;
        if (from != myUid && to != myUid) return false;

        string peer = from == myUid ? to : from;
        f = new Friendship
        {
            PeerUid  = peer,
            PeerName = NameOf(names, peer),
            MyName   = NameOf(names, myUid),
            State    = status == ACCEPTED ? FriendState.Friend : from == myUid ? FriendState.Outgoing : FriendState.Incoming,
        };
        return true;
    }

    private static string NameOf(Dictionary<string, object> names, string uid)
        => names != null && names.TryGetValue(uid, out object v) && v is string s && s.Length > 0 ? s : PlayerProfile.ANONYMOUS_PREFIX;

    // ───────────────────────────── 쓰기 ─────────────────────────────

    public void Request(string myUid, string myName, string peerUid, string peerName, Action<string> onDone)
    {
        var data = new Dictionary<string, object>
        {
            { "members",   new List<string> { myUid, peerUid } },
            { "from",      myUid },
            { "to",        peerUid },
            { "status",    PENDING },
            { "names",     new Dictionary<string, object> { { myUid, myName }, { peerUid, peerName } } },
            { "sentAt",    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
            { "createdAt", FieldValue.ServerTimestamp },
        };
        Run(() => Doc(myUid, peerUid).SetAsync(data), "친구 요청을 보내지 못했습니다.", onDone);
    }

    public void Accept(string myUid, string myName, string peerUid, Action<string> onDone)
    {
        var data = new Dictionary<string, object>
        {
            { "status",          ACCEPTED },
            { "names." + myUid,  myName },   // 키는 필드 경로 — names 안의 내 이름만 바꿈
            { "acceptedAt",      FieldValue.ServerTimestamp },
        };
        Run(() => Doc(myUid, peerUid).UpdateAsync(data), "친구 요청을 수락하지 못했습니다.", onDone);
    }

    public void Remove(string myUid, string peerUid, Action<string> onDone)
        => Run(() => Doc(myUid, peerUid).DeleteAsync(), "처리하지 못했습니다. 잠시 후 다시 시도해 주세요.", onDone);

    public void UpdateMyName(string myUid, string peerUid, string myName)
    {
        var data = new Dictionary<string, object> { { "names." + myUid, myName } };
        Run(() => Doc(myUid, peerUid).UpdateAsync(data), null, null);
    }

    public void Block(string myUid, string peerUid, string peerName, Action<string> onDone)
    {
        // merge — users 안의 다른 칸은 그대로 두고 한 칸만 더함 (문서가 없으면 새로 만듦)
        var data = new Dictionary<string, object>
        {
            { "users",     new Dictionary<string, object> { { peerUid, peerName } } },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
        Run(() => db().Collection(BLOCK_COLLECTION).Document(myUid).SetAsync(data, SetOptions.MergeAll),
            "차단하지 못했습니다. 잠시 후 다시 시도해 주세요.", onDone);
    }

    public void Unblock(string myUid, string peerUid, Action<string> onDone)
    {
        var data = new Dictionary<string, object>
        {
            { "users." + peerUid, FieldValue.Delete },
            { "updatedAt",        FieldValue.ServerTimestamp },
        };
        Run(() => db().Collection(BLOCK_COLLECTION).Document(myUid).UpdateAsync(data),
            "차단을 해제하지 못했습니다. 잠시 후 다시 시도해 주세요.", onDone);
    }

    private static void Run(Func<System.Threading.Tasks.Task> write, string error, Action<string> onDone)
    {
        try
        {
            write().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[Friend] 쓰기 실패: {task.Exception}");
                    onDone?.Invoke(error);
                    return;
                }
                onDone?.Invoke(null);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Friend] 쓰기 호출 실패: {e}");
            onDone?.Invoke(error);
        }
    }
}

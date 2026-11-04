using System;
using System.Collections.Generic;
using System.Globalization;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>
/// 1:1 개인 메시지 — 뒤끝 쪽지(Backend.Message)로 주고받습니다.
///
/// [대화방]  뒤끝에는 '대화방' 이 없고 받은 쪽지함 / 보낸 쪽지함만 있습니다.
///   두 쪽지함을 받아 상대(gamer inDate)별로 묶어 대화방처럼 보여 줍니다.
///   이 기기에서 연 방 · 나간 방은 PlayerPrefs 에 계정별로 기억합니다 (나간 방은 그 뒤에 새 쪽지가 오면 다시 보임).
///   상대가 나갔는지는 알 수 없어서 PeerLeft 는 늘 false 입니다.
///
/// [받기]  실시간 리스너가 없어 Refresh 때 두 쪽지함을 다시 받습니다 (뒤끝 호출 2회).
///   ChatManager 가 연결할 때 · 보낸 뒤 · 쪽지 실시간 알림(BackendNotify) · 일정 간격으로 부릅니다.
///
/// [안 읽은 수]  이 기기에서 마지막으로 읽은 쪽지 시각을 계정·상대별로 기억해 셉니다 (서버 '읽음' 표시는 쓰지 않음 —
///   쪽지마다 따로 불러야 해서 호출이 많아짐). 다른 기기에서 읽은 것은 이 기기에선 안 읽은 것으로 보일 수 있습니다.
///
/// [진행도]  쪽지 내용 앞에 "진행도|" 를 붙여 보내고, 받을 때 떼어 냅니다 (채팅 줄의 [2-3] 표시용).
///
/// ⚠ 뒤끝 콘솔의 쪽지 설정(보관 개수 · 보관 기간)을 넘은 옛 쪽지는 서버에서 지워져 대화에서도 사라집니다.
/// [약속]  IDirectService 와 같음 — 모든 이벤트·콜백은 메인 스레드 (BackendManager.Call).
/// </summary>
public class BackendDirectService : IDirectService
{
    public event Action<IReadOnlyList<DirectRoom>>          RoomsReceived;
    public event Action<string, IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<string, IReadOnlyList<string>>      MessagesRemoved { add { } remove { } }   // 지운 것은 다음 목록에서 빠질 뿐
    public event Action<string>                             Failed;

    private const string PREF_ROOMS = "dm.rooms.";   // + 내 uid — 이 기기에서 연 방 / 나간 방 / 읽은 시각
    private const char   ProgressSep = '|';

    private string myUid;
    private int    generation;
    private bool   loadedOnce;
    private bool   refreshing;
    private bool   refreshAgain;

    private string watching;
    private int    watchCount = 50;

    // 상대별 쪽지 (보낸 시각 순)
    private readonly Dictionary<string, List<ChatMessage>> byPeer = new Dictionary<string, List<ChatMessage>>();
    private readonly Dictionary<string, string>            names  = new Dictionary<string, string>();   // 상대 닉네임

    /// <summary>이 기기에서 기억하는 방 상태 (계정별).</summary>
    [Serializable]
    private class LocalRoom
    {
        public string peer;
        public string name;
        public bool   left;       // 나감
        public long   since;      // 열거나 나간 시각 (UTC ms)
        public long   readUpTo;   // 여기까지 읽음 (받은 쪽지의 서버 시각, UTC ms)
    }
    [Serializable] private class LocalRooms { public List<LocalRoom> rooms = new List<LocalRoom>(); }

    private readonly Dictionary<string, LocalRoom> local = new Dictionary<string, LocalRoom>();

    // ───────────────────────────── 연결 ─────────────────────────────

    public void Connect(string uid)
    {
        Disconnect();

        string reason = SessionProblem(uid);
        if (reason != null) { Failed?.Invoke(reason); return; }

        myUid = uid;
        LoadLocal();
        Refresh();
    }

    public void Disconnect()
    {
        myUid = null;
        generation++;
        loadedOnce   = false;
        refreshing   = false;
        refreshAgain = false;
        watching     = null;
        byPeer.Clear();
        names.Clear();
        local.Clear();
    }

    private static string SessionProblem(string uid)
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsSignedIn)                       return "로그인하면 개인 메시지를 쓸 수 있습니다.";
        if (!auth.IsOnline)                                         return "서버에 연결할 수 없습니다.";
        if (string.IsNullOrEmpty(uid) || uid != Backend.UserInDate) return "계정 정보를 확인하는 중입니다.";
        return null;
    }

    // ───────────────────────────── 받기 ─────────────────────────────

    public void Refresh()
    {
        if (myUid == null) return;
        if (refreshing) { refreshAgain = true; return; }

        refreshing = true;
        int gen = generation;
        var received = new List<JsonData>();
        var sent     = new List<JsonData>();

        Fetch(cb => Backend.Message.GetReceivedMessageList(cb), "받은 쪽지", gen, received, e1 =>
        {
            if (e1 != null) { FinishRefresh(gen, null, null, e1); return; }
            Fetch(cb => Backend.Message.GetSentMessageList(cb), "보낸 쪽지", gen, sent, e2 =>
                FinishRefresh(gen, received, sent, e2));
        });
    }

    private void Fetch(Action<Backend.BackendCallback> request, string what, int gen, List<JsonData> into, Action<string> next)
    {
        BackendManager.Call(request, bro =>
        {
            if (gen != generation) return;
            if (bro != null && bro.GetStatusCode() == "404") { next(null); return; }   // 비었음
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Direct] {what} 목록 조회 실패: {BackendManager.Describe(bro)}");
                next(BackendManager.IsNetworkError(bro) ? "서버에 연결할 수 없습니다." : "대화 목록을 불러오지 못했습니다.");
                return;
            }

            JsonData rows = bro.FlattenRows();
            for (int i = 0; rows != null && i < rows.Count; i++) into.Add(rows[i]);
            next(null);
        });
    }

    private void FinishRefresh(int gen, List<JsonData> received, List<JsonData> sent, string error)
    {
        if (gen != generation) return;
        refreshing = false;

        if (error != null)
        {
            if (!loadedOnce) { Failed?.Invoke(error); return; }   // 처음이면 ChatManager 가 끊고 잠시 뒤 다시 연결
        }
        else
        {
            loadedOnce = true;
            Rebuild(received, sent);
            PublishRooms();
            PublishWatched();
        }

        if (refreshAgain)
        {
            refreshAgain = false;
            Refresh();
        }
    }

    /// <summary>두 쪽지함을 상대별 대화로 묶습니다.</summary>
    private void Rebuild(List<JsonData> received, List<JsonData> sent)
    {
        byPeer.Clear();

        foreach (JsonData row in received)
        {
            string peer = First(row, "sender", "senderInDate", "senderIndate");
            if (string.IsNullOrEmpty(peer)) continue;
            string name = First(row, "senderNickname", "senderNickName", "nickname");
            Add(peer, name, row, fromMe: false);
        }

        foreach (JsonData row in sent)
        {
            string peer = First(row, "receiver", "receiverInDate", "receiverIndate");
            if (string.IsNullOrEmpty(peer)) continue;
            string name = First(row, "receiverNickname", "receiverNickName", "nickname");
            Add(peer, name, row, fromMe: true);
        }

        foreach (List<ChatMessage> list in byPeer.Values)
            list.Sort((a, b) => a.SentAt != b.SentAt ? a.SentAt.CompareTo(b.SentAt) : string.CompareOrdinal(a.Id, b.Id));
    }

    private void Add(string peer, string peerName, JsonData row, bool fromMe)
    {
        string id = BackendJson.Str(row, "inDate");
        if (string.IsNullOrEmpty(id)) return;

        if (!string.IsNullOrEmpty(peerName)) names[peer] = peerName;

        Decode(First(row, "content", "contents") ?? string.Empty, out int progress, out string text);
        var message = new ChatMessage
        {
            Id       = (fromMe ? "s:" : "r:") + id,
            Uid      = fromMe ? myUid : peer,
            Nickname = fromMe ? PlayerProfile.DisplayName : (names.TryGetValue(peer, out string n) ? n : "?"),
            Progress = progress,
            Text     = text,
            SentAt   = BackendJson.DateMs(row, "inDate"),
        };

        if (!byPeer.TryGetValue(peer, out List<ChatMessage> list)) byPeer[peer] = list = new List<ChatMessage>();
        list.Add(message);
    }

    private static string First(JsonData row, params string[] keys)
    {
        foreach (string k in keys)
        {
            string v = BackendJson.Str(row, k);
            if (!string.IsNullOrEmpty(v)) return v;
        }
        return null;
    }

    private void PublishRooms()
    {
        var rooms = new List<DirectRoom>();
        var peers = new HashSet<string>(byPeer.Keys);
        foreach (string p in local.Keys) peers.Add(p);

        foreach (string peer in peers)
        {
            byPeer.TryGetValue(peer, out List<ChatMessage> list);
            local.TryGetValue(peer, out LocalRoom room);

            long last = list != null && list.Count > 0 ? list[list.Count - 1].SentAt : 0;

            // 나간 방은 그 뒤에 새 쪽지가 있을 때만 다시 보임
            bool left = room != null && room.left && last <= room.since;

            int unread = 0;
            long readUpTo = room != null ? room.readUpTo : 0;
            if (list != null)
                foreach (ChatMessage m in list)
                    if (m.Uid == peer && m.SentAt > readUpTo) unread++;

            rooms.Add(new DirectRoom
            {
                PeerUid    = peer,
                PeerName   = names.TryGetValue(peer, out string n) ? n : room?.name ?? "?",
                MyState    = left ? RoomState.Left : RoomState.In,
                PeerState  = RoomState.In,   // 상대가 나갔는지는 알 수 없음
                Unread     = left ? 0 : unread,
                LastSentAt = Math.Max(last, room != null ? room.since : 0),
            });
        }

        RoomsReceived?.Invoke(rooms);
    }

    private void PublishWatched()
    {
        if (watching == null || !byPeer.TryGetValue(watching, out List<ChatMessage> list) || list.Count == 0) return;
        int start = Math.Max(0, list.Count - watchCount);
        MessagesReceived?.Invoke(watching, list.GetRange(start, list.Count - start));
    }

    // ───────────────────────────── 보기 / 열기 / 나가기 ─────────────────────────────

    public void Watch(string uid, string peerUid, int count)
    {
        watching   = peerUid;
        watchCount = Math.Max(1, count);
        PublishWatched();
    }

    public void Unwatch() => watching = null;

    public void Open(string uid, string myName, string peerUid, string peerName, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke("개인 메시지를 불러오는 중입니다. 잠시 후 다시 시도해 주세요."); return; }

        LocalRoom room = GetLocal(peerUid);
        room.left  = false;
        room.since = Now();
        if (!string.IsNullOrEmpty(peerName)) room.name = peerName;
        SaveLocal();

        PublishRooms();
        onDone?.Invoke(null);
    }

    public void Leave(string uid, string peerUid, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke(null); return; }

        LocalRoom room = GetLocal(peerUid);
        room.left  = true;
        // 서버 시각과 기기 시각이 어긋나도 지금까지의 쪽지는 확실히 숨기도록, 마지막 쪽지 시각과 지금 중 큰 값
        room.since = Math.Max(Now(), LastAt(peerUid));
        SaveLocal();

        PublishRooms();
        onDone?.Invoke(null);
    }

    // ───────────────────────────── 보내기 / 읽음 ─────────────────────────────

    public void Send(string uid, string peerUid, ChatMessage message, bool invitePeer, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke("개인 메시지를 불러오는 중입니다. 잠시 후 다시 시도해 주세요."); return; }

        string contents = message.Progress.ToString(CultureInfo.InvariantCulture) + ProgressSep + message.Text;
        int gen = generation;

        BackendManager.Call(cb => Backend.Message.SendMessage(peerUid, contents, cb), bro =>
        {
            if (gen != generation) { onDone?.Invoke("계정이 바뀌어 보내지 않았습니다."); return; }

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Direct] 쪽지 보내기 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(SendError(bro));
                return;
            }

            onDone?.Invoke(null);
            Refresh();   // 보낸 쪽지함에서 내 메시지를 받아 화면에 붙임
        });
    }

    private static string SendError(BackendReturnObject bro)
    {
        if (BackendManager.IsNetworkError(bro)) return "서버에 연결할 수 없습니다. 잠시 후 다시 보내 주세요.";
        switch (bro.GetStatusCode())
        {
            case "400": return "나에게는 개인 메시지를 보낼 수 없습니다.";
            case "403": return "이 유저에게는 메시지를 보낼 수 없습니다 (친구만 보낼 수 있을 수 있습니다).";
            case "404": return "상대를 찾을 수 없습니다.";
            case "405": return "상대의 쪽지함이 가득 찼습니다.";
            case "412": return "메시지가 너무 깁니다.";   // 내용 길이가 뒤끝 콘솔의 쪽지 글자 수 설정을 넘음 (진행도 접두사 "12|" 포함)
            default:    return $"메시지를 보내지 못했습니다. ({bro.GetStatusCode()})";
        }
    }

    public void MarkRead(string uid, string peerUid)
    {
        if (myUid == null || uid != myUid) return;

        long last = 0;
        if (byPeer.TryGetValue(peerUid, out List<ChatMessage> list))
            foreach (ChatMessage m in list)
                if (m.Uid == peerUid && m.SentAt > last) last = m.SentAt;

        LocalRoom room = GetLocal(peerUid);
        if (last <= room.readUpTo) return;
        room.readUpTo = last;
        SaveLocal();
        PublishRooms();
    }

    // ───────────────────────────── 도우미 ─────────────────────────────

    /// <summary>"12|안녕" → 진행도 12, "안녕". 형식이 다르면(다른 앱·콘솔에서 보낸 쪽지) 진행도 0, 내용 그대로.</summary>
    private static void Decode(string raw, out int progress, out string text)
    {
        progress = 0;
        text     = raw;
        int sep = raw.IndexOf(ProgressSep);
        if (sep <= 0 || sep > 7) return;
        if (!int.TryParse(raw.Substring(0, sep), NumberStyles.None, CultureInfo.InvariantCulture, out progress)) { progress = 0; return; }
        text = raw.Substring(sep + 1);
    }

    private long LastAt(string peer)
        => byPeer.TryGetValue(peer, out List<ChatMessage> list) && list.Count > 0 ? list[list.Count - 1].SentAt : 0;

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private LocalRoom GetLocal(string peer)
    {
        if (!local.TryGetValue(peer, out LocalRoom room))
            local[peer] = room = new LocalRoom { peer = peer, name = names.TryGetValue(peer, out string n) ? n : null };
        return room;
    }

    private void LoadLocal()
    {
        local.Clear();
        string json = PlayerPrefs.GetString(PREF_ROOMS + myUid, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            LocalRooms saved = JsonUtility.FromJson<LocalRooms>(json);
            if (saved?.rooms != null)
                foreach (LocalRoom r in saved.rooms)
                    if (!string.IsNullOrEmpty(r.peer)) local[r.peer] = r;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Direct] 대화방 기록을 읽지 못해 비웁니다: {e.Message}");
        }
    }

    private void SaveLocal()
    {
        var saved = new LocalRooms();
        saved.rooms.AddRange(local.Values);
        PlayerPrefs.SetString(PREF_ROOMS + myUid, JsonUtility.ToJson(saved));
        PlayerPrefs.Save();
    }
}

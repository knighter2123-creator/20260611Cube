using System;
using System.Collections.Generic;
using System.Globalization;
using BackEnd;
using LitJson;
using UnityEngine;


public class BackendDirectService : IDirectService
{
    public event Action<IReadOnlyList<DirectRoom>>          RoomsReceived;
    public event Action<string, IReadOnlyList<ChatMessage>> MessagesReceived;
    public event Action<string>                             Failed;

    private const string PREF_ROOMS = "dm.rooms.";   // + 내 uid — 이 기기에서 연 방 / 나간 방 / 읽은 시각
    private const char   ProgressSep = '|';

    private string myUid;
    private readonly RefreshGate gate = new RefreshGate();   // 받기 하나씩 + 이전 계정의 늦은 응답 버리기

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

        string reason = BackendSession.Problem(uid, GameMessages.Direct.loginRequired);
        if (reason != null) { Failed?.Invoke(reason); return; }

        myUid = uid;
        LoadLocal();
        Refresh();
    }

    public void Disconnect()
    {
        myUid    = null;
        watching = null;
        gate.Reset();
        byPeer.Clear();
        names.Clear();
        local.Clear();
    }

    // ───────────────────────────── 받기 ─────────────────────────────

    public void Refresh()
    {
        if (myUid == null || !gate.TryBegin(out int gen)) return;   // 받는 중이면 끝난 뒤 한 번 더
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
            if (!gate.IsCurrent(gen)) return;
            if (bro != null && bro.GetStatusCode() == "404") { next(null); return; }   // 비었음
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Direct] {what} 목록 조회 실패: {BackendManager.Describe(bro)}");
                next(BackendManager.IsNetworkError(bro) ? GameMessages.Common.offline : GameMessages.Direct.roomsFailed);
                return;
            }

            JsonData rows = bro.FlattenRows();
            for (int i = 0; rows != null && i < rows.Count; i++) into.Add(rows[i]);
            next(null);
        });
    }

    private void FinishRefresh(int gen, List<JsonData> received, List<JsonData> sent, string error)
    {
        if (!gate.IsCurrent(gen)) return;

        bool firstFailed = error != null && !gate.LoadedOnce;   // 처음이면 ChatManager 가 끊고 잠시 뒤 다시 연결
        if (error == null)
        {
            Rebuild(received, sent);
            PublishRooms();
            PublishWatched();
        }

        bool again = gate.End(error == null);
        if (firstFailed) { Failed?.Invoke(error); return; }
        if (again) Refresh();
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

    public void Open(string uid, string peerUid, string peerName, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke(GameMessages.Direct.loading); return; }

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

    public void Send(string uid, string peerUid, ChatMessage message, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke(GameMessages.Direct.loading); return; }

        string contents = message.Progress.ToString(CultureInfo.InvariantCulture) + ProgressSep + message.Text;
        int gen = gate.Generation;

        BackendManager.Call(cb => Backend.Message.SendMessage(peerUid, contents, cb), bro =>
        {
            if (!gate.IsCurrent(gen)) { onDone?.Invoke(GameMessages.Direct.accountChanged); return; }

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
        if (BackendManager.IsNetworkError(bro)) return GameMessages.Direct.offline;
        switch (bro.GetStatusCode())
        {
            case "400": return GameMessages.Chat.selfDirect;
            case "403": return GameMessages.Direct.forbidden;
            case "404": return GameMessages.Direct.notFound;
            case "405": return GameMessages.Direct.boxFull;
            case "412": return GameMessages.Chat.tooLong;   // 내용 길이가 뒤끝 콘솔의 쪽지 글자 수 설정을 넘음 (진행도 접두사 "12|" 포함)
            default:    return ChatText.Fill(GameMessages.Direct.failedCode, "code", bro.GetStatusCode());
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

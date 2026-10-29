using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1:1 개인 메시지 — 두 사람 사이의 '대화방' 단위. 서버 구조는 FirestoreDirectService 주석 참고.
///
/// [탭이 남는 동안]  내가 [나가기] 를 누르거나 상대를 차단하기 전까지 대화방(탭)은 계속 남고, 메시지도 계속 오갑니다.
///   채팅 창을 닫는 것은 나가기가 아닙니다 — 닫아도 방은 그대로이고, 안 읽은 수가 미리보기 줄에 쌓입니다.
/// [상대가 나가면]  탭은 내가 나갈 때까지 남지만 더 보낼 수 없습니다 (PeerLeft). 상대가 다시 열면 이어서 대화할 수 있습니다.
/// [받기]  대화방 목록(안 읽은 수 포함)은 채팅이 연결돼 있는 동안 항상 받고, 메시지는 보고 있는 방 하나만 받습니다 (WatchConversation).
/// [안 읽은 수]  서버의 대화방 문서에 있어 기기를 바꿔도 같습니다. 그 탭을 보고 있으면 MarkRead 로 0 이 됩니다.
/// </summary>
public partial class ChatManager
{
    public struct Conversation
    {
        public string PeerUid;
        public string PeerName;
        public long   LastSentAt;   // 탭 정렬 기준
        public bool   PeerLeft;     // 상대가 나감 — 더 보낼 수 없음
    }

    private IDirectService directService;

    private readonly Dictionary<string, DirectRoom> rooms   = new Dictionary<string, DirectRoom>();   // 서버의 내 대화방 (나간 방 포함)
    private readonly Dictionary<string, Conversation> opening = new Dictionary<string, Conversation>(); // 방을 여는 중 — 서버 목록에 들어오기 전에도 탭을 보여 줌
    private readonly HashSet<string> readRequested = new HashSet<string>();   // MarkRead 를 보냈고 아직 목록에 반영 안 됨

    private readonly List<ChatMessage> directMessages = new List<ChatMessage>();   // watchingPeer 와의 메시지 (보낸 시각 순)
    private readonly HashSet<string>   directIds      = new HashSet<string>();
    private static readonly List<ChatMessage> NoMessages = new List<ChatMessage>();

    private readonly List<Conversation> conversationList = new List<Conversation>();
    private readonly HashSet<string>    conversationSet  = new HashSet<string>();
    private bool conversationsDirty = true;

    private string directKey;          // 대화방 리스너가 연결된 UID (null = 끊김)
    private string directFailReason;
    private float  nextDirectConnectTime;
    private bool   roomsLoaded;
    private string watchedPeer;        // 보고 싶은 방 (ChatPanel 이 정함)
    private string watchingPeer;       // 실제로 메시지를 받고 있는 방

    /// <summary>개인 메시지를 지금 쓸 수 없는 이유. null 이면 사용 가능.</summary>
    public string DirectUnavailableReason
    {
        get
        {
            if (UnavailableReason != null) return UnavailableReason;
            if (directKey == null)         return directFailReason ?? "개인 메시지 서버에 연결하는 중입니다...";
            return roomsLoaded ? null : "대화 목록을 불러오는 중입니다...";
        }
    }

    /// <summary>내가 들어가 있는 대화방 — 최근 대화 순 (차단한 상대 제외).</summary>
    public IReadOnlyList<Conversation> Conversations
    {
        get { RebuildConversations(); return conversationList; }
    }

    public bool HasConversation(string peerUid)
    {
        RebuildConversations();
        return peerUid != null && conversationSet.Contains(peerUid);
    }

    /// <summary>상대가 대화방을 나갔는가 (더 보낼 수 없음).</summary>
    public bool PeerLeft(string peerUid)
        => peerUid != null && rooms.TryGetValue(peerUid, out DirectRoom r) && r.PeerState == RoomState.Left;

    public int UnreadCount(string peerUid)
        => peerUid != null && !readRequested.Contains(peerUid) && rooms.TryGetValue(peerUid, out DirectRoom r) ? r.Unread : 0;

    /// <summary>들어가 있는 모든 대화방의 안 읽은 수 합계 (미리보기 줄용).</summary>
    public int TotalUnread
    {
        get
        {
            int total = 0;
            foreach (Conversation c in Conversations) total += UnreadCount(c.PeerUid);
            return total;
        }
    }

    // ───────────────────────────── 대화방 열기 / 나가기 ─────────────────────────────

    /// <summary>
    /// 상대와의 대화방을 엽니다 — 없으면 만들고, 내가 나갔던 방이면 다시 들어갑니다. 탭은 바로 생깁니다.
    /// 열 수 없으면 이유를 돌려줍니다 (null = 열림). 서버 쓰기가 나중에 실패하면 onFailed(이유) 가 오고 탭이 사라집니다.
    /// </summary>
    public string OpenConversation(string peerUid, string peerName, Action<string> onFailed)
    {
        if (DirectUnavailableReason != null)                 return DirectUnavailableReason;
        if (string.IsNullOrEmpty(peerUid) || peerUid == messagesKey) return "나와는 대화할 수 없습니다.";
        if (IsBlocked(peerUid))                              return "차단한 유저와는 대화할 수 없습니다.";
        if (HasConversation(peerUid))                        return null;
        if (!TryGetSender(out string uid))                   return "계정 정보를 확인하는 중입니다. 잠시 후 다시 시도해 주세요.";

        opening[peerUid] = new Conversation
        {
            PeerUid    = peerUid,
            PeerName   = peerName,
            LastSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            PeerLeft   = PeerLeft(peerUid),
        };
        conversationsDirty = true;
        changed = true;

        directService.Open(uid, PlayerProfile.DisplayName, peerUid, peerName, error =>
        {
            if (this == null || error == null) return;
            if (opening.Remove(peerUid)) { conversationsDirty = true; changed = true; }
            onFailed?.Invoke(error);
        });
        return null;
    }

    /// <summary>나가기 — 내 탭이 사라지고, 상대는 더 보낼 수 없게 됩니다. (채팅 창을 닫는 것과는 다름)</summary>
    public void LeaveConversation(string peerUid, Action<string> onDone)
    {
        if (peerUid == null) { onDone?.Invoke(null); return; }
        if (!TryGetSender(out string uid)) { onDone?.Invoke("계정 정보를 확인하는 중입니다. 잠시 후 다시 시도해 주세요."); return; }

        if (opening.Remove(peerUid)) { conversationsDirty = true; changed = true; }

        if (!rooms.TryGetValue(peerUid, out DirectRoom room) || room.MyState != RoomState.In) { onDone?.Invoke(null); return; }
        directService.Leave(uid, peerUid, error => { if (this != null) onDone?.Invoke(error); });
    }

    // ───────────────────────────── 메시지 ─────────────────────────────

    /// <summary>
    /// 메시지를 받을 대화방 (ChatPanel 이 개인 탭을 보는 동안 그 상대, 아니면 null). 한 번에 한 방만 받습니다.
    /// </summary>
    public void WatchConversation(string peerUid)
    {
        if (watchedPeer == peerUid) return;
        watchedPeer = peerUid;
        ApplyWatch();
    }

    /// <summary>그 상대와 주고받은 메시지 (보낸 시각 순). WatchConversation 으로 보고 있는 방만 채워져 있습니다.</summary>
    public IReadOnlyList<ChatMessage> DirectMessagesWith(string peerUid)
        => peerUid != null && peerUid == watchingPeer ? directMessages : NoMessages;

    /// <summary>그 대화를 지금까지 읽은 것으로 표시합니다 (대화 탭을 보고 있는 동안 ChatPanel 이 부름).</summary>
    public void MarkRead(string peerUid)
    {
        if (UnreadCount(peerUid) == 0 || !TryGetSender(out string uid)) return;
        readRequested.Add(peerUid);   // 다음 목록이 올 때까지 다시 보내지 않음
        directService.MarkRead(uid, peerUid);
        changed = true;
    }

    private bool CannotSendDirect(string peerUid, out string error)
    {
        error = DirectUnavailableReason;
        if (error == null && IsBlocked(peerUid))        error = "차단한 유저에게는 메시지를 보낼 수 없습니다.";
        if (error == null && !HasConversation(peerUid)) error = "대화방이 없습니다. 다시 열어 주세요.";
        if (error == null && PeerLeft(peerUid))         error = "상대방이 대화방을 나갔습니다.";
        return error != null;
    }

    private void SendDirect(string uid, string peerUid, ChatMessage message, Action<string> onDone)
    {
        // 상대가 아직 이 방에 들어온 적 없으면 이 메시지로 상대 탭이 생김
        bool invite = !rooms.TryGetValue(peerUid, out DirectRoom room) || room.PeerState == RoomState.None;
        directService.Send(uid, peerUid, message, invite, onDone);
    }

    // ───────────────────────────── 연결 ─────────────────────────────

    private void AwakeDirect()
    {
        directService = service.Direct;
        directService.RoomsReceived    += OnRoomsReceived;
        directService.MessagesReceived += OnDirectMessagesReceived;
        directService.MessagesRemoved  += OnDirectMessagesRemoved;
        directService.Failed           += OnDirectFailed;
    }

    private void DestroyDirect()
    {
        directService.RoomsReceived    -= OnRoomsReceived;
        directService.MessagesReceived -= OnDirectMessagesReceived;
        directService.MessagesRemoved  -= OnDirectMessagesRemoved;
        directService.Failed           -= OnDirectFailed;
        directService.Disconnect();
    }

    /// <summary>매 프레임 (ChatManager.Update) — 채팅 연결을 따라 대화방 리스너를 열고 닫음.</summary>
    private void UpdateDirectConnection()
    {
        if (directKey != null && directKey != connectedKey)
        {
            directService.Disconnect();
            directKey    = null;
            watchingPeer = null;
            changed      = true;
        }

        if (connectedKey != null && directKey == null && Time.unscaledTime >= nextDirectConnectTime)
        {
            directKey        = connectedKey;
            directFailReason = null;
            directService.Connect(connectedKey);   // 실패하면 이 안에서 바로 OnDirectFailed 가 불릴 수 있음
        }

        ApplyWatch();
    }

    /// <summary>보고 싶은 방(watchedPeer)과 실제로 받는 방을 맞춤. 방이 바뀌면 이전 방의 메시지는 비움.</summary>
    private void ApplyWatch()
    {
        string want = directKey != null && HasConversation(watchedPeer) ? watchedPeer : null;
        if (want == watchingPeer) return;

        if (watchingPeer != null) directService.Unwatch();
        directMessages.Clear();
        directIds.Clear();
        watchingPeer = want;
        changed = true;

        if (want != null) directService.Watch(directKey, want, directHistoryCount);
    }

    private void OnDirectFailed(string reason)
    {
        directService.Disconnect();
        directKey             = null;
        watchingPeer          = null;   // 다시 연결되면 보던 방을 다시 받음
        directFailReason      = $"{reason} 잠시 후 다시 연결합니다.";
        nextDirectConnectTime = Time.unscaledTime + retryDelay;
        changed = true;
    }

    private void OnRoomsReceived(IReadOnlyList<DirectRoom> list)
    {
        rooms.Clear();
        foreach (DirectRoom r in list) rooms[r.PeerUid] = r;

        // 서버 목록에 들어온(내가 '대화 중' 인) 방은 여는 중 목록에서 뺌
        var opened = new List<string>();
        foreach (string peer in opening.Keys)
            if (rooms.TryGetValue(peer, out DirectRoom r) && r.MyState == RoomState.In) opened.Add(peer);
        foreach (string peer in opened) opening.Remove(peer);

        readRequested.Clear();
        roomsLoaded        = true;
        conversationsDirty = true;
        changed            = true;
    }

    private void OnDirectMessagesReceived(string peerUid, IReadOnlyList<ChatMessage> received)
    {
        if (peerUid != watchingPeer) return;   // 방을 바꾼 직후 늦게 온 이전 방 메시지
        if (AddMessages(directMessages, directIds, received, directHistoryCount)) changed = true;
    }

    private void OnDirectMessagesRemoved(string peerUid, IReadOnlyList<string> removed)
    {
        if (peerUid != watchingPeer) return;
        if (RemoveMessages(directMessages, directIds, removed)) changed = true;
    }

    private void RebuildConversations()
    {
        if (!conversationsDirty) return;
        conversationsDirty = false;

        conversationList.Clear();
        conversationSet.Clear();

        foreach (DirectRoom r in rooms.Values)
        {
            if (r.MyState != RoomState.In || IsBlocked(r.PeerUid)) continue;
            conversationSet.Add(r.PeerUid);
            conversationList.Add(new Conversation
            {
                PeerUid    = r.PeerUid,
                PeerName   = r.PeerName,
                LastSentAt = r.LastSentAt,
                PeerLeft   = r.PeerState == RoomState.Left,
            });
        }

        foreach (Conversation c in opening.Values)
        {
            if (IsBlocked(c.PeerUid) || !conversationSet.Add(c.PeerUid)) continue;
            conversationList.Add(c);
        }

        conversationList.Sort((a, b) => b.LastSentAt.CompareTo(a.LastSentAt));
    }

    private void ClearDirect()
    {
        if (directKey != null) directService.Disconnect();
        rooms.Clear();
        opening.Clear();
        readRequested.Clear();
        directMessages.Clear();
        directIds.Clear();
        directKey             = null;
        directFailReason      = null;
        nextDirectConnectTime = 0f;
        roomsLoaded           = false;
        watchedPeer           = null;
        watchingPeer          = null;
        conversationsDirty    = true;
    }

    /// <summary>계정이 바뀔 때 (ChatManager.Update) — 이전 계정의 개인 메시지·친구·차단을 비움. 새 계정 것은 다시 연결되면 서버에서 받음.</summary>
    private void ResetAccountState()
    {
        ClearDirect();
        ClearFriends();
    }
}

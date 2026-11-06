using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>친구(뒤끝 친구): 요청 → 상대 수락 시 양쪽에 보임 — 채팅 신원으로 친구·차단 목록을 받고, 목록만 실패하면 채팅은 두고 retryDelay 뒤 재시도.</summary>
public partial class ChatManager
{
    /// <summary>친구 + 보낸 요청 최대 수. 서버 규칙으로는 막지 않는 클라이언트 제한입니다.</summary>
    public const int MAX_FRIENDS = 50;

    private static string FriendsLoading => GameMessages.Friends.loading;

    /// <summary>친구 목록 재수신 간격 — 창을 열 때·실시간 알림 때도 바로 받고, 알림 연결 시엔 놓침 대비라 길게 (뒤끝 호출 3회/간격).</summary>
    private static float FriendsPollSeconds => BackendNotify.IsConnected ? 300f : 120f;
    private float nextFriendsPoll;

    private IFriendService friendService;
    private readonly Connection friendsLink = new Connection();
    private readonly List<Friendship> friendships = new List<Friendship>();
    private bool friendsLoaded;       // 이 계정의 목록을 한 번이라도 받았는가

    /// <summary>친구 목록·차단 목록이 바뀔 때마다 올라갑니다 — 친구 창은 이 값이 바뀔 때만 다시 그립니다.</summary>
    public int FriendsVersion { get; private set; }

    /// <summary>친구 + 받은 요청 + 보낸 요청. 상태 순(받은 요청 → 친구 → 보낸 요청), 그 안에서 이름 순.</summary>
    public IReadOnlyList<Friendship> Friendships => friendships;

    /// <summary>친구 기능을 지금 쓸 수 없는 이유. null 이면 사용 가능.</summary>
    public string FriendsUnavailableReason
    {
        get
        {
            if (UnavailableReason != null) return UnavailableReason;
            if (friendsLink.Key == null)   return friendsLink.FailReason ?? FriendsLoading;
            return friendsLoaded ? null : FriendsLoading;
        }
    }

    public bool TryGetFriendship(string uid, out Friendship friendship)
    {
        foreach (Friendship f in friendships)
            if (f.PeerUid == uid) { friendship = f; return true; }
        friendship = default;
        return false;
    }

    public int CountFriends(FriendState state)
    {
        int n = 0;
        foreach (Friendship f in friendships) if (f.State == state) n++;
        return n;
    }

    // ───────────────────────────── 연결 ─────────────────────────────

    private void AwakeFriends()
    {
        friendService = service.Friends;
        friendService.Received       += OnFriendsReceived;
        friendService.BlocksReceived += OnBlocksReceived;
        friendService.Failed         += OnFriendsFailed;
    }

    private void DestroyFriends()
    {
        friendService.Received       -= OnFriendsReceived;
        friendService.BlocksReceived -= OnBlocksReceived;
        friendService.Failed         -= OnFriendsFailed;
        friendService.Disconnect();
    }

    /// <summary>매 프레임 (ChatManager.Update) — 채팅 연결을 따라 친구 리스너를 열고 닫음.</summary>
    private void UpdateFriendsConnection()
    {
        if (friendsLink.ShouldDisconnect(MyUid))
        {
            friendService.Disconnect();
            friendsLink.Disconnected();
            changed = true;
        }

        if (friendsLink.ShouldConnect(MyUid))
        {
            friendsLink.Connected(MyUid);
            nextFriendsPoll = Time.unscaledTime + FriendsPollSeconds;
            friendService.Connect(MyUid);   // 실패하면 이 안에서 바로 OnFriendsFailed 가 불릴 수 있음
        }

        // 연결돼 있고 한 번 받은 뒤에만 주기적으로 다시 받음 (받는 중이면 서비스가 알아서 한 번으로 합침)
        if (friendsLink.Key != null && friendsLoaded && Time.unscaledTime >= nextFriendsPoll)
        {
            nextFriendsPoll = Time.unscaledTime + FriendsPollSeconds;
            friendService.Refresh();
        }
    }

    /// <summary>친구 목록을 지금 다시 받습니다 (친구 창을 열 때). 연결 전이면 아무것도 안 함.</summary>
    public void RefreshFriends()
    {
        if (friendsLink.Key == null) return;
        nextFriendsPoll = Time.unscaledTime + FriendsPollSeconds;
        friendService.Refresh();
    }

    private void OnFriendsFailed(string reason)
    {
        friendService.Disconnect();
        friendsLink.Failed(ChatText.Fill(GameMessages.Friends.retrying, "reason", reason), retryDelay);
        FriendsVersion++;
        changed = true;
    }

    private void OnFriendsReceived(IReadOnlyList<Friendship> list)
    {
        friendships.Clear();
        friendships.AddRange(list);
        friendships.Sort((a, b) => a.State != b.State
            ? StateOrder(a.State).CompareTo(StateOrder(b.State))
            : string.Compare(a.PeerName, b.PeerName, StringComparison.CurrentCulture));

        friendsLoaded = true;
        FriendsVersion++;
        changed = true;
    }

    /// <summary>목록 순서 — 받은 요청(할 일) → 친구 → 보낸 요청.</summary>
    private static int StateOrder(FriendState s)
    {
        switch (s)
        {
            case FriendState.Incoming: return 0;
            case FriendState.Friend:   return 1;
            default:                   return 2;
        }
    }

    private void ClearFriends()
    {
        if (friendsLink.Key != null) friendService.Disconnect();
        friendsLink.Reset();
        friendships.Clear();
        blocked.Clear();
        friendsLoaded = false;
        FriendsVersion++;
    }

    // ───────────────────────────── 요청 / 수락 / 삭제 ─────────────────────────────

    /// <summary>친구 요청(상대가 이미 요청했으면 수락) — onDone(오류 문구, 성공이면 null).</summary>
    public void RequestFriend(string peerUid, string peerName, Action<string> onDone)
    {
        if (!CanEditFriends(peerUid, out string uid, out string error)) { onDone?.Invoke(error); return; }

        if (TryGetFriendship(peerUid, out Friendship f))
        {
            switch (f.State)
            {
                case FriendState.Friend:   onDone?.Invoke(GameMessages.Friends.alreadyFriend); return;
                case FriendState.Outgoing: onDone?.Invoke(GameMessages.Friends.alreadyRequested); return;
                case FriendState.Incoming: AcceptFriend(peerUid, onDone); return;
            }
        }

        if (IsBlocked(peerUid))                { onDone?.Invoke(GameMessages.Friends.blockedRequest); return; }
        if (FriendLimitReached(out error))     { onDone?.Invoke(error); return; }

        friendService.Request(uid, peerUid, peerName, IfAlive(onDone));
    }

    /// <summary>정확한 닉네임으로 찾아 친구 요청(검색 칸, 이미 요청받았으면 수락) — onDone(오류 문구, 성공이면 null).</summary>
    public void RequestFriendByName(string nickname, Action<string> onDone)
    {
        string name = (nickname ?? string.Empty).Trim();
        if (name.Length == 0)                    { onDone?.Invoke(GameMessages.Friends.enterNickname); return; }
        if (name == PlayerProfile.DisplayName)   { onDone?.Invoke(GameMessages.Friends.selfRequest); return; }
        if (FriendsUnavailableReason != null)    { onDone?.Invoke(FriendsUnavailableReason); return; }

        friendService.FindUser(name, IfAlive<string, string>((uid, error) =>
        {
            if (error != null) { onDone?.Invoke(error); return; }
            RequestFriend(uid, name, onDone);
        }));
    }

    public void AcceptFriend(string peerUid, Action<string> onDone)
    {
        if (!CanEditFriends(peerUid, out string uid, out string error)) { onDone?.Invoke(error); return; }

        bool incoming = TryGetFriendship(peerUid, out Friendship f) && f.State == FriendState.Incoming;
        if (!incoming)                         { onDone?.Invoke(GameMessages.Friends.noIncoming); return; }
        if (FriendLimitReached(out error))     { onDone?.Invoke(error); return; }

        friendService.Accept(uid, peerUid, IfAlive(onDone));
    }

    /// <summary>친구 삭제 · 받은 요청 거절 · 보낸 요청 취소. (상대 친구 창에서도 사라짐)</summary>
    public void RemoveFriend(string peerUid, Action<string> onDone)
    {
        if (!CanEditFriends(peerUid, out string uid, out string error)) { onDone?.Invoke(error); return; }
        if (!TryGetFriendship(peerUid, out _)) { onDone?.Invoke(null); return; }   // 이미 없음

        friendService.Remove(uid, peerUid, IfAlive(onDone));
    }

    private bool CanEditFriends(string peerUid, out string uid, out string error)
    {
        uid   = null;
        error = FriendsUnavailableReason;
        if (error != null) return false;

        if (!TryGetSender(out uid))                          { error = AccountPending; return false; }
        if (string.IsNullOrEmpty(peerUid) || peerUid == uid) { error = GameMessages.Friends.selfRequest; return false; }
        return true;
    }

    /// <summary>친구 + 보낸 요청이 MAX_FRIENDS 에 닿았는가.</summary>
    private bool FriendLimitReached(out string error)
    {
        bool full = CountFriends(FriendState.Friend) + CountFriends(FriendState.Outgoing) >= MAX_FRIENDS;
        error = full ? ChatText.Fill(GameMessages.Friends.maxFriends, "max", MAX_FRIENDS.ToString()) : null;
        return full;
    }
}

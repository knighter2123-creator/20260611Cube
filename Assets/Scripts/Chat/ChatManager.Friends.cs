using System;
using System.Collections.Generic;

/// <summary>
/// 친구 — 요청을 보내고, 상대가 수락하면 양쪽 친구 창에 서로가 보입니다. 서버 구조는 FirestoreFriendService 주석 참고.
///
/// [연결]  채팅이 연결된 신원(MyUid)으로 친구·차단 목록 리스너를 엽니다. 목록만 실패하면 채팅은 그대로 두고 retryDelay 뒤 다시 시도.
/// [이름]  요청·수락 때 서로의 닉네임을 남깁니다. 내가 이름을 바꾸면 다음 목록 갱신 때 내 이름 칸을 고쳐 씁니다 (상대 화면에 반영).
/// </summary>
public partial class ChatManager
{
    /// <summary>친구 + 보낸 요청 최대 수. 서버 규칙으로는 막지 않는 클라이언트 제한입니다.</summary>
    public const int MAX_FRIENDS = 50;

    private const string FriendsLoading = "친구 목록을 불러오는 중입니다...";

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
            friendService.Connect(MyUid);   // 실패하면 이 안에서 바로 OnFriendsFailed 가 불릴 수 있음
        }
    }

    private void OnFriendsFailed(string reason)
    {
        friendService.Disconnect();
        friendsLink.Failed($"{reason} 잠시 후 다시 시도합니다.", retryDelay);
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

        SyncMyName();
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

    /// <summary>관계 문서에 남은 내 이름이 지금 이름과 다르면 고쳐 씀 (상대 친구 창에 새 이름이 보이게).</summary>
    private void SyncMyName()
    {
        if (!TryGetSender(out string uid)) return;
        string name = PlayerProfile.DisplayName;

        // 먼저 모아 둠 — LocalFriendService 는 UpdateMyName 안에서 바로 목록을 다시 보내 friendships 가 바뀜
        var stale = new List<string>();
        foreach (Friendship f in friendships)
            if (f.MyName != name) stale.Add(f.PeerUid);
        foreach (string peer in stale) friendService.UpdateMyName(uid, peer, name);
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

    /// <summary>
    /// 친구 요청을 보냅니다. 상대가 이미 나에게 요청했으면 그 요청을 수락합니다.
    /// 결과는 onDone(오류 문구) — 성공이면 null.
    /// </summary>
    public void RequestFriend(string peerUid, string peerName, Action<string> onDone)
    {
        if (!CanEditFriends(peerUid, out string uid, out string error)) { onDone?.Invoke(error); return; }

        if (TryGetFriendship(peerUid, out Friendship f))
        {
            switch (f.State)
            {
                case FriendState.Friend:   onDone?.Invoke("이미 친구입니다."); return;
                case FriendState.Outgoing: onDone?.Invoke("이미 친구 요청을 보냈습니다."); return;
                case FriendState.Incoming: AcceptFriend(peerUid, onDone); return;
            }
        }

        if (IsBlocked(peerUid))                { onDone?.Invoke("차단한 유저에게는 친구 요청을 보낼 수 없습니다."); return; }
        if (FriendLimitReached(out error))     { onDone?.Invoke(error); return; }

        friendService.Request(uid, PlayerProfile.DisplayName, peerUid, peerName, IfAlive(onDone));
    }

    public void AcceptFriend(string peerUid, Action<string> onDone)
    {
        if (!CanEditFriends(peerUid, out string uid, out string error)) { onDone?.Invoke(error); return; }

        bool incoming = TryGetFriendship(peerUid, out Friendship f) && f.State == FriendState.Incoming;
        if (!incoming)                         { onDone?.Invoke("받은 친구 요청이 없습니다."); return; }
        if (FriendLimitReached(out error))     { onDone?.Invoke(error); return; }

        friendService.Accept(uid, PlayerProfile.DisplayName, peerUid, IfAlive(onDone));
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
        if (string.IsNullOrEmpty(peerUid) || peerUid == uid) { error = "나에게는 친구 요청을 보낼 수 없습니다."; return false; }
        return true;
    }

    /// <summary>친구 + 보낸 요청이 MAX_FRIENDS 에 닿았는가.</summary>
    private bool FriendLimitReached(out string error)
    {
        bool full = CountFriends(FriendState.Friend) + CountFriends(FriendState.Outgoing) >= MAX_FRIENDS;
        error = full ? $"친구는 요청 중인 사람을 포함해 최대 {MAX_FRIENDS}명까지입니다." : null;
        return full;
    }
}

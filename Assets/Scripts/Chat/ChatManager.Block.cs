using System;
using System.Collections.Generic;

/// <summary>
/// 채팅 차단 — 차단한 사람의 전체 채팅을 내 화면에서 숨기고, 그 사람과의 대화방에서 나갑니다 (상대에게는 '나갔다' 로만 보임).
/// 목록은 서버(chatBlocks/{내 UID})에 저장 — 같은 계정이면 어느 기기에서나 같습니다. 친구 목록과 함께 받습니다 (FirestoreFriendService).
/// </summary>
public partial class ChatManager
{
    public const int MAX_BLOCKS = 100;

    private readonly Dictionary<string, string> blocked = new Dictionary<string, string>();   // uid → 차단할 때의 닉네임

    /// <summary>차단한 사람 (uid → 닉네임).</summary>
    public IReadOnlyDictionary<string, string> BlockedUsers => blocked;

    public bool IsBlocked(string uid) => uid != null && blocked.ContainsKey(uid);

    /// <summary>차단하고 그 사람과의 대화방에서 나갑니다. 결과는 onDone(오류 문구) — 성공이면 null.</summary>
    public void Block(string uid, string nickname, Action<string> onDone)
    {
        if (FriendsUnavailableReason != null)       { onDone?.Invoke(FriendsUnavailableReason); return; }
        if (!TryGetSender(out string me))           { onDone?.Invoke(AccountPending); return; }
        if (string.IsNullOrEmpty(uid) || uid == me) { onDone?.Invoke("차단할 수 없는 유저입니다."); return; }
        if (IsBlocked(uid))                         { onDone?.Invoke(null); return; }
        if (blocked.Count >= MAX_BLOCKS)            { onDone?.Invoke($"최대 {MAX_BLOCKS}명까지 차단할 수 있습니다."); return; }

        // 서버 응답을 기다리지 않고 내 화면에서 바로 숨김 — 실패하면 되돌림 (성공이면 리스너가 같은 목록을 다시 보냄)
        SetBlockedLocally(uid, nickname, true);

        friendService.Block(me, uid, nickname, error =>
        {
            if (this == null) return;
            if (error == null) LeaveConversation(uid, null);   // 상대는 더 보낼 수 없게 됨
            else               SetBlockedLocally(uid, nickname, false);
            onDone?.Invoke(error);
        });
    }

    /// <summary>차단 해제. 나갔던 대화방은 그대로 — 다시 대화하려면 개인 메시지로 다시 엽니다.</summary>
    public void Unblock(string uid, Action<string> onDone)
    {
        if (!IsBlocked(uid))               { onDone?.Invoke(null); return; }
        if (!TryGetSender(out string me))  { onDone?.Invoke(AccountPending); return; }

        friendService.Unblock(me, uid, IfAlive(onDone));
    }

    private void SetBlockedLocally(string uid, string nickname, bool on)
    {
        if (on) blocked[uid] = nickname;
        else if (!blocked.Remove(uid)) return;
        MarkBlocksChanged();
    }

    private void OnBlocksReceived(IReadOnlyDictionary<string, string> list)
    {
        blocked.Clear();
        foreach (KeyValuePair<string, string> pair in list) blocked[pair.Key] = pair.Value;
        MarkBlocksChanged();
    }

    private void MarkBlocksChanged()
    {
        conversationsDirty = true;   // 차단한 상대의 대화 탭을 숨김
        FriendsVersion++;            // 친구 창의 차단 목록
        changed = true;              // 채팅 창 — 차단한 사람의 메시지를 숨김
    }
}

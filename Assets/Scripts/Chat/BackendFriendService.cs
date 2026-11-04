using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>
/// 친구 — 뒤끝 친구 기능(Backend.Friend)으로 친구 · 받은 요청 · 보낸 요청을 주고받습니다.
///
/// [신원]  myUid = 뒤끝 유저 inDate (= AuthManager.Uid). 다른 유저를 가리키는 값도 모두 gamer inDate 입니다.
///   지금 뒤끝 세션(Backend.UserInDate)이 그 계정이 아니면 연결하지 않습니다 — 다른 계정의 친구가 섞여 보이지 않게.
///
/// [목록]  뒤끝에는 실시간 알림이 없어 세 목록(친구 / 받은 요청 / 보낸 요청)을 차례로 받아 한 번에 보냅니다.
///   연결할 때, 요청·수락·삭제 뒤, ChatManager 가 부르는 Refresh (친구 창을 열 때 · 일정 간격) 에 다시 받습니다.
///
/// [삭제]  관계 상태에 따라 다른 API — 친구 BreakFriend / 받은 요청 RejectFriend / 보낸 요청 RevokeSentRequest.
/// [이름]  닉네임은 뒤끝이 목록에 매번 최신 값을 실어 주므로 UpdateMyName 은 할 일이 없습니다.
///
/// [차단]  아직 서버 저장이 없습니다 — 이 기기의 PlayerPrefs 에 계정별로 둡니다 (다른 기기에는 안 보임).
///   TODO(뒤끝 이전 D단계): 게임 정보 테이블로 옮겨 계정 공통으로.
/// [약속]  IFriendService 와 같음 — 모든 이벤트·콜백은 메인 스레드 (BackendManager.Call).
/// </summary>
public class BackendFriendService : IFriendService
{
    public event Action<IReadOnlyList<Friendship>>         Received;
    public event Action<IReadOnlyDictionary<string, string>> BlocksReceived;
    public event Action<string>                             Failed;

    // 한 번에 받는 목록 길이. 클라이언트 친구 제한(ChatManager.MAX_FRIENDS = 50)과 맞춤
    private const int    PAGE        = 50;
    private const string PREF_BLOCKS = "friends.blocks.";   // + 내 uid
    private const string NoName      = "(이름 없음)";

    private string myUid;
    private int    generation;      // Connect/Disconnect 마다 올림 — 이전 계정의 늦은 응답을 버리기 위해
    private bool   loadedOnce;
    private bool   refreshing;
    private bool   refreshAgain;

    private readonly Dictionary<string, Friendship> current = new Dictionary<string, Friendship>();
    private readonly Dictionary<string, string>     blocks  = new Dictionary<string, string>();

    // ───────────────────────────── 연결 ─────────────────────────────

    public void Connect(string uid)
    {
        Disconnect();

        string reason = SessionProblem(uid);
        if (reason != null) { Failed?.Invoke(reason); return; }

        myUid = uid;
        LoadBlocks();
        BlocksReceived?.Invoke(new Dictionary<string, string>(blocks));
        Refresh();
    }

    public void Disconnect()
    {
        myUid = null;
        generation++;
        loadedOnce   = false;
        refreshing   = false;
        refreshAgain = false;
        current.Clear();
        blocks.Clear();
    }

    /// <summary>지금 이 계정으로 서버에 물을 수 있는가. 안 되면 화면에 보일 이유.</summary>
    private static string SessionProblem(string uid)
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsSignedIn)                       return "로그인하면 친구 기능을 쓸 수 있습니다.";
        if (!auth.IsOnline)                                         return "서버에 연결할 수 없습니다.";
        if (string.IsNullOrEmpty(uid) || uid != Backend.UserInDate) return "계정 정보를 확인하는 중입니다.";
        return null;
    }

    // ───────────────────────────── 목록 ─────────────────────────────

    public void Refresh()
    {
        if (myUid == null) return;
        if (refreshing) { refreshAgain = true; return; }

        refreshing = true;
        int gen = generation;
        var list = new Dictionary<string, Friendship>();

        // 친구 → 받은 요청 → 보낸 요청 순. 같은 사람이 겹치면 앞의 상태를 씁니다.
        // 개수 지정(PAGE)이 서버 허용치를 넘으면 400 → 기본 개수로 한 번 더 (fallback)
        Fetch(cb => Backend.Friend.GetFriendList(PAGE, cb), cb => Backend.Friend.GetFriendList(cb),
              FriendState.Friend, gen, list, e1 =>
        {
            if (e1 != null) { FinishRefresh(gen, null, e1); return; }
            Fetch(cb => Backend.Friend.GetReceivedRequestList(PAGE, cb), cb => Backend.Friend.GetReceivedRequestList(cb),
                  FriendState.Incoming, gen, list, e2 =>
            {
                if (e2 != null) { FinishRefresh(gen, null, e2); return; }
                Fetch(cb => Backend.Friend.GetSentRequestList(PAGE, cb), cb => Backend.Friend.GetSentRequestList(cb),
                      FriendState.Outgoing, gen, list, e3 => FinishRefresh(gen, e3 == null ? list : null, e3));
            });
        });
    }

    private void Fetch(Action<Backend.BackendCallback> request, Action<Backend.BackendCallback> fallback, FriendState state, int gen,
                       Dictionary<string, Friendship> into, Action<string> next)
    {
        BackendManager.Call(request, bro =>
        {
            if (gen != generation) return;   // 그 사이 끊김·계정 전환

            if (fallback != null && bro != null && bro.GetStatusCode() == "400")
            {
                Debug.LogWarning($"[Friends] {state} 목록을 {PAGE}개씩 받을 수 없어 기본 개수로 받습니다: {BackendManager.Describe(bro)}");
                Fetch(fallback, null, state, gen, into, next);
                return;
            }

            // 목록이 비었을 때 404 로 오는 경우도 '없음' 으로 봄
            if (bro != null && bro.GetStatusCode() == "404") { next(null); return; }
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Friends] {state} 목록 조회 실패: {BackendManager.Describe(bro)}");
                next(BackendManager.IsNetworkError(bro) ? "서버에 연결할 수 없습니다." : "친구 목록을 불러오지 못했습니다.");
                return;
            }

            string myName = PlayerProfile.DisplayName;
            JsonData rows = bro.FlattenRows();
            for (int i = 0; rows != null && i < rows.Count; i++)
            {
                string peer = BackendJson.Str(rows[i], "inDate");
                if (string.IsNullOrEmpty(peer) || into.ContainsKey(peer)) continue;

                string name = BackendJson.Str(rows[i], "nickname");
                into[peer] = new Friendship
                {
                    PeerUid  = peer,
                    PeerName = string.IsNullOrEmpty(name) ? NoName : name,
                    MyName   = myName,   // 서버가 늘 최신 닉네임을 주므로 고쳐 쓸 일 없음 (ChatManager.SyncMyName 이 건너뜀)
                    State    = state,
                };
            }
            next(null);
        });
    }

    private void FinishRefresh(int gen, Dictionary<string, Friendship> list, string error)
    {
        if (gen != generation) return;
        refreshing = false;

        if (error != null)
        {
            // 처음 받기에 실패하면 ChatManager 가 끊고 잠시 뒤 다시 연결 / 이미 받은 목록이 있으면 그대로 두고 다음 갱신을 기다림
            if (!loadedOnce) { Failed?.Invoke(error); return; }
        }
        else
        {
            loadedOnce = true;
            current.Clear();
            foreach (KeyValuePair<string, Friendship> pair in list) current[pair.Key] = pair.Value;
            Received?.Invoke(new List<Friendship>(current.Values));
        }

        if (refreshAgain)
        {
            refreshAgain = false;
            Refresh();
        }
    }

    // ───────────────────────────── 요청 / 수락 / 삭제 ─────────────────────────────

    public void Request(string uid, string myName, string peerUid, string peerName, Action<string> onDone)
        => Act(uid, cb => Backend.Friend.RequestFriend(peerUid, cb), "친구 요청", onDone);

    public void Accept(string uid, string myName, string peerUid, Action<string> onDone)
        => Act(uid, cb => Backend.Friend.AcceptFriend(peerUid, cb), "친구 수락", onDone);

    public void Remove(string uid, string peerUid, Action<string> onDone)
    {
        if (!current.TryGetValue(peerUid, out Friendship f)) { onDone?.Invoke(null); return; }   // 이미 없음

        switch (f.State)
        {
            case FriendState.Friend:   Act(uid, cb => Backend.Friend.BreakFriend(peerUid, cb),       "친구 삭제", onDone); break;
            case FriendState.Incoming: Act(uid, cb => Backend.Friend.RejectFriend(peerUid, cb),      "요청 거절", onDone); break;
            default:                   Act(uid, cb => Backend.Friend.RevokeSentRequest(peerUid, cb), "요청 취소", onDone); break;
        }
    }

    public void UpdateMyName(string uid, string peerUid, string myName) { }   // 클래스 주석 [이름]

    /// <summary>닉네임 → gamer inDate (Backend.Social.GetUserInfoByNickName). 닉네임은 정확히 같아야 합니다.</summary>
    public void FindUser(string nickname, Action<string, string> onDone)
    {
        if (myUid == null) { onDone?.Invoke(null, "친구 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요."); return; }

        int gen = generation;
        BackendManager.Call(cb => Backend.Social.GetUserInfoByNickName(nickname, cb), bro =>
        {
            if (gen != generation) { onDone?.Invoke(null, "계정이 바뀌어 처리하지 않았습니다."); return; }

            if (bro != null && bro.GetStatusCode() == "404") { onDone?.Invoke(null, "그 닉네임의 유저가 없습니다."); return; }
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Friends] 닉네임 검색 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(null, BackendManager.IsNetworkError(bro) ? "서버에 연결할 수 없습니다." : "유저를 찾지 못했습니다.");
                return;
            }

            string inDate = null;
            try { inDate = BackendJson.Str(BackendJson.Get(bro.GetReturnValuetoJSON(), "row"), "inDate"); }
            catch (Exception e) { Debug.LogWarning($"[Friends] 닉네임 검색 응답을 읽지 못함: {e.Message}\n{bro}"); }

            if (string.IsNullOrEmpty(inDate)) onDone?.Invoke(null, "그 닉네임의 유저가 없습니다.");
            else                              onDone?.Invoke(inDate, null);
        });
    }

    /// <summary>서버 작업 하나 — 끝나면 목록을 다시 받아 양쪽 상태를 맞춥니다. onDone(오류 문구, 성공이면 null).</summary>
    private void Act(string uid, Action<Backend.BackendCallback> request, string what, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke("친구 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요."); return; }

        int gen = generation;
        BackendManager.Call(request, bro =>
        {
            if (gen != generation) { onDone?.Invoke("계정이 바뀌어 처리하지 않았습니다."); return; }

            string error = null;
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Friends] {what} 실패: {BackendManager.Describe(bro)}");
                error = ToMessage(bro, what);
            }

            onDone?.Invoke(error);
            Refresh();   // 실패해도 다시 받음 — 상대가 먼저 처리했으면(이미 친구 등) 목록이 맞춰짐
        });
    }

    private static string ToMessage(BackendReturnObject bro, string what)
    {
        if (BackendManager.IsNetworkError(bro)) return "서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";

        switch (bro.GetStatusCode())
        {
            case "409": return "이미 친구이거나 요청이 오간 상대입니다.";
            case "404": return "상대를 찾을 수 없거나 요청이 이미 처리되었습니다.";
            case "412": return "친구 수가 가득 찼습니다.";
            default:    return $"{what}에 실패했습니다. ({bro.GetStatusCode()})";
        }
    }

    // ───────────────────────────── 차단 (이 기기 · 계정별) ─────────────────────────────

    public void Block(string uid, string peerUid, string peerName, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke("친구 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요."); return; }
        blocks[peerUid] = peerName ?? NoName;
        SaveBlocks();
        onDone?.Invoke(null);
    }

    public void Unblock(string uid, string peerUid, Action<string> onDone)
    {
        if (myUid == null || uid != myUid) { onDone?.Invoke("친구 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요."); return; }
        if (blocks.Remove(peerUid)) SaveBlocks();
        onDone?.Invoke(null);
    }

    [Serializable] private class BlockList { public List<string> uids = new List<string>(); public List<string> names = new List<string>(); }

    private void LoadBlocks()
    {
        blocks.Clear();
        string json = PlayerPrefs.GetString(PREF_BLOCKS + myUid, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            BlockList saved = JsonUtility.FromJson<BlockList>(json);
            for (int i = 0; saved != null && i < saved.uids.Count && i < saved.names.Count; i++)
                blocks[saved.uids[i]] = saved.names[i];
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Friends] 차단 목록을 읽지 못해 비웁니다: {e.Message}");
        }
    }

    private void SaveBlocks()
    {
        var list = new BlockList();
        foreach (KeyValuePair<string, string> pair in blocks) { list.uids.Add(pair.Key); list.names.Add(pair.Value); }
        PlayerPrefs.SetString(PREF_BLOCKS + myUid, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
        BlocksReceived?.Invoke(new Dictionary<string, string>(blocks));
    }
}

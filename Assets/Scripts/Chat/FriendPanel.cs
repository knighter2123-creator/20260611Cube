using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 친구 창 — 받은 친구 요청(수락/거절) / 친구 목록(메시지/삭제) / 보낸 요청(취소) / 차단한 유저(해제).
/// 왼쪽 위 [차단 목록] 버튼으로 친구 목록 ↔ 차단 목록을 바꿔 봅니다.
///
/// [붙이는 위치]  ChatCanvas (ChatPanel 과 같은 오브젝트 — 항상 켜져 있어야 Awake 가 돎). 창(root)은 ChatCanvas 바로 아래
///   (채팅 창 Panel 밖 — 채팅을 열지 않아도 열림). Tools/UI 굽기/채팅 탭·친구 창 이 컴포넌트를 붙이고 UI 를 만들어 칸을 채웁니다.
///   랭킹 버튼 옆의 [친구] 버튼(OpenFriendsButton)으로 엽니다.
///
///   Friends                  ← root (꺼 둠)
///     Dim (Button)           ← 누르면 닫힘
///     Window
///       Title / BlockList (Button + TMP) / Close (Button)
///       Viewport (ScrollRect)
///         Content            ← content
///           Header (꺼 둠)    ← headerTemplate. 구역 제목
///           Row (꺼 둠)       ← rowTemplate (FriendRow)
///         Status             ← statusText
///       Notice               ← noticeText. 평소엔 안내 문구, 실패·완료 문구를 잠깐 보여 줌
///
/// [데이터]  ChatManager.Friendships / BlockedUsers 를 그리기만 합니다. ChatManager.FriendsVersion 이 바뀔 때만 다시 그립니다
///   (Changed 는 채팅 메시지마다 오므로 그것으로 다시 그리지 않음).
/// [삭제]  친구 [삭제] 는 한 번 더 눌러야 지워집니다 (confirmSeconds 안에) — 잘못 눌러 지우지 않게.
/// </summary>
public class FriendPanel : MonoBehaviour
{
    [Header("UI (Tools/UI 굽기/채팅 탭·친구 창 이 채움)")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button dimButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private Button blockListButton;
    [SerializeField] private TextMeshProUGUI blockListLabel;
    [SerializeField] private RectTransform content;
    [SerializeField] private TextMeshProUGUI headerTemplate;
    [SerializeField] private FriendRow rowTemplate;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI noticeText;
    [SerializeField] private ChatPanel chatPanel;

    [Header("닉네임으로 친구 요청 (Tools/UI 굽기/친구 창 - 닉네임 검색 칸 이 채움 · 비워 두면 숨김)")]
    [SerializeField] private TMP_InputField searchInput;
    [SerializeField] private Button         searchButton;

    [SerializeField] private float noticeDuration = 3f;
    [Tooltip("[삭제] 를 누른 뒤 이 시간(초) 안에 한 번 더 누르면 삭제")]
    [SerializeField] private float confirmSeconds = 3f;

    /// <summary>
    /// 코드가 실행 중에 넣는 글자. {count} {max} 자리는 실행 중에 값으로 바뀝니다.
    /// 창·줄의 모양(크기·위치·폰트·배경색)은 하이어라키(ChatCanvas > Friends)에서 직접 고치세요.
    /// </summary>
    [Serializable]
    private class Texts
    {
        [Header("제목 · 전환 버튼")]
        [Tooltip("비워 두면 하이어라키의 Title 글자를 그대로 씀")]
        public string friendsTitle     = "";
        public string blockedTitle     = "차단 목록";
        [Tooltip("차단 목록을 보는 중일 때 전환 버튼 글자")]
        public string toFriendList     = "친구 목록";
        [Tooltip("친구 목록을 보는 중일 때 전환 버튼 글자. {count} = 차단한 수")]
        public string toBlockList      = "차단 목록 {count}";

        [Header("구역 제목")]
        public string incomingHeader   = "받은 친구 요청";
        [Tooltip("{count} = 친구 수, {max} = 최대")]
        public string friendsHeader    = "친구 {count}/{max}";
        public string outgoingHeader   = "보낸 친구 요청";
        [Tooltip("{count} = 차단한 수, {max} = 최대")]
        public string blockedHeader    = "차단한 유저 {count}/{max}";

        [Header("줄 버튼")]
        public string accept           = "수락";
        public string decline          = "거절";
        public string message          = "메시지";
        public string remove           = "삭제";
        [Tooltip("[삭제] 를 한 번 누른 뒤의 글자")]
        public string removeConfirm    = "정말 삭제?";
        public string cancelRequest    = "취소";
        public string unblock          = "해제";

        [Header("빈 목록 · 안내")]
        public string noFriends        = "아직 친구가 없습니다.";
        public string noBlocked        = "차단한 유저가 없습니다.";
        [Tooltip("검색 칸이 있을 때 아래 안내 문구 (검색 칸이 없으면 하이어라키의 Notice 글자를 씀)")]
        public string searchHint       = "상대의 닉네임을 정확히 입력하고 [친구 요청] 을 누르세요. 친구가 되면 [메시지] 로 대화할 수 있습니다.";
        [Tooltip("차단 목록을 볼 때 아래 안내 문구")]
        public string blockHint        = "차단한 유저의 채팅은 내 화면에서만 보이지 않습니다. [해제] 를 누르면 다시 보입니다.";

        [Header("알림 (아래 안내 칸에 잠깐 뜨는 문구) — {name} = 상대 닉네임")]
        [Tooltip("[수락] 또는 검색으로 요청했는데 상대가 이미 나에게 요청해 둔 경우")]
        public string noticeAccepted   = "{name} 님과 친구가 되었습니다.";
        [Tooltip("닉네임 검색으로 친구 요청을 보냈을 때")]
        public string noticeRequested  = "{name} 님에게 친구 요청을 보냈습니다.";
        public string noticeDeclined   = "친구 요청을 거절했습니다.";
        public string noticeCanceled   = "친구 요청을 취소했습니다.";
        public string noticeRemoved    = "{name} 님을 친구에서 삭제했습니다.";
        public string noticeUnblocked  = "{name} 님의 차단을 해제했습니다.";
    }

    [Header("글자 · 색 (실행 중에 코드가 넣는 값)")]
    [SerializeField] private Texts texts = new Texts();
    [Tooltip("구역 제목 글자 색 (받은 친구 요청 / 친구 n/50 ...)")]
    [SerializeField] private Color headerColor = ChatText.ProgressColor;

    private bool searching;   // 검색·요청 중 — 연타 방지

    private readonly List<GameObject> rows = new List<GameObject>();
    private bool   ready;
    private bool   showBlocked;        // true = 차단 목록을 보는 중
    private string hint;               // 굽기 때 넣은 안내 문구 — 알림이 끝나면 되돌림
    private string friendsTitle;       // 굽기 때 넣은 제목 ("친구")
    private float  noticeUntil = float.NegativeInfinity;

    private int    drawnVersion = -1;
    private string drawnReason;
    private string confirmUid;         // [삭제] 를 한 번 누른 친구
    private float  confirmUntil;

    private bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        ready = root != null && content != null && rowTemplate != null && headerTemplate != null && statusText != null;
        if (!ready)
        {
            Debug.LogError("[FriendPanel] UI 칸이 비어 있습니다. StageScene 을 열고 Tools/UI 굽기/채팅 탭·친구 창 메뉴로 만든 뒤 씬을 저장하세요.", this);
            return;
        }

        AddListenerButton();

        // 검색 칸이 있으면 안내 문구도 그에 맞춤 (굽기 때 넣은 문구는 '채팅에서 누르기' 안내)
        hint         = searchInput != null ? texts.searchHint : noticeText != null ? noticeText.text : null;
        friendsTitle = !string.IsNullOrEmpty(texts.friendsTitle) ? texts.friendsTitle
                     : titleText != null ? titleText.text : null;
        headerTemplate.gameObject.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    void AddListenerButton()
    {
        if (dimButton   != null) dimButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (blockListButton != null) blockListButton.onClick.AddListener(ToggleBlocked);

        if (searchButton != null) searchButton.onClick.AddListener(OnSearch);
        if (searchInput  != null) searchInput.onSubmit.AddListener(_ => OnSearch());
    }

    public void Open()
    {
        if (!ready) return;
        root.SetActive(true);
        root.transform.SetAsLastSibling();   // 채팅 창 위
        confirmUid  = null;
        showBlocked = false;
        noticeUntil = float.NegativeInfinity;
        // 열 때 최신 목록을 받음 — 실시간 알림을 놓쳤어도 맞춰지게 (유니티 오브젝트라 ?. 대신 != null)
        ChatManager chat = ChatManager.Instance;
        if (chat != null) chat.RefreshFriends();
        Draw();
    }

    private void ToggleBlocked()
    {
        showBlocked = !showBlocked;
        confirmUid  = null;
        noticeUntil = float.NegativeInfinity;   // 바뀐 화면의 안내 문구로
        Draw();
    }

    private string CurrentHint => showBlocked ? texts.blockHint : hint;

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        ChatManager chat = ChatManager.Instance;
        int    version = chat != null ? chat.FriendsVersion : -1;
        string reason  = chat != null ? chat.FriendsUnavailableReason : null;
        if (version != drawnVersion || reason != drawnReason) Draw();

        if (confirmUid != null && Time.unscaledTime >= confirmUntil)
        {
            confirmUid = null;
            Draw();
        }

        if (noticeText != null && Time.unscaledTime >= noticeUntil && noticeText.text != CurrentHint)
            noticeText.text = CurrentHint;
    }

    // ───────────────────────────── 그리기 ─────────────────────────────

    private void Draw()
    {
        ClearRows();

        ChatManager chat = ChatManager.Instance;
        drawnVersion = chat != null ? chat.FriendsVersion : -1;
        drawnReason  = chat != null ? chat.FriendsUnavailableReason : null;

        int blockedCount = chat != null ? chat.BlockedUsers.Count : 0;
        if (titleText      != null) titleText.text      = showBlocked ? texts.blockedTitle : friendsTitle;
        if (blockListLabel != null) blockListLabel.text = showBlocked ? texts.toFriendList
                                                                      : ChatText.Fill(texts.toBlockList, "count", blockedCount.ToString());

        if (chat == null)
        {
            ShowStatus(ChatText.NoManager);
            return;
        }

        if (showBlocked)
        {
            BlockedSection(chat, ChatText.Fill(texts.blockedHeader, "count", blockedCount.ToString(), "max", ChatManager.MAX_BLOCKS.ToString()));
        }
        else
        {
            int friends = chat.CountFriends(FriendState.Friend);
            Section(chat, FriendState.Incoming, texts.incomingHeader);
            Section(chat, FriendState.Friend,   ChatText.Fill(texts.friendsHeader, "count", friends.ToString(), "max", ChatManager.MAX_FRIENDS.ToString()));
            Section(chat, FriendState.Outgoing, texts.outgoingHeader);
        }

        bool nothingLoaded = showBlocked ? blockedCount == 0 : chat.Friendships.Count == 0;
        if (drawnReason != null && nothingLoaded) ShowStatus(drawnReason);
        else if (rows.Count == 0) ShowStatus(showBlocked ? texts.noBlocked : texts.noFriends);
        else ShowStatus(string.Empty);
    }

    private void BlockedSection(ChatManager chat, string title)
    {
        if (chat.BlockedUsers.Count == 0) return;

        Header(title);
        var sorted = new List<KeyValuePair<string, string>>(chat.BlockedUsers);
        sorted.Sort((a, b) => string.Compare(a.Value, b.Value, StringComparison.CurrentCulture));
        foreach (KeyValuePair<string, string> pair in sorted)
        {
            string uid = pair.Key, name = pair.Value;
            Row(name, null, null, texts.unblock, () => chat.Unblock(uid, Result(Named(texts.noticeUnblocked, name))));
        }
    }

    private void Section(ChatManager chat, FriendState state, string title)
    {
        bool any = false;
        foreach (Friendship f in chat.Friendships)
        {
            if (f.State != state) continue;
            if (!any) { Header(title); any = true; }

            string uid = f.PeerUid, name = f.PeerName;
            switch (state)
            {
                case FriendState.Incoming:
                    Row(name, texts.accept,  () => chat.AcceptFriend(uid, Result(Named(texts.noticeAccepted, name))),
                              texts.decline, () => chat.RemoveFriend(uid, Result(texts.noticeDeclined)));
                    break;

                case FriendState.Friend:
                    Row(name, texts.message, () => OpenDirect(uid, name),
                              confirmUid == uid ? texts.removeConfirm : texts.remove, () => OnRemoveFriend(chat, uid, name));
                    break;

                case FriendState.Outgoing:
                    Row(name, null, null, texts.cancelRequest, () => chat.RemoveFriend(uid, Result(texts.noticeCanceled)));
                    break;
            }
        }
    }

    /// <summary>[친구 요청] — 입력한 닉네임의 유저에게 요청 (이미 나에게 요청한 사람이면 수락).</summary>
    private void OnSearch()
    {
        if (searching || searchInput == null) return;

        ChatManager chat = ChatManager.Instance;
        if (chat == null) { Notice(ChatText.NoManager); return; }

        string name = searchInput.text.Trim();
        // 이미 나에게 요청한 사람이면 RequestFriendByName 이 '수락' 으로 처리함 — 완료 문구를 고르려고 미리 봐 둠
        bool accepting = chat.TryGetFriendship(PeerUidByName(chat, name), out Friendship before) && before.State == FriendState.Incoming;
        searching = true;
        if (searchButton != null) searchButton.interactable = false;

        chat.RequestFriendByName(name, error =>
        {
            if (this == null) return;
            searching = false;
            if (searchButton != null) searchButton.interactable = true;

            if (error == null)
            {
                searchInput.text = string.Empty;
                Notice(Named(accepting ? texts.noticeAccepted : texts.noticeRequested, name));
            }
            else Notice(error);
        });
    }

    private static string PeerUidByName(ChatManager chat, string name)
    {
        foreach (Friendship f in chat.Friendships) if (f.PeerName == name) return f.PeerUid;
        return null;
    }

    private void OnRemoveFriend(ChatManager chat, string uid, string name)
    {
        if (confirmUid != uid)
        {
            confirmUid   = uid;
            confirmUntil = Time.unscaledTime + confirmSeconds;
            Draw();
            return;
        }

        confirmUid = null;
        chat.RemoveFriend(uid, Result(Named(texts.noticeRemoved, name)));
        Draw();
    }

    private void OpenDirect(string uid, string name)
    {
        Close();
        if (chatPanel != null) chatPanel.OpenDirect(uid, name);
    }

    /// <summary>알림 문구의 {name} 을 닉네임으로 (다른 사람이 정한 글자라 리치 텍스트로 해석하지 않게 감쌈).</summary>
    private static string Named(string template, string name) => ChatText.Fill(template, "name", ChatText.Plain(name));

    /// <summary>서버 작업이 끝나면 실패 문구, 성공이면 done 을 알림으로 보여 주는 콜백.</summary>
    private Action<string> Result(string done) => error =>
    {
        if (this == null) return;
        Notice(error ?? done);
    };

    private void Header(string title)
    {
        TextMeshProUGUI h = Instantiate(headerTemplate, content);
        h.gameObject.SetActive(true);
        h.text = ChatText.Colored(headerColor, title);
        rows.Add(h.gameObject);
    }

    private void Row(string name, string primary, Action onPrimary, string secondary, Action onSecondary)
    {
        FriendRow row = Instantiate(rowTemplate, content);
        row.gameObject.SetActive(true);
        row.Set(ChatText.Plain(name),primary, onPrimary, secondary, onSecondary);
        rows.Add(row.gameObject);
    }

    private void Notice(string message)
    {
        if (noticeText == null) return;
        noticeText.text = message;
        noticeUntil = Time.unscaledTime + noticeDuration;
    }

    private void ShowStatus(string message)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void ClearRows()
    {
        foreach (GameObject row in rows) Destroy(row);
        rows.Clear();
    }
}

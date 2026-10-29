using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 친구 창 — 받은 친구 요청(수락/거절) / 친구 목록(메시지/삭제) / 보낸 요청(취소) / 차단한 유저(해제).
/// 왼쪽 위 [차단 목록] 버튼으로 친구 목록 ↔ 차단 목록을 바꿔 봅니다. 버튼이 없는 옛 씬이면 차단한 유저를 친구 목록 아래에 붙입니다.
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

    [SerializeField] private float noticeDuration = 3f;
    [Tooltip("[삭제] 를 누른 뒤 이 시간(초) 안에 한 번 더 누르면 삭제")]
    [SerializeField] private float confirmSeconds = 3f;

    private const string HeaderHex = "#FFD933";
    private const string BlockHint = "차단한 유저의 채팅은 내 화면에서만 보이지 않습니다. [해제] 를 누르면 다시 보입니다.";

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

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        ready = root != null && content != null && rowTemplate != null && headerTemplate != null && statusText != null;
        if (!ready)
        {
            Debug.LogError("[FriendPanel] UI 칸이 비어 있습니다. StageScene 을 열고 Tools/UI 굽기/채팅 탭·친구 창 메뉴로 만든 뒤 씬을 저장하세요.", this);
            return;
        }

        if (dimButton   != null) dimButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (blockListButton != null) blockListButton.onClick.AddListener(ToggleBlocked);

        hint         = noticeText != null ? noticeText.text : null;
        friendsTitle = titleText  != null ? titleText.text  : null;
        headerTemplate.gameObject.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    public void Open()
    {
        if (!ready) return;
        root.SetActive(true);
        root.transform.SetAsLastSibling();   // 채팅 창 위
        confirmUid  = null;
        showBlocked = false;
        noticeUntil = float.NegativeInfinity;
        Draw();
    }

    private void ToggleBlocked()
    {
        showBlocked = !showBlocked;
        confirmUid  = null;
        noticeUntil = float.NegativeInfinity;   // 바뀐 화면의 안내 문구로
        Draw();
    }

    private string CurrentHint => showBlocked ? BlockHint : hint;

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
        if (titleText      != null) titleText.text      = showBlocked ? "차단 목록" : friendsTitle;
        if (blockListLabel != null) blockListLabel.text = showBlocked ? "친구 목록" : $"차단 목록 {blockedCount}";

        if (chat == null)
        {
            ShowStatus("채팅 시스템을 찾을 수 없습니다.");
            return;
        }

        if (showBlocked)
        {
            BlockedSection(chat, $"차단한 유저 {blockedCount}/{ChatManager.MAX_BLOCKS}");
        }
        else
        {
            int friends = chat.CountFriends(FriendState.Friend);
            Section(chat, FriendState.Incoming, "받은 친구 요청");
            Section(chat, FriendState.Friend,   $"친구 {friends}/{ChatManager.MAX_FRIENDS}");
            Section(chat, FriendState.Outgoing, "보낸 친구 요청");
            if (blockListButton == null) BlockedSection(chat, "차단한 유저");   // 전환 버튼이 없는 옛 씬
        }

        bool nothingLoaded = showBlocked ? blockedCount == 0 : chat.Friendships.Count == 0;
        if (drawnReason != null && nothingLoaded) ShowStatus(drawnReason);
        else if (rows.Count == 0) ShowStatus(showBlocked ? "차단한 유저가 없습니다." : "아직 친구가 없습니다.");
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
            Row(name, null, null, "해제", () => chat.Unblock(uid, Result($"{ChatPanel.Plain(name)} 님의 차단을 해제했습니다.")));
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
                    Row(name, "수락", () => chat.AcceptFriend(uid, Result($"{ChatPanel.Plain(name)} 님과 친구가 되었습니다.")),
                              "거절", () => chat.RemoveFriend(uid, Result("친구 요청을 거절했습니다.")));
                    break;

                case FriendState.Friend:
                    Row(name, "메시지", () => OpenDirect(uid, name),
                              confirmUid == uid ? "정말 삭제?" : "삭제", () => OnRemoveFriend(chat, uid, name));
                    break;

                case FriendState.Outgoing:
                    Row(name, null, null, "취소", () => chat.RemoveFriend(uid, Result("친구 요청을 취소했습니다.")));
                    break;
            }
        }
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
        chat.RemoveFriend(uid, Result($"{ChatPanel.Plain(name)} 님을 친구에서 삭제했습니다."));
        Draw();
    }

    private void OpenDirect(string uid, string name)
    {
        Close();
        if (chatPanel != null) chatPanel.OpenDirect(uid, name);
    }

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
        h.text = $"<color={HeaderHex}>{title}</color>";
        rows.Add(h.gameObject);
    }

    private void Row(string name, string primary, Action onPrimary, string secondary, Action onSecondary)
    {
        FriendRow row = Instantiate(rowTemplate, content);
        row.gameObject.SetActive(true);
        row.Set(ChatPanel.Plain(name), primary, onPrimary, secondary, onSecondary);
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

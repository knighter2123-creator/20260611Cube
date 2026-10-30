using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채팅 창 왼쪽 채널 탭 — 맨 위 "전체 채팅", 그 아래 개인 채팅 상대 닉네임 (최근 대화 순, 안 읽은 수 표시).
/// 탭을 누르면 그 채널의 메시지를 보여 주고, 보내는 메시지도 그 채널로 갑니다.
/// 개인 탭을 보는 동안에는 오른쪽 위 [나가기] 가 보입니다 — 누르면 바로 대화방에서 나가고 탭이 사라집니다.
///
/// [씬 구성]  Tools/UI 굽기/채팅 탭·친구 창 이 만듭니다.
///   Window
///     Sidebar
///       Tabs (ScrollRect)
///         TabList (VerticalLayoutGroup)   ← tabList
///           Tab (Button, 꺼 둠)            ← tabTemplate. 첫 자식 TMP 가 이름
///     Leave (Button)                      ← leaveButton
/// </summary>
public partial class ChatPanel
{
    [Header("채널 탭 (Tools/UI 굽기/채팅 탭·친구 창 이 채움)")]
    [SerializeField] private RectTransform tabList;
    [Tooltip("탭 하나의 모양. 꺼 둔 채로 tabList 아래에 둡니다.")]
    [SerializeField] private Button tabTemplate;
    [Tooltip("창 제목 — 지금 채널 이름을 보여 줌 (비워 두면 바꾸지 않음)")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("개인 채팅 나가기. 개인 탭을 보는 동안에만 보입니다.")]
    [SerializeField] private Button leaveButton;

    // 탭 색. UIBaker 도 같은 값으로 굽습니다.
    public static readonly Color TabColor         = new Color(1f, 1f, 1f, 0.06f);
    public static readonly Color TabSelectedColor = new Color(0.30f, 0.55f, 0.95f, 0.9f);

    private const string PublicTabName = "전체 채팅";

    private string currentPeer;       // 보고 있는 개인 채팅 상대 (null = 전체 채팅)
    private string currentPeerName;

    /// <summary>탭 오브젝트 하나와 그 탭이 가리키는 채널.</summary>
    private sealed class Tab
    {
        public Button          Button;
        public TextMeshProUGUI Label;
        public string          Peer;   // null = 전체 채팅
        public string          Name;
    }

    // 탭 풀 — [0] 은 항상 전체 채팅. 대화가 줄면 남는 탭은 꺼 둠
    private readonly List<Tab> tabs = new List<Tab>();

    private bool HasTabs => tabList != null && tabTemplate != null;

    private void AwakeTabs()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(OnLeaveClicked);
            leaveButton.gameObject.SetActive(false);
        }
        if (HasTabs) tabTemplate.gameObject.SetActive(false);
    }

    /// <summary>그 상대와의 개인 채팅을 엽니다 (창이 닫혀 있으면 창도 엶). 유저 메뉴·친구 창에서 부릅니다.</summary>
    public void OpenDirect(string peerUid, string peerName)
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null) return;

        if (!IsOpen) Open();
        string error = chat.OpenConversation(peerUid, peerName, failed =>
        {
            if (this != null) ShowNotice(failed);
        });
        if (error != null)
        {
            ShowNotice(error);
            return;
        }

        SetChannel(peerUid, peerName);
    }

    private void SelectChannel(Tab tab)
    {
        if (tab.Peer == currentPeer) return;
        SetChannel(tab.Peer, tab.Name);
    }

    private void SetChannel(string peer, string name)
    {
        currentPeer     = peer;
        currentPeerName = name;
        noticeText.gameObject.SetActive(false);
        CloseUserMenu();
        Redraw(true);
    }

    /// <summary>[나가기] — 누르면 바로 나감.</summary>
    private void OnLeaveClicked()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null || currentPeer == null) return;

        string name = currentPeerName;
        chat.LeaveConversation(currentPeer, error =>
        {
            if (this == null) return;
            ShowNotice(error ?? $"{ChatText.Plain(name)} 님과의 대화방에서 나갔습니다.");
        });
        SetChannel(null, null);
    }

    /// <summary>탭 목록·제목·나가기 버튼을 지금 상태에 맞춥니다. 보던 대화가 없어졌으면 전체 채팅으로 돌아갑니다.</summary>
    private void SyncTabs(ChatManager chat)
    {
        if (chat == null || (currentPeer != null && !chat.HasConversation(currentPeer)))
        {
            currentPeer     = null;
            currentPeerName = null;
        }

        if (HasTabs) SyncTabList(chat);

        if (titleText != null)
            titleText.text = currentPeer == null ? PublicTabName : $"{ChatText.Plain(currentPeerName)} 님과 개인 채팅";

        bool showLeave = currentPeer != null;
        if (leaveButton != null && leaveButton.gameObject.activeSelf != showLeave)
            leaveButton.gameObject.SetActive(showLeave);
    }

    private void SyncTabList(ChatManager chat)
    {
        int count = 0;
        SetTab(count++, null, PublicTabName, PublicTabName);

        if (chat != null)
        {
            foreach (ChatManager.Conversation c in chat.Conversations)
            {
                if (c.PeerUid == currentPeer) currentPeerName = c.PeerName;   // 상대가 이름을 바꿨으면 새 이름으로

                int unread = c.PeerUid == currentPeer ? 0 : chat.UnreadCount(c.PeerUid);
                string label = ChatText.Plain(c.PeerName);
                if (c.PeerLeft) label += " " + ChatText.Colored(ChatText.LeftHex, "(나감)");
                if (unread > 0) label += " " + ChatText.Colored(ChatText.UnreadHex, $"({unread})");
                SetTab(count++, c.PeerUid, c.PeerName, label);
            }
        }

        for (int i = count; i < tabs.Count; i++) tabs[i].Button.gameObject.SetActive(false);
    }

    private void SetTab(int index, string peer, string name, string label)
    {
        while (tabs.Count <= index) tabs.Add(CreateTab());

        Tab tab = tabs[index];
        tab.Peer = peer;
        tab.Name = name;
        tab.Button.gameObject.SetActive(true);

        if (tab.Label != null && tab.Label.text != label) tab.Label.text = label;   // 같으면 메시 재생성 안 함

        Graphic background = tab.Button.targetGraphic;
        if (background != null) background.color = peer == currentPeer ? TabSelectedColor : TabColor;
    }

    private Tab CreateTab()
    {
        var tab = new Tab { Button = Instantiate(tabTemplate, tabList) };
        tab.Label = tab.Button.GetComponentInChildren<TextMeshProUGUI>(true);
        tab.Button.onClick.AddListener(() => SelectChannel(tab));
        return tab;
    }
}

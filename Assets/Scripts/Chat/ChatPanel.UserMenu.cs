using TMPro;
using UnityEngine;
using UnityEngine.UI;


public partial class ChatPanel
{
    [Header("유저 메뉴 (Tools/UI 굽기/채팅 탭·친구 창 이 채움)")]
    [SerializeField] private GameObject userMenu;
    [SerializeField] private Button userMenuDim;
    [SerializeField] private RectTransform userMenuBox;
    [SerializeField] private TextMeshProUGUI userMenuTitle;
    [SerializeField] private Button menuFriendButton;
    [SerializeField] private TextMeshProUGUI menuFriendLabel;
    [SerializeField] private Button menuMessageButton;
    [SerializeField] private Button menuBlockButton;

    [Tooltip("누른 자리와 메뉴 상자 사이 간격 (1920x1080 기준)")]
    [SerializeField] private float menuGap = 16f;

    // 메뉴 모양은 하이어라키(…/UserMenu/Box)에서, 코드는 위치만 (관계에 따라 바뀌는 '친구' 버튼 글자는 인스펙터 texts.menu*)

    private string menuUid;
    private string menuName;

    private bool HasUserMenu => userMenu != null && userMenuBox != null;

    private void AwakeUserMenu()
    {
        if (!HasUserMenu) return;

        if (userMenuDim       != null) userMenuDim.onClick.AddListener(CloseUserMenu);
        if (menuFriendButton  != null) menuFriendButton.onClick.AddListener(OnMenuFriend);
        if (menuMessageButton != null) menuMessageButton.onClick.AddListener(OnMenuMessage);
        if (menuBlockButton   != null) menuBlockButton.onClick.AddListener(OnMenuBlock);
        userMenu.SetActive(false);
    }

    private void OnLineTapped(ChatLinePress press, Vector2 screenPosition)
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null || !HasUserMenu) return;

        ChatMessage m = press.Message;
        if (!m.HasPlayerUid || m.Uid == chat.MyUid) return;   // 내 메시지 · 공지 · uid 를 모르는 메시지

        menuUid  = m.Uid;
        menuName = m.Nickname;
        userMenu.SetActive(true);
        userMenu.transform.SetAsLastSibling();   // 창·친구 창보다 위
        PlaceMenu(screenPosition);
        RefreshUserMenu();
    }

    private void CloseUserMenu()
    {
        if (userMenu != null) userMenu.SetActive(false);
        menuUid = menuName = null;
    }

    /// <summary>메뉴 상자를 누른 자리 오른쪽 아래에(화면 밖이면 반대쪽) — 하이어라키의 크기·pivot·scale 은 유지하고 anchor 만 모음.</summary>
    private void PlaceMenu(Vector2 screenPosition)
    {
        var area = (RectTransform)userMenuBox.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, EventCamera(area), out Vector2 local)) return;

        Rect    r     = area.rect;
        Vector2 rect  = userMenuBox.rect.size;                                 // 상자 자체 크기
        Vector2 size  = Vector2.Scale(rect, (Vector2)userMenuBox.localScale);  // 부모 기준으로 보이는 크기
        Vector2 pivot = userMenuBox.pivot;

        // 왼쪽 위 꼭짓점 기준 — 오른쪽·아래로 넘치면 손가락 반대쪽으로 뒤집음
        float x = local.x + menuGap;
        float y = local.y - menuGap;
        if (x + size.x > r.xMax) x = local.x - menuGap - size.x;
        if (y - size.y < r.yMin) y = local.y + menuGap + size.y;
        x = Mathf.Clamp(x, r.xMin, r.xMax - size.x);
        y = Mathf.Clamp(y, r.yMin + size.y, r.yMax);

        // anchor 를 늘려 두었어도(Stretch) 크기가 바뀌지 않게 — 가운데 anchor 에서는 sizeDelta 가 곧 크기
        userMenuBox.anchorMin = userMenuBox.anchorMax = new Vector2(0.5f, 0.5f);
        userMenuBox.sizeDelta = rect;

        // 왼쪽 위 꼭짓점 → pivot 자리. area 의 pivot 이 가운데가 아니어도 맞게 (anchoredPosition 은 area 가운데 기준)
        Vector2 pivotPos = new Vector2(x + size.x * pivot.x, y - size.y * (1f - pivot.y));
        userMenuBox.anchoredPosition = pivotPos - r.center;
    }

    /// <summary>메뉴 글자를 지금 관계에 맞춤 (열려 있는 동안 목록이 바뀌어도 따라감).</summary>
    private void RefreshUserMenu()
    {
        if (!HasUserMenu || !userMenu.activeSelf) return;

        ChatManager chat = ChatManager.Instance;
        if (chat == null || menuUid == null) { CloseUserMenu(); return; }

        if (userMenuTitle != null) userMenuTitle.text = ChatText.Plain(menuName);

        bool blocked = chat.IsBlocked(menuUid);

        if (menuFriendButton != null)
        {
            FriendButtonState(chat, blocked, out string label, out bool interactable);
            if (menuFriendLabel != null) menuFriendLabel.text = label;
            menuFriendButton.interactable = interactable;
        }

        if (menuMessageButton != null) menuMessageButton.interactable = !blocked;
    }

    /// <summary>친구 버튼 글자·활성 — 관계가 없으면 '친구 추가', 받은 요청이면 '수락', 그 외엔 누를 수 없음.</summary>
    private void FriendButtonState(ChatManager chat, bool blocked, out string label, out bool interactable)
    {
        label        = texts.menuAddFriend;
        interactable = !blocked;
        if (!chat.TryGetFriendship(menuUid, out Friendship f)) return;

        switch (f.State)
        {
            case FriendState.Friend:   label = texts.menuAlreadyFriend; interactable = false; break;
            case FriendState.Outgoing: label = texts.menuRequestSent;   interactable = false; break;
            case FriendState.Incoming: label = texts.menuAccept;        break;
        }
    }

    private void OnMenuFriend()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null || menuUid == null) return;

        bool accepting = chat.TryGetFriendship(menuUid, out Friendship f) && f.State == FriendState.Incoming;
        string name = ChatText.Plain(menuName);
        string done = ChatText.Fill(accepting ? texts.noticeFriendAccepted : texts.noticeFriendRequested, "name", name);

        chat.RequestFriend(menuUid, menuName, error =>
        {
            if (this != null) ShowNotice(error ?? done);
        });
        CloseUserMenu();
    }

    private void OnMenuMessage()
    {
        string uid = menuUid, name = menuName;
        CloseUserMenu();
        if (uid != null) OpenDirect(uid, name);
    }

    /// <summary>차단 — 메시지가 바로 사라짐 (메시지가 안 보여 이 메뉴를 다시 못 여니 해제는 친구 창 차단 목록에서).</summary>
    private void OnMenuBlock()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null || menuUid == null) return;

        string done = ChatText.Fill(texts.noticeBlocked, "name", ChatText.Plain(menuName));
        chat.Block(menuUid, menuName, error =>
        {
            if (this != null) ShowNotice(error ?? done);
        });
        CloseUserMenu();
    }
}

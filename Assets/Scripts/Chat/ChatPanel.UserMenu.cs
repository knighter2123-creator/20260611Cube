using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 다른 유저의 채팅 줄을 한 번 누르면 뜨는 메뉴 — 친구 추가 / 개인 메시지 / 채팅 차단.
/// 누른 자리 옆에 뜨고, 바깥(Dim)을 누르면 닫힙니다. 내 메시지는 눌러도 뜨지 않습니다.
/// 채팅 차단을 누르면 그 사람의 메시지가 바로 사라집니다 (내 화면에서만 — 다른 사람에게는 그대로 보임).
///
/// [씬 구성]  Tools/UI 굽기/채팅 탭·친구 창 이 Panel 아래에 만듭니다.
///   UserMenu                 ← userMenu (꺼 둠)
///     Dim (Button)           ← userMenuDim. 투명에 가까운 전체 화면 — 누르면 닫힘
///     Box                    ← userMenuBox. 누른 자리로 옮겨짐
///       Title / Friend / Message / Block (Button + TMP)
/// </summary>
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

    private const float MenuGap = 16f;   // 누른 자리와 메뉴 상자 사이

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
        if (string.IsNullOrEmpty(m.Uid) || m.Uid == chat.MyUid) return;   // 내 메시지

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

    /// <summary>메뉴 상자를 누른 자리 오른쪽 아래에 둡니다 (화면 밖으로 나가면 반대쪽으로).</summary>
    private void PlaceMenu(Vector2 screenPosition)
    {
        var area = (RectTransform)userMenuBox.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, EventCamera(area), out Vector2 local)) return;

        Rect    r    = area.rect;
        Vector2 size = userMenuBox.rect.size;

        // 왼쪽 위 꼭짓점 기준 — 오른쪽·아래로 넘치면 손가락 반대쪽으로 뒤집음
        float x = local.x + MenuGap;
        float y = local.y - MenuGap;
        if (x + size.x > r.xMax) x = local.x - MenuGap - size.x;
        if (y - size.y < r.yMin) y = local.y + MenuGap + size.y;
        x = Mathf.Clamp(x, r.xMin, r.xMax - size.x);
        y = Mathf.Clamp(y, r.yMin + size.y, r.yMax);

        userMenuBox.anchorMin = userMenuBox.anchorMax = new Vector2(0.5f, 0.5f);
        userMenuBox.pivot     = new Vector2(0f, 1f);
        // area 의 pivot 이 가운데가 아니어도 맞게 — local 은 area pivot 기준, anchoredPosition 은 area 가운데 기준
        userMenuBox.anchoredPosition = new Vector2(x, y) - r.center;
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
        label        = "친구 추가";
        interactable = !blocked;
        if (!chat.TryGetFriendship(menuUid, out Friendship f)) return;

        switch (f.State)
        {
            case FriendState.Friend:   label = "이미 친구입니다"; interactable = false; break;
            case FriendState.Outgoing: label = "친구 요청 보냄";  interactable = false; break;
            case FriendState.Incoming: label = "친구 요청 수락";  break;
        }
    }

    private void OnMenuFriend()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null || menuUid == null) return;

        bool accepting = chat.TryGetFriendship(menuUid, out Friendship f) && f.State == FriendState.Incoming;
        string name = ChatText.Plain(menuName);
        string done = accepting ? $"{name} 님과 친구가 되었습니다." : $"{name} 님에게 친구 요청을 보냈습니다.";

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

    /// <summary>
    /// 차단 — 그 사람의 메시지가 바로 사라집니다. 해제는 친구 창의 차단 목록에서만 합니다
    /// (차단한 사람의 메시지는 채팅에 보이지 않으므로 이 메뉴를 다시 열 수 없음).
    /// </summary>
    private void OnMenuBlock()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null || menuUid == null) return;

        string done = $"{ChatText.Plain(menuName)} 님의 채팅을 차단했습니다. 친구 창의 차단 목록에서 해제할 수 있습니다.";
        chat.Block(menuUid, menuName, error =>
        {
            if (this != null) ShowNotice(error ?? done);
        });
        CloseUserMenu();
    }
}

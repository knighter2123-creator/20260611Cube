using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ChatPanel : MonoBehaviour
{
    [Header("창")]
    [Tooltip("Dim + Window 를 묶은 오브젝트. 열고 닫을 때 켜고 끕니다.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Button dimButton;
    [SerializeField] private Button closeButton;
    [Tooltip("content 는 Log(줄들을 담는 VerticalLayoutGroup) 여야 합니다.")]
    [SerializeField] private ScrollRect scroll;
    [Tooltip("메시지 한 줄의 모양. 꺼 둔 채로 Log 아래에 둡니다.")]
    [SerializeField] private TextMeshProUGUI logLineTemplate;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI noticeText;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button sendButton;

    [Tooltip("보내기 실패 문구를 보여 주는 시간(초)")]
    [SerializeField] private float noticeDuration = 3f;

    [Header("줄 풀")]
    [Tooltip("미리 만들어 둘 줄 수. ChatManager 의 historyCount 와 같게 두면 창을 열 때 새로 만들 일이 없습니다.")]
    [SerializeField] private int prewarmLines = 50;

    [Tooltip("한 프레임에 미리 만들 줄 수 (시작 직후 프레임 드랍 방지)")]
    [SerializeField] private int prewarmPerFrame = 3;

    [Header("미리보기 (창을 닫았을 때 한 줄)")]
    [Tooltip("미리보기 줄. 별도 캔버스에 둡니다 — panel 안에 두면 창을 닫을 때 함께 꺼짐.")]
    [SerializeField] private RectTransform previewBar;
    [SerializeField] private TextMeshProUGUI previewText;

    [Tooltip("OpenChatButton 오른쪽에 붙을 때 버튼과의 간격 (1920x1080 기준)")]
    [SerializeField] private float previewGap = 16f;

    /// <summary>실행 중 넣는 글자({name}{max}{count} 는 값으로 치환) — 모양은 하이어라키(ChatCanvas 프리팹)에서 고침.</summary>
    [Serializable]
    private class Texts
    {
        [Header("입력창 안내")]
        public string placeholder       = "메시지 입력";
        [Tooltip("{max} = 개인 메시지 최대 글자 수")]
        public string directPlaceholder = "메시지 입력 (최대 {max}자)";

        [Header("빈 채널")]
        public string emptyPublic = "아직 메시지가 없습니다. 첫 인사를 남겨 보세요!";
        [Tooltip("{name} = 상대 닉네임")]
        public string emptyDirect = "{name} 님에게 첫 메시지를 보내 보세요.";
        public string emptyGuild  = "아직 길드 메시지가 없습니다. 길드원에게 인사를 남겨 보세요!";

        [Header("미리보기 (창을 닫았을 때 한 줄)")]
        public string previewLabel  = "채팅";
        [Tooltip("{count} = 안 읽은 개인 메시지 수")]
        public string previewUnread = "개인 메시지 {count}";
        [Tooltip("{count} = 안 읽은 길드 메시지 수")]
        public string previewGuildUnread = "길드 {count}";
        public string previewEmpty  = "눌러서 대화에 참여해 보세요!";

        [Header("채널 탭 · 창 제목")]
        public string publicTabName = "전체 채팅";
        [Tooltip("길드에 들어가 있을 때만 보이는 탭")]
        public string guildTabName  = "길드 채팅";
        [Tooltip("{name} = 길드 이름")]
        public string guildTitle    = "{name} 길드 채팅";
        [Tooltip("{name} = 상대 닉네임")]
        public string directTitle   = "{name} 님과 개인 채팅";
        [Tooltip("탭 이름 뒤 안 읽은 수. {count} = 개수")]
        public string tabUnread     = "({count})";

        [Header("유저 메뉴 — 친구 버튼")]
        public string menuAddFriend     = "친구 추가";
        public string menuAlreadyFriend = "이미 친구입니다";
        public string menuRequestSent   = "친구 요청 보냄";
        public string menuAccept        = "친구 요청 수락";

        [Header("알림 (창 아래 잠깐 뜨는 문구) — {name} = 상대 닉네임")]
        public string noticeFriendRequested = "{name} 님에게 친구 요청을 보냈습니다.";
        [Tooltip("상대가 먼저 요청해 둔 상태에서 [친구 요청 수락] 을 눌렀을 때")]
        public string noticeFriendAccepted  = "{name} 님과 친구가 되었습니다.";
        public string noticeBlocked         = "{name} 님의 채팅을 차단했습니다. 친구 창의 차단 목록에서 해제할 수 있습니다.";
        [Tooltip("개인 채팅 [나가기] 를 눌렀을 때")]
        public string noticeLeft            = "{name} 님과의 대화방에서 나갔습니다.";
    }

    /// <summary>메시지 줄·미리보기·탭 글자 색 (TMP 리치 텍스트로 칠함 — 템플릿 TMP 의 색 위에 덮어씀).</summary>
    [Serializable]
    private class Colors
    {
        [Tooltip("[2-3] 진행도 · 미리보기의 '채팅'")]
        public Color progress = ChatText.ProgressColor;
        public Color myName   = ChatText.MyNameColor;
        public Color name     = ChatText.NameColor;
        public Color body     = ChatText.BodyColor;
        [Tooltip("안 읽은 수")]
        public Color unread   = ChatText.UnreadColor;

        [Tooltip("지금 보고 있는 탭의 배경색. 안 고른 탭은 Tab 템플릿(하이어라키)의 색 그대로입니다.")]
        public Color tabSelected = TabSelectedColor;
    }

    [Header("글자 · 색 (실행 중에 코드가 넣는 값)")]
    [SerializeField] private Texts  texts  = new Texts();
    [SerializeField] private Colors colors = new Colors();

    private TMP_Text placeholderText;

    private readonly StringBuilder sb = new StringBuilder();
    private float noticeUntil;

    private ChatManager   subscribed;      // Changed 를 구독 중인 매니저
    private RectTransform previewFollow;   // OpenChatButton — 있으면 그 오른쪽에 붙음
    private float         previewWidth;    // 씬에서 정한 폭 (오른쪽 끝에 닿으면 이보다 줄어듦)
    private readonly Vector3[] corners = new Vector3[4];

    // 줄 풀 (클래스 주석 [줄 풀링] 참고)
    private readonly Dictionary<string, TextMeshProUGUI> rowsById = new Dictionary<string, TextMeshProUGUI>();
    private readonly Stack<TextMeshProUGUI> freeRows = new Stack<TextMeshProUGUI>();
    private readonly HashSet<string> liveIds  = new HashSet<string>();
    private readonly List<string>    staleIds = new List<string>();
    private int createdRows;

    public bool IsOpen => panel != null && panel.activeSelf;

    private void Awake()
    {
        dimButton.onClick.AddListener(Close);
        closeButton.onClick.AddListener(Close);
        sendButton.onClick.AddListener(Submit);

        // 미리보기는 터치를 받지 않음 — 씬에서 Raycast Target 을 켜 두어도 아래 버튼이 눌리게 코드에서 끔
        foreach (Graphic g in previewBar.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        // 글자 수 제한 — 전체 채팅은 0(제한 없음, 뒤끝챗 콘솔이 막음), 개인 메시지는 ChatManager 상수 (SetInputState 에서 탭마다 바꿈)
        input.characterLimit = 0;
        input.onSubmit.AddListener(_ => Submit());   // PC 엔터 / 모바일 키보드 완료
        placeholderText = input.placeholder as TMP_Text;
        if (placeholderText != null) placeholderText.text = texts.placeholder;

        previewWidth = previewBar.sizeDelta.x;
        logLineTemplate.gameObject.SetActive(false);
        logLineTemplate.raycastTarget = true;   // 누르기 (ChatLinePress) — 복제본도 같은 값

        AwakeTabs();
        AwakeUserMenu();

        panel.SetActive(false);
        noticeText.gameObject.SetActive(false);
        UpdatePreview();   // 매니저가 아직 없을 때도 빈 줄로 두지 않게
    }

    private IEnumerator Start()
    {
        // 첫 프레임(Awake/Start 집중)은 건너뛰고 조금씩 줄을 미리 만듦 (panel 이 꺼져 있어도 꺼진 채 풀로)
        int perFrame = Mathf.Max(1, prewarmPerFrame);
        while (createdRows < prewarmLines)
        {
            yield return null;
            for (int i = 0; i < perFrame && createdRows < prewarmLines; i++)
                freeRows.Push(CreateRow());
        }
    }

    private void OnDestroy()
    {
        if (subscribed != null) subscribed.Changed -= OnChanged;
        // 창을 연 채 씬이 바뀌면 ChatManager(씬 밖)가 계속 그 방을 짧은 주기로 받거나 길드 메시지를 읽음 처리하지 않게
        StopWatching();   // (IsOpen 은 쓰지 않음 — 씬을 내릴 때 panel 이 먼저 파괴돼 있을 수 있음)
    }

    private void Update()
    {
        // 매니저가 나보다 늦게 생기거나(에디터에서 이 씬부터 실행) 다시 만들어져도 따라가게 매 프레임 확인
        if (subscribed != ChatManager.Instance)
        {
            if (subscribed != null) subscribed.Changed -= OnChanged;
            subscribed = ChatManager.Instance;
            if (subscribed != null) subscribed.Changed += OnChanged;
            OnChanged();
        }

        if (IsOpen && noticeText.gameObject.activeSelf && Time.unscaledTime >= noticeUntil)
            noticeText.gameObject.SetActive(false);
    }

    /// <summary>미리보기를 이 UI(보통 OpenChatButton) 오른쪽에 붙입니다.</summary>
    public void AttachPreviewTo(RectTransform target) => previewFollow = target;

    /// <summary>미리보기 표시·위치(버튼 오른쪽) — 버튼이 다른 캔버스라 화면 좌표를 거쳐 변환, 레이아웃 뒤라 LateUpdate.</summary>
    private void LateUpdate()
    {
        bool visible = !IsOpen && (previewFollow == null || previewFollow.gameObject.activeInHierarchy);
        if (previewBar.gameObject.activeSelf != visible) previewBar.gameObject.SetActive(visible);
        if (!visible || previewFollow == null) return;

        Camera cam = EventCamera(previewFollow);

        previewFollow.GetWorldCorners(corners);   // 0 왼쪽 아래, 1 왼쪽 위, 2 오른쪽 위, 3 오른쪽 아래
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, (corners[2] + corners[3]) * 0.5f);   // 오른쪽 변 가운데

        var canvasRt = (RectTransform)previewBar.parent;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, screen, null, out Vector2 local)) return;

        previewBar.anchorMin = previewBar.anchorMax = new Vector2(0.5f, 0.5f);
        previewBar.pivot            = new Vector2(0f, 0.5f);
        previewBar.anchoredPosition = local + new Vector2(previewGap, 0f);

        // 화면 오른쪽 밖으로 나가지 않게 폭을 줄임
        float room = canvasRt.rect.width * 0.5f - previewBar.anchoredPosition.x - previewGap;
        previewBar.sizeDelta = new Vector2(Mathf.Clamp(room, 0f, previewWidth), previewBar.sizeDelta.y);
    }

    /// <summary>이 UI 가 속한 캔버스의 화면 변환 카메라 (Overlay 면 null).</summary>
    private static Camera EventCamera(Component ui)
    {
        Canvas canvas = ui.GetComponentInParent<Canvas>();
        return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // ───────────────────────────── 열기 / 닫기 ─────────────────────────────

    public void Open()
    {
        panel.SetActive(true);
        previewBar.gameObject.SetActive(false);
        noticeText.gameObject.SetActive(false);

        if (ChatManager.Instance == null)
            Debug.LogError("[ChatPanel] ChatManager 가 없습니다. LoginScene 의 ManagerRoot 하위에 두었는지 확인하세요.");

        Redraw(true);
    }

    /// <summary>창만 닫습니다 — 개인 대화방은 나가지 않으므로 탭과 메시지는 그대로 남습니다 (메시지 받기만 멈춤, 안 읽은 수는 계속 셈).</summary>
    public void Close()
    {
        CloseUserMenu();
        panel.SetActive(false);   // 미리보기는 LateUpdate 가 다시 켬
        StopWatching();
        UpdatePreview();
    }

    /// <summary>보던 개인 대화방·길드 탭을 놓음 — 창을 닫거나 씬이 바뀌어 이 창이 사라질 때.</summary>
    private void StopWatching()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null) return;
        chat.WatchConversation(null);
        chat.WatchGuild(false);
    }

    private void OnChanged()
    {
        UpdatePreview();
        Redraw(false);
        RefreshUserMenu();
    }

    // ───────────────────────────── 보내기 ─────────────────────────────

    private void Submit()
    {
        ChatManager chat = ChatManager.Instance;
        if (chat == null) return;

        string text = input.text;
        if (string.IsNullOrWhiteSpace(text)) return;

        bool rejected = false;
        bool cleared  = false;
        chat.Send(text, error =>
        {
            if (this == null || error == null) return;
            rejected = true;
            ShowNotice(error);

            // 서버가 나중에 거절하면(도배·길이·금칙어) 비운 글을 되돌림 — 그사이 새 글을 쓰기 시작했으면 덮지 않음
            if (cleared && input.text.Length == 0) input.text = text;
        }, currentPeer, currentGuild);

        // 클라이언트 검사에 걸리면(콜백 즉시) 글을 남기고, 아니면 응답을 기다리지 않고 비움 (연달아 입력 가능)
        if (!rejected)
        {
            input.text = string.Empty;
            cleared    = true;
        }

        Redraw(true);   // 내가 보낸 직후에는 맨 아래로
        input.ActivateInputField();   // 연달아 입력할 수 있게 포커스 유지
    }

    private void ShowNotice(string message)
    {
        noticeText.text = message;
        noticeText.gameObject.SetActive(true);
        noticeUntil = Time.unscaledTime + noticeDuration;
    }

    // ───────────────────────────── 그리기 ─────────────────────────────

    private void Redraw(bool scrollToBottom)
    {
        if (!IsOpen) return;

        ChatManager chat = ChatManager.Instance;

        SyncTabs(chat);   // 보던 대화가 사라졌으면(나가기·차단·계정 전환) 여기서 전체 채팅으로 돌아감
        bool direct = currentPeer != null;
        if (chat != null)
        {
            chat.WatchConversation(currentPeer);   // 개인 탭이면 그 방의 메시지를 받기 시작
            chat.WatchGuild(currentGuild);         // 길드 탭이면 보는 동안 온 메시지는 읽음
            if (direct) chat.MarkRead(currentPeer);
        }

        string reason = chat == null  ? ChatText.NoManager
                      : direct        ? chat.DirectUnavailableReason
                      : currentGuild  ? chat.GuildUnavailableReason
                                      : chat.UnavailableReason;

        // 맨 아래를 보고 있었으면 새 메시지가 와도 맨 아래를 유지, 위로 올려 읽는 중이면 그대로 둠
        bool wasAtBottom = scroll.verticalNormalizedPosition <= 0.01f;

        int shown = SyncRows(chat);
        ShowStatus(StatusFor(reason, shown, direct));
        SetInputState(usable: reason == null, direct);

        if (scrollToBottom || wasAtBottom)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>목록 위 안내 문구 — 쓸 수 없는 이유, 또는 빈 채널의 첫 인사 안내. 없으면 빈 문자열.</summary>
    private string StatusFor(string reason, int shown, bool direct)
    {
        if (reason != null) return reason;
        if (shown > 0)      return string.Empty;
        return direct       ? ChatText.Fill(texts.emptyDirect, "name", ChatText.Plain(currentPeerName))
             : currentGuild ? texts.emptyGuild
                            : texts.emptyPublic;
    }

    private void SetInputState(bool usable, bool direct)
    {
        input.interactable      = usable;
        sendButton.interactable = usable;

        // 개인 메시지 탭만 클라이언트 글자 수 제한 (전체 채팅은 뒤끝챗 콘솔 설정)
        int limit = direct ? ChatManager.DIRECT_MAX_LENGTH : 0;
        if (input.characterLimit != limit)
        {
            input.characterLimit = limit;
            if (limit > 0 && input.text.Length > limit) input.text = input.text.Substring(0, limit);   // 전체 탭에서 길게 쓰다 넘어온 경우
        }

        if (placeholderText != null)
        {
            string hint = direct ? ChatText.Fill(texts.directPlaceholder, "max", ChatManager.DIRECT_MAX_LENGTH.ToString())
                                 : texts.placeholder;
            if (placeholderText.text != hint) placeholderText.text = hint;
        }
    }

    /// <summary>지금 채널의 메시지와 줄 오브젝트를 맞춤(새 메시지만 글자 채움, 빠진 줄은 풀로, 차단한 사람 제외) — 보이는 줄 수 반환.</summary>
    private int SyncRows(ChatManager chat)
    {
        liveIds.Clear();
        if (chat != null)
        {
            string myUid = chat.MyUid;
            IReadOnlyList<ChatMessage> list = currentPeer != null ? chat.DirectMessagesWith(currentPeer)
                                            : currentGuild        ? chat.GuildMessages
                                                                  : chat.Messages;
            foreach (ChatMessage m in list)
            {
                if (chat.IsBlocked(m.Uid)) continue;

                liveIds.Add(m.Id);
                if (!rowsById.TryGetValue(m.Id, out TextMeshProUGUI row))
                {
                    row = TakeRow();
                    sb.Clear();
                    AppendLine(sb, m, m.Uid == myUid);
                    row.text = sb.ToString();
                    row.GetComponent<ChatLinePress>().Message = m;
                    rowsById[m.Id] = row;
                }
                row.transform.SetAsLastSibling();   // 보낸 시각 순서대로 쌓임 (꺼진 템플릿·풀 줄은 레이아웃에서 빠짐)
            }
        }

        RecycleStaleRows();
        return liveIds.Count;
    }

    /// <summary>이번에 보이지 않는(liveIds 에 없는) 메시지의 줄을 꺼서 풀로 돌립니다.</summary>
    private void RecycleStaleRows()
    {
        staleIds.Clear();
        foreach (string id in rowsById.Keys)
            if (!liveIds.Contains(id)) staleIds.Add(id);

        foreach (string id in staleIds)
        {
            TextMeshProUGUI row = rowsById[id];
            rowsById.Remove(id);
            row.gameObject.SetActive(false);
            freeRows.Push(row);
        }
    }

    private TextMeshProUGUI TakeRow()
    {
        TextMeshProUGUI row = freeRows.Count > 0 ? freeRows.Pop() : CreateRow();
        row.gameObject.SetActive(true);
        return row;
    }

    private TextMeshProUGUI CreateRow()
    {
        TextMeshProUGUI row = Instantiate(logLineTemplate, scroll.content);
        row.gameObject.SetActive(false);

        var press = row.gameObject.GetComponent<ChatLinePress>();
        if (press == null) press = row.gameObject.AddComponent<ChatLinePress>();
        press.Tapped += OnLineTapped;

        createdRows++;
        return row;
    }

    /// <summary>미리보기 한 줄 — 안 읽은 개인 메시지 수 + 마지막 메시지. 없으면 안내 문구.</summary>
    private void UpdatePreview()
    {
        if (IsOpen) return;

        ChatManager chat = ChatManager.Instance;
        sb.Clear();
        if (!string.IsNullOrEmpty(texts.previewLabel)) sb.AppendColored(colors.progress, texts.previewLabel).Append("  ");

        if (chat == null)
        {
            sb.Append(ChatText.NoManager);
        }
        else
        {
            int unread = chat.TotalUnread;
            if (unread > 0) sb.AppendColored(colors.unread, ChatText.Fill(texts.previewUnread, "count", unread.ToString())).Append("  ");
            int guildUnread = chat.GuildUnread;
            if (guildUnread > 0) sb.AppendColored(colors.unread, ChatText.Fill(texts.previewGuildUnread, "count", guildUnread.ToString())).Append("  ");

            if (TryGetLastVisible(chat, out ChatMessage last)) AppendLine(sb, last, last.Uid == chat.MyUid);
            else                                               sb.Append(chat.UnavailableReason ?? texts.previewEmpty);
        }

        previewText.text = sb.ToString();
    }

    /// <summary>차단하지 않은 사람의 마지막 전체 채팅 메시지.</summary>
    private static bool TryGetLastVisible(ChatManager chat, out ChatMessage message)
    {
        IReadOnlyList<ChatMessage> list = chat.Messages;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (chat.IsBlocked(list[i].Uid)) continue;
            message = list[i];
            return true;
        }
        message = default;
        return false;
    }

    /// <summary>[2-3] 닉네임 : 내용</summary>
    private void AppendLine(StringBuilder sb, ChatMessage m, bool mine)
    {
        sb.AppendColored(colors.progress, $"[{StageRecord.Format(m.Progress)}]").Append(' ');
        sb.AppendColored(mine ? colors.myName : colors.name, ChatText.Plain(m.Nickname));
        sb.Append(" : ");
        sb.AppendColored(colors.body, ChatText.Plain(m.Text));
    }

    private void ShowStatus(string message)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 채팅 창 — [진행도] 닉네임 : 내용 목록 + 입력창.
///
/// [붙이는 위치]
///   StageScene 의 ChatCanvas (씬 최상위 캔버스). 창·미리보기 UI 는 씬에 배치된 오브젝트를 아래 칸에 연결해서 씁니다.
///   ChatCanvas 를 씬 최상위에 두는 이유는 RankingPanel.Build 주석 참고 (다른 캔버스 안에 넣으면 sortingOrder·CanvasScaler 가 무시됨).
///   여는 버튼에는 OpenChatButton 을 붙이고 이 컴포넌트(ChatCanvas)를 연결하세요.
///
/// [씬 구성]
///   ChatCanvas (Canvas 500)            ← 이 컴포넌트. 항상 켜 둠
///     Panel                            ← panel. 열고 닫을 때 켜고 끔
///       Dim (Button)                   ← 누르면 닫힘
///       Window
///         Title / Close (Button)
///         Viewport (ScrollRect, RectMask2D)
///           Log (VerticalLayoutGroup, ContentSizeFitter)  ← ScrollRect.content
///             LogLine (꺼 둠)           ← logLineTemplate. 메시지 한 줄의 모양 — 복제해서 씀
///           Status                     ← 연결 중 / 메시지 없음
///         Notice                       ← 보내기 실패 문구
///         Input (TMP_InputField) / Send (Button)
///   ChatPreviewCanvas (Canvas 100)
///     Preview (Image)                  ← previewBar. 창이 닫혀 있을 때 마지막 메시지 한 줄
///       Text
///   버튼 클릭·입력 제출은 Awake 에서 코드로 연결합니다 (씬의 OnClick 칸은 비워 둠).
///
/// [미리보기]  창이 닫혀 있는 동안 마지막 메시지 한 줄을 띄웁니다. 보여 주기만 하고 터치는 아래 UI 로 통과시킵니다
///   (미리보기 캔버스가 위에 있어 오프라인 보상·스탯 강화 버튼을 가리지 않게). 창은 OpenChatButton 으로만 엽니다.
///   OpenChatButton 이 이 패널을 연결하고 있으면 그 버튼 오른쪽에 붙어 따라다닙니다 (버튼이 꺼지면 함께 숨음).
///   버튼이 없으면 씬에 배치한 위치 그대로입니다. 미리보기 폭은 씬에서 정한 폭이 최대입니다.
///
/// [데이터]  ChatManager.Messages 를 그리기만 합니다. ChatManager.Changed 를 받아 미리보기와(열려 있으면) 창을 다시 그립니다.
///
/// [줄 풀링]  메시지 한 줄 = LogLine 복제본 하나. 메시지 Id 별로 줄을 기억해서
///   새 메시지만 글자를 채우고(TMP 메시 재생성), 이미 있는 줄은 순서만 맞춥니다. 밀려난 줄은 꺼서 다시 씁니다.
///   복제본은 처음 몇 초 동안 프레임마다 조금씩 미리 만들어 둡니다 — 창을 처음 열 때 50줄을 한 번에 만들지 않게.
///
/// [나눠 둔 파일]
///   ChatPanel.Tabs.cs     — 왼쪽 채널 탭 (전체 채팅 / 닉네임별 개인 채팅) + 개인 채팅 [나가기]
///   ChatPanel.UserMenu.cs — 다른 유저의 메시지를 누르면 뜨는 메뉴 (친구 추가 / 개인 메시지 / 채팅 차단)
///   탭·메뉴·친구 창 UI 는 StageScene 을 열고 Tools/UI 굽기/채팅 탭·친구 창 메뉴로 만듭니다 (UIBaker).
///   굽지 않았으면(칸이 비어 있으면) 예전처럼 전체 채팅만 보입니다.
/// </summary>
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

    private static string DefaultPlaceholder => $"메시지 입력 (최대 {ChatManager.MAX_LENGTH}자)";
    private const string PeerLeftPlaceholder = "상대방이 대화방을 나갔습니다.";
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

        // 글자 수 제한은 ChatManager 상수를 따름 (씬 값과 어긋나지 않게 코드에서 덮어씀)
        input.characterLimit = ChatManager.MAX_LENGTH;
        input.onSubmit.AddListener(_ => Submit());   // PC 엔터 / 모바일 키보드 완료
        placeholderText = input.placeholder as TMP_Text;
        if (placeholderText != null) placeholderText.text = DefaultPlaceholder;

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
        // 첫 프레임(씬 로드 직후 Awake/Start 가 몰리는 때)은 건너뛰고, 그다음부터 조금씩 만듭니다.
        // (panel 이 꺼져 있어도 Instantiate 는 됩니다 — 줄은 꺼진 채 풀에 들어감)
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

    /// <summary>
    /// 미리보기를 보이거나 숨기고, 붙은 버튼 오른쪽으로 옮깁니다. 버튼은 다른 캔버스(Safe Panel 등)에 있으므로
    /// 버튼의 오른쪽 변 가운데 → 화면 좌표 → 미리보기 캔버스 좌표로 바꿉니다. 레이아웃이 끝난 뒤라 LateUpdate.
    /// </summary>
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
        if (ChatManager.Instance != null) ChatManager.Instance.WatchConversation(null);
        UpdatePreview();
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
        chat.Send(text, error =>
        {
            if (this == null || error == null) return;
            rejected = true;
            ShowNotice(error);
        }, currentPeer);

        // 검사에 걸리면(도배·길이 등) 콜백이 바로 와서 rejected = true → 고쳐 보낼 수 있게 글을 남겨 둠.
        // 그 외에는 서버 응답을 기다리지 않고 비움 — 내 메시지는 리스너가 서버 확인 전에 바로 보여 줌.
        if (!rejected) input.text = string.Empty;

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
            if (direct) chat.MarkRead(currentPeer);
        }

        string reason = chat == null ? ChatText.NoManager
                      : direct       ? chat.DirectUnavailableReason
                                     : chat.UnavailableReason;

        // 상대가 나간 방 — 메시지는 보이지만 더 보낼 수 없음 (입력창 안내 문구로 알림)
        bool peerLeft = direct && reason == null && chat.PeerLeft(currentPeer);

        // 맨 아래를 보고 있었으면 새 메시지가 와도 맨 아래를 유지, 위로 올려 읽는 중이면 그대로 둠
        bool wasAtBottom = scroll.verticalNormalizedPosition <= 0.01f;

        int shown = SyncRows(chat);
        ShowStatus(StatusFor(reason, shown, direct));
        SetInputState(usable: reason == null && !peerLeft, peerLeft);

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
        return direct ? $"{ChatText.Plain(currentPeerName)} 님에게 첫 메시지를 보내 보세요."
                      : "아직 메시지가 없습니다. 첫 인사를 남겨 보세요!";
    }

    private void SetInputState(bool usable, bool peerLeft)
    {
        input.interactable      = usable;
        sendButton.interactable = usable;
        if (peerLeft && input.text.Length > 0) input.text = string.Empty;

        if (placeholderText != null)
        {
            string hint = peerLeft ? PeerLeftPlaceholder : DefaultPlaceholder;
            if (placeholderText.text != hint) placeholderText.text = hint;
        }
    }

    /// <summary>
    /// 지금 채널(전체 / 개인)의 메시지 목록과 줄 오브젝트를 맞춥니다. 새 메시지만 글자를 채우고, 빠진 메시지의 줄은 풀로 돌립니다.
    /// 차단한 사람의 메시지는 뺍니다. 보이는 줄 수를 돌려줍니다.
    /// </summary>
    private int SyncRows(ChatManager chat)
    {
        liveIds.Clear();
        if (chat != null)
        {
            string myUid = chat.MyUid;
            IReadOnlyList<ChatMessage> list = currentPeer != null ? chat.DirectMessagesWith(currentPeer) : chat.Messages;
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
        sb.AppendColored(ChatText.ProgressHex, "채팅").Append("  ");

        if (chat == null)
        {
            sb.Append(ChatText.NoManager);
        }
        else
        {
            int unread = chat.TotalUnread;
            if (unread > 0) sb.AppendColored(ChatText.UnreadHex, $"개인 메시지 {unread}").Append("  ");

            if (TryGetLastVisible(chat, out ChatMessage last)) AppendLine(sb, last, last.Uid == chat.MyUid);
            else                                               sb.Append(chat.UnavailableReason ?? "눌러서 대화에 참여해 보세요!");
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
    private static void AppendLine(StringBuilder sb, ChatMessage m, bool mine)
    {
        sb.AppendColored(ChatText.ProgressHex, $"[{StageRecord.Format(m.Progress)}]").Append(' ');
        sb.AppendColored(mine ? ChatText.MyNameHex : ChatText.NameHex, ChatText.Plain(m.Nickname));
        sb.Append(" : ");
        sb.AppendColored(ChatText.BodyHex, ChatText.Plain(m.Text));
    }

    private void ShowStatus(string message)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}

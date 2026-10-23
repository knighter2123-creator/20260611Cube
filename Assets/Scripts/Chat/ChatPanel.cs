using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
///   OpenChatButton 이 이 패널을 연결하고 있으면 그 버튼 위쪽에 붙어 따라다닙니다 (버튼이 꺼지면 함께 숨음).
///   버튼이 없으면 씬에 배치한 위치 그대로입니다. 미리보기 폭은 씬에서 정한 폭이 최대입니다.
///
/// [데이터]  ChatManager.Messages 를 그리기만 합니다. ChatManager.Changed 를 받아 미리보기와(열려 있으면) 창을 다시 그립니다.
///
/// [줄 풀링]  메시지 한 줄 = LogLine 복제본 하나. 메시지 Id 별로 줄을 기억해서
///   새 메시지만 글자를 채우고(TMP 메시 재생성), 이미 있는 줄은 순서만 맞춥니다. 밀려난 줄은 꺼서 다시 씁니다.
///   복제본은 처음 몇 초 동안 프레임마다 조금씩 미리 만들어 둡니다 — 창을 처음 열 때 50줄을 한 번에 만들지 않게.
/// </summary>
public class ChatPanel : MonoBehaviour
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

    // 줄 색 (TMP 리치 텍스트)
    private const string ProgressHex = "#FFD933";
    private const string MyNameHex   = "#7FD8FF";
    private const string NameHex     = "#FFFFFF";
    private const string TextHex     = "#D8D8D8";

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
        if (input.placeholder is TMP_Text placeholder)
            placeholder.text = $"메시지 입력 (최대 {ChatManager.MAX_LENGTH}자)";

        previewWidth = previewBar.sizeDelta.x;
        logLineTemplate.gameObject.SetActive(false);

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

        Canvas targetCanvas = previewFollow.GetComponentInParent<Canvas>();
        Camera cam = targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : targetCanvas.worldCamera;

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

    public void Close()
    {
        panel.SetActive(false);   // 미리보기는 LateUpdate 가 다시 켬
        UpdatePreview();
    }

    private void OnChanged()
    {
        UpdatePreview();
        Redraw(false);
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
        });

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
        string reason = chat == null ? "채팅 시스템을 찾을 수 없습니다." : chat.UnavailableReason;

        // 맨 아래를 보고 있었으면 새 메시지가 와도 맨 아래를 유지, 위로 올려 읽는 중이면 그대로 둠
        bool wasAtBottom = scroll.verticalNormalizedPosition <= 0.01f;

        SyncRows(chat);

        if (reason != null)                       ShowStatus(reason);
        else if (chat.Messages.Count == 0)        ShowStatus("아직 메시지가 없습니다. 첫 인사를 남겨 보세요!");
        else                                      ShowStatus(string.Empty);

        bool usable = reason == null;
        input.interactable      = usable;
        sendButton.interactable = usable;

        if (scrollToBottom || wasAtBottom)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>메시지 목록과 줄 오브젝트를 맞춥니다. 새 메시지만 글자를 채우고, 빠진 메시지의 줄은 풀로 돌립니다.</summary>
    private void SyncRows(ChatManager chat)
    {
        liveIds.Clear();
        if (chat != null)
        {
            string myUid = chat.MyUid;
            foreach (ChatMessage m in chat.Messages)
            {
                liveIds.Add(m.Id);
                if (!rowsById.TryGetValue(m.Id, out TextMeshProUGUI row))
                {
                    row = TakeRow();
                    sb.Clear();
                    AppendLine(sb, m, m.Uid == myUid);
                    row.text = sb.ToString();
                    rowsById[m.Id] = row;
                }
                row.transform.SetAsLastSibling();   // 보낸 시각 순서대로 쌓임 (꺼진 템플릿·풀 줄은 레이아웃에서 빠짐)
            }
        }

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
        createdRows++;
        return row;
    }

    /// <summary>미리보기 한 줄 — 마지막 메시지. 없으면 안내 문구.</summary>
    private void UpdatePreview()
    {
        if (IsOpen) return;

        ChatManager chat = ChatManager.Instance;
        sb.Clear();
        sb.Append("<color=").Append(ProgressHex).Append(">채팅</color>  ");

        if (chat == null)
            sb.Append("채팅 시스템을 찾을 수 없습니다.");
        else if (chat.Messages.Count > 0)
        {
            ChatMessage last = chat.Messages[chat.Messages.Count - 1];
            AppendLine(sb, last, last.Uid == chat.MyUid);
        }
        else
            sb.Append(chat.UnavailableReason ?? "눌러서 대화에 참여해 보세요!");

        previewText.text = sb.ToString();
    }

    /// <summary>[2-3] 닉네임 : 내용</summary>
    private static void AppendLine(StringBuilder sb, ChatMessage m, bool mine)
    {
        sb.Append("<color=").Append(ProgressHex).Append(">[").Append(StageRecord.Format(m.Progress)).Append("]</color> ");
        sb.Append("<color=").Append(mine ? MyNameHex : NameHex).Append('>').Append(Plain(m.Nickname)).Append("</color>");
        sb.Append(" : <color=").Append(TextHex).Append('>').Append(Plain(m.Text)).Append("</color>");
    }

    private static readonly Regex NoParseTag = new Regex("</?noparse>", RegexOptions.IgnoreCase);

    /// <summary>
    /// 다른 사람이 보낸 글을 리치 텍스트로 해석하지 않게 감쌉니다. (예: "&lt;size=300&gt;" 로 화면을 덮는 장난 방지)
    /// 글 안의 noparse 태그는 지웁니다 — 지운 뒤 다시 태그가 생기는 경우("&lt;nopa&lt;noparse&gt;rse&gt;")까지 반복.
    /// </summary>
    private static string Plain(string s)
    {
        s = s ?? string.Empty;
        string prev;
        do { prev = s; s = NoParseTag.Replace(s, string.Empty); } while (s != prev);
        return "<noparse>" + s + "</noparse>";
    }

    private void ShowStatus(string message)
    {
        statusText.text = message;
        statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}

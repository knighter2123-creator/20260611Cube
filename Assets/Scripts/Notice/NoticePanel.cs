using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>공지사항 창: 목록(줄을 누르면 내용) ↔ 내용 보기 두 화면. 읽기 전용 — 공지는 뒤끝 콘솔에서만 씀.</summary>
// [UI] Assets/Prefabs/UI/NoticeCanvas 프리팹 (StageScene 에 배치) — 모양은 프리팹에서 직접 고침
public class NoticePanel : MonoBehaviour
{
    [Header("UI (NoticeCanvas 프리팹)")]
    [SerializeField] private GameObject      root;
    [SerializeField] private Button          dimButton;
    [SerializeField] private Button          closeButton;

    [Header("목록 화면")]
    [SerializeField] private GameObject      listView;
    [SerializeField] private RectTransform   content;
    [SerializeField] private NoticeRow       rowTemplate;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button          moreButton;

    [Header("내용 화면")]
    [SerializeField] private GameObject      detailView;
    [SerializeField] private TextMeshProUGUI detailTitle;
    [SerializeField] private TextMeshProUGUI detailDate;
    [SerializeField] private TextMeshProUGUI detailBody;
    [SerializeField] private ScrollRect      detailScroll;
    [SerializeField] private Button          backButton;
    [SerializeField] private Button          linkButton;
    [SerializeField] private TextMeshProUGUI linkLabel;

    [Header("굽기 설정 (UIBaker 가 UI 를 만들 때만 씀)")]
    [Tooltip("한글이 들어 있는 TMP 폰트. 비우면 씬에서 가장 많이 쓰는 폰트.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int sortingOrder = 500;

    [Header("글자 (실행 중에 코드가 넣는 값)")]
    [SerializeField] private string loadingText     = "공지사항을 불러오는 중...";
    [SerializeField] private string emptyText       = "등록된 공지사항이 없습니다.";
    [SerializeField] private string failedText      = "공지사항을 불러오지 못했습니다.";
    [SerializeField] private string offlineText     = "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.";
    [SerializeField] private string defaultLinkText = "바로 가기";

    public TMP_FontAsset Font         => font;
    public int           SortingOrder => sortingOrder;

    // UIBaker 가 Row 템플릿을 구울 때만 쓰는 색 — 구운 뒤에는 하이어라키의 Row 템플릿에서 직접 고치세요
    public static readonly Color RowColor     = new Color(1f, 1f, 1f, 0.04f);
    public static readonly Color NewMarkColor = new Color(0.90f, 0.30f, 0.30f, 1f);

    private readonly List<GameObject> rows = new List<GameObject>();
    private bool ready;
    private bool drawing;
    private long seenBeforeOpen;     // 창을 열기 전까지 본 공지 — 이것보다 새 공지에 NEW 표시
    private NoticeItem shown;        // 내용 화면에 띄운 공지 (null = 목록 화면)

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        ready = root != null && listView != null && content != null && rowTemplate != null && statusText != null
             && detailView != null && detailTitle != null && detailBody != null;
        if (!ready)
        {
            Debug.LogError("[NoticePanel] UI 칸이 비어 있습니다. NoticeCanvas 프리팹(Assets/Prefabs/UI)의 NoticePanel 칸이 모두 연결돼 있는지 확인하세요.", this);
            return;
        }

        if (dimButton   != null) dimButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (backButton  != null) backButton.onClick.AddListener(ShowList);
        if (moreButton  != null) moreButton.onClick.AddListener(NoticeManager.LoadMore);
        if (linkButton  != null) linkButton.onClick.AddListener(OpenLink);

        rowTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    private void OnEnable()  => NoticeManager.OnChanged += OnNoticesChanged;
    private void OnDisable() => NoticeManager.OnChanged -= OnNoticesChanged;

    private void OnNoticesChanged()
    {
        if (IsOpen && !drawing) Draw();
    }

    public void Open()
    {
        if (!ready) return;
        seenBeforeOpen = NoticeManager.LastSeenMs;
        root.SetActive(true);
        ShowList();
        NoticeManager.Refresh();   // 콘솔에서 새로 쓴 공지가 있을 수 있으니 열 때마다 첫 페이지부터
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
        shown = null;
    }

    private void ShowList()
    {
        shown = null;
        detailView.SetActive(false);
        listView.SetActive(true);
        Draw();
    }

    private void ShowDetail(NoticeItem notice)
    {
        shown = notice;
        listView.SetActive(false);
        detailView.SetActive(true);

        detailTitle.text = notice.title;
        if (detailDate != null) detailDate.text = notice.DateText;
        detailBody.text = notice.content;
        if (detailScroll != null) detailScroll.verticalNormalizedPosition = 1f;   // 맨 위부터

        if (linkButton != null)
        {
            linkButton.gameObject.SetActive(notice.HasLink);
            if (linkLabel != null) linkLabel.text = notice.linkButtonName ?? defaultLinkText;
        }
    }

    private void OpenLink()
    {
        if (shown != null && shown.HasLink) Application.OpenURL(shown.linkUrl);
    }

    private void Draw()
    {
        drawing = true;
        try
        {
            // 창이 열려 있는 동안 받은 공지는 본 것으로 (버튼의 안 읽은 개수가 사라짐)
            NoticeManager.MarkAllSeen();

            foreach (GameObject row in rows) Destroy(row);
            rows.Clear();

            IReadOnlyList<NoticeItem> notices = NoticeManager.Items;
            foreach (NoticeItem notice in notices)
            {
                NoticeItem n = notice;
                NoticeRow row = Instantiate(rowTemplate, content);
                row.gameObject.SetActive(true);
                row.Set(n, n.postedAt > seenBeforeOpen, () => ShowDetail(n));
                rows.Add(row.gameObject);
            }

            bool empty = notices.Count == 0;
            string status = NoticeManager.IsLoading && empty            ? loadingText
                          : NoticeManager.LastFailed && empty           ? (NoticeManager.LastFailedOffline ? offlineText : failedText)
                          : !NoticeManager.CanFetch && empty            ? offlineText
                          : empty && NoticeManager.HasLoaded            ? emptyText
                          : empty                                       ? loadingText
                          : string.Empty;
            statusText.text = status;
            statusText.gameObject.SetActive(status.Length > 0);

            if (moreButton != null)
            {
                moreButton.gameObject.SetActive(NoticeManager.HasMore);
                moreButton.interactable = !NoticeManager.IsLoading;
            }
        }
        finally { drawing = false; }
    }
}

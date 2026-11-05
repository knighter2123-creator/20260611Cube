using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 우편함 창 — 받은 우편 목록 + 줄마다 [받기] + [모두 받기].
///
/// [붙이는 위치 / UI]
///   StageScene 을 열고 Tools/UI 굽기/우편함 메뉴를 누르세요 (UIBaker).
///   MailboxPanel 오브젝트, MailboxCanvas, 랭킹(친구) 버튼 옆 우편함 버튼을 만들고 칸을 모두 채웁니다. 그 뒤 씬을 저장하면 됩니다.
///
///   MailboxCanvas (Canvas 500)
///     Dim (Button)                  ← dimButton. 누르면 닫힘
///     Window
///       Title / Close (Button)      ← closeButton
///       Viewport (ScrollRect)
///         Content (VerticalLayoutGroup)   ← content
///           Row (MailRow, 꺼 둠)            ← rowTemplate. 복제해서 씀
///         Status                    ← statusText
///       ClaimAll (Button)           ← claimAllButton
///
/// [데이터]  Mailbox 를 읽어 그리기만 합니다. 우편이 오거나 받으면 Mailbox.OnChanged 로 다시 그립니다.
/// </summary>
public class MailboxPanel : MonoBehaviour
{
    [Header("UI (Tools/UI 굽기/우편함 이 채움)")]
    [SerializeField] private GameObject      root;
    [SerializeField] private Button          dimButton;
    [SerializeField] private Button          closeButton;
    [SerializeField] private RectTransform   content;
    [SerializeField] private MailRow         rowTemplate;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button          claimAllButton;

    [Header("굽기 설정 (UIBaker 가 UI 를 만들 때만 씀)")]
    [Tooltip("한글이 들어 있는 TMP 폰트. 비우면 씬에서 가장 많이 쓰는 폰트.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int sortingOrder = 500;

    [Header("글자 (실행 중에 코드가 넣는 값)")]
    [SerializeField] private string loadingText = "우편을 불러오는 중...";
    [SerializeField] private string emptyText   = "받은 우편이 없습니다.";

    public TMP_FontAsset Font         => font;
    public int           SortingOrder => sortingOrder;

    // UIBaker 가 Row 템플릿을 구울 때만 쓰는 색 — 구운 뒤에는 하이어라키의 Row 템플릿에서 직접 고치세요
    public static readonly Color RowColor    = new Color(1f, 1f, 1f, 0.04f);
    public static readonly Color RewardColor = new Color(1f, 0.85f, 0.2f);

    private readonly List<GameObject> rows = new List<GameObject>();
    private bool ready;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        ready = root != null && content != null && rowTemplate != null && statusText != null;
        if (!ready)
        {
            Debug.LogError("[MailboxPanel] UI 칸이 비어 있습니다. StageScene 을 열고 Tools/UI 굽기/우편함 메뉴로 만든 뒤 씬을 저장하세요.", this);
            return;
        }

        if (dimButton      != null) dimButton.onClick.AddListener(Close);
        if (closeButton    != null) closeButton.onClick.AddListener(Close);
        if (claimAllButton != null) claimAllButton.onClick.AddListener(() => Mailbox.ClaimAll());

        rowTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    private void OnEnable()  => Mailbox.OnChanged += OnMailboxChanged;
    private void OnDisable() => Mailbox.OnChanged -= OnMailboxChanged;

    private void OnMailboxChanged()
    {
        if (IsOpen) Draw();
    }

    public void Open()
    {
        if (!ready) return;
        root.SetActive(true);
        Draw();
        Mailbox.Refresh();   // 서버 우편을 새로 받아 옴 — 도착하면 OnChanged 로 다시 그림
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void Draw()
    {
        foreach (GameObject row in rows) Destroy(row);
        rows.Clear();

        bool busy = Mailbox.IsClaiming;
        List<MailItem> mails = Mailbox.NewestFirst();
        foreach (MailItem mail in mails)
        {
            string id = mail.id;
            MailRow row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Set(mail, () => Mailbox.TryClaim(id), canClaim: !busy);
            rows.Add(row.gameObject);
        }

        // 받기 실패 사유가 있으면 그것을, 없으면 빈 우편함 안내
        bool empty = mails.Count == 0;
        string status = !string.IsNullOrEmpty(Mailbox.LastError) ? Mailbox.LastError
                      : empty ? (Mailbox.IsRefreshing ? loadingText : emptyText)
                      : string.Empty;
        statusText.text = status;
        statusText.gameObject.SetActive(status.Length > 0);
        if (claimAllButton != null) claimAllButton.interactable = Mailbox.Count > 0 && !busy;   // 받은 우편만 남았으면 막음
    }
}

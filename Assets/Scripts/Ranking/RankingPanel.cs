using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 랭킹 창 — "순위 / 닉네임 / 스테이지" 목록 + 맨 아래 내 순위.
///
/// [붙이는 위치]
///   StageScene 의 항상 켜진 빈 오브젝트. 여는 버튼에는 OpenRankingButton 을 붙이고 이 컴포넌트를 연결하세요.
///
/// [UI]  씬에 있는 오브젝트를 아래 칸에 연결해서 씁니다.
///   처음 만들거나 모양을 다시 만들 때는 StageScene 을 열고 Tools/UI 굽기/랭킹 창 메뉴를 누르세요 (UIBaker) —
///   RankingCanvas 를 씬 최상위에 만들고 아래 칸을 모두 채웁니다. 그 뒤 씬을 저장하면 됩니다.
///   RankingCanvas 를 씬 최상위에 두는 이유: 다른 캔버스(예: Safe Panel) 안에 넣으면 '중첩 캔버스' 가 되어
///   sortingOrder·CanvasScaler 가 무시되고, 어두운 배경도 Safe Area 만큼만 덮습니다.
///
///   RankingCanvas (Canvas 500)
///     Dim (Button)               ← dimButton. 누르면 닫힘
///     Window
///       Title / Close (Button)   ← closeButton
///       Header (RankingRow)      ← 머리줄
///       Viewport (ScrollRect)
///         Content (VerticalLayoutGroup)   ← content
///           Row (RankingRow, 꺼 둠)         ← rowTemplate. 복제해서 씀
///         Status                 ← statusText
///       MyRank / Text            ← myRankText
///
/// [데이터]  RankingManager.FetchTop 으로 받아 와 그리기만 합니다 (값을 들고 있지 않음).
///   너무 자주 열고 닫아도 서버 조회가 몰리지 않도록 refreshCooldown 동안은 이전 결과를 다시 씁니다.
/// </summary>
public class RankingPanel : MonoBehaviour
{
    [Header("UI (Tools/UI 굽기/랭킹 창 이 채움)")]
    [Tooltip("창 전체 (RankingCanvas). 열고 닫을 때 켜고 끕니다.")]
    [SerializeField] private GameObject root;
    [SerializeField] private Button dimButton;
    [SerializeField] private Button closeButton;
    [Tooltip("목록 줄들이 들어가는 곳 (ScrollRect.content)")]
    [SerializeField] private RectTransform content;
    [Tooltip("목록 한 줄의 모양. 꺼 둔 채로 content 아래에 둡니다.")]
    [SerializeField] private RankingRow rowTemplate;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI myRankText;

    [Header("굽기 설정 (UIBaker 가 UI 를 만들 때만 씀)")]
    [Tooltip("한글이 들어 있는 TMP 폰트 (예: BMJUA_ttf SDF). 비우면 TMP 기본 폰트 — 한글이 □ 로 보일 수 있습니다.")]
    [SerializeField] private TMP_FontAsset font;

    [Tooltip("다른 UI 위에 뜨도록. (플레이 시간 알림 AlarmClock 은 1000 이라 그보다 낮게)")]
    [SerializeField] private int sortingOrder = 500;

    [Header("조회")]
    [Tooltip("이 시간(초) 안에 다시 열면 서버에 다시 묻지 않고 이전 결과를 보여 줍니다.")]
    [SerializeField] private float refreshCooldown = 30f;

    public TMP_FontAsset Font         => font;
    public int           SortingOrder => sortingOrder;

    // 목록을 그릴 때 쓰는 색. UIBaker 도 같은 값으로 구워서 굽는 모양과 그리는 색이 어긋나지 않게 합니다
    // (창 배경처럼 굽고 나면 바뀌지 않는 색은 UIBaker 에만 있음)
    public static readonly Color RowColor    = new Color(1f, 1f, 1f, 0.04f);
    public static readonly Color MyRowColor  = new Color(1f, 0.85f, 0.2f, 0.25f);
    public static readonly Color GoldColor   = new Color(1f, 0.85f, 0.2f);

    // 구분자. 가운뎃점(·)은 BMJUA 폰트에 없어서 □ 로 보이므로 폰트에 있는 '|' 를 씁니다.
    private const string Sep = "  |  ";

    private readonly List<GameObject> rows = new List<GameObject>();

    private List<RankingManager.Entry> cached;
    private float cachedTime = float.NegativeInfinity;
    private bool  loading;

    public bool IsOpen => root != null && root.activeSelf;

    private bool ready;   // 칸이 다 연결돼 있는가 — 비어 있으면 열지 않음 (Awake 에서 한 번 알림)

    private void Awake()
    {
        ready = root != null && content != null && rowTemplate != null && statusText != null && myRankText != null;
        if (!ready)
        {
            Debug.LogError("[RankingPanel] UI 칸이 비어 있습니다. StageScene 을 열고 Tools/UI 굽기/랭킹 창 메뉴로 만든 뒤 씬을 저장하세요.", this);
            return;
        }

        // 버튼은 코드에서 연결합니다 (씬의 OnClick 칸은 비워 둠 — ChatPanel 과 같은 규칙)
        if (dimButton   != null) dimButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        rowTemplate.gameObject.SetActive(false);
        root.SetActive(false);
    }

    // ───────────────────────────── 열기 / 닫기 ─────────────────────────────

    public void Open()
    {
        if (!ready) return;
        root.SetActive(true);

        if (cached != null && Time.unscaledTime - cachedTime < refreshCooldown)
        {
            Draw(cached);
            return;
        }
        Refresh();
    }

    public void Close()
    {
        if (root != null) root.SetActive(false);
    }

    private void Refresh()
    {
        if (loading) return;

        if (RankingManager.Instance == null)
        {
            ShowStatus("랭킹 시스템을 찾을 수 없습니다.");
            Debug.LogError("[RankingPanel] RankingManager 가 없습니다. LoginScene 의 ManagerRoot 하위에 두었는지 확인하세요.");
            return;
        }

        loading = true;
        ClearRows();
        ShowStatus("불러오는 중...");

        RankingManager.Instance.FetchTop((list, error) =>
        {
            if (this == null) return;   // 기다리는 사이 씬이 바뀜
            loading = false;

            if (error != null)
            {
                ClearRows();
                ShowStatus(error);
                UpdateMyRank(null);
                return;
            }

            cached     = list;
            cachedTime = Time.unscaledTime;
            if (IsOpen) Draw(list);
        });
    }

    // ───────────────────────────── 그리기 ─────────────────────────────

    private void Draw(List<RankingManager.Entry> list)
    {
        ClearRows();
        ShowStatus(list.Count == 0 ? "아직 등록된 기록이 없습니다." : string.Empty);

        string myUid = AuthManager.Instance != null ? AuthManager.Instance.Uid : null;
        foreach (RankingManager.Entry e in list)
        {
            RankingRow row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Set(e.Rank.ToString(), e.Nickname, StageRecord.Format(e.Progress),
                    e.Uid == myUid ? MyRowColor : RowColor,
                    e.Rank <= 3 ? GoldColor : Color.white);
            rows.Add(row.gameObject);
        }

        UpdateMyRank(list);
    }

    /// <summary>맨 아래 "내 순위" 줄. 목록에 내가 없으면 내 기록만 보여 줍니다.</summary>
    private void UpdateMyRank(List<RankingManager.Entry> list)
    {
        string myName  = PlayerProfile.DisplayName;
        string myStage = StageRecord.Format(StageRecord.BestProgress);

        if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn)
        {
            myRankText.text = $"{myName}{Sep}{myStage}  (로그인하면 랭킹에 등록됩니다)";
            return;
        }

        if (list != null)
        {
            string myUid = AuthManager.Instance.Uid;
            foreach (RankingManager.Entry e in list)
            {
                if (e.Uid != myUid) continue;
                // 서버 기록으로 표시 — 다른 기기에서 세운 기록이 이 기기 기록보다 높을 수 있음
                myRankText.text = $"내 순위  {e.Rank}위{Sep}{myName}{Sep}{StageRecord.Format(e.Progress)}";
                return;
            }
        }

        int top = RankingManager.Instance != null ? RankingManager.Instance.TopCount : 0;
        myRankText.text = StageRecord.HasRecord
            ? $"내 기록  {myName}{Sep}{myStage}  ({top}위 밖)"
            : $"{myName}{Sep}스테이지를 클리어하면 랭킹에 등록됩니다";
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

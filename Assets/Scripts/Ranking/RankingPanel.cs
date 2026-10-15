using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 랭킹 창 — "순위 / 닉네임 / 스테이지" 목록 + 맨 아래 내 순위.
///
/// [붙이는 위치]
///   StageScene 의 항상 켜진 빈 오브젝트. 창 UI 는 Awake 에서 코드로 만들므로 따로 배치할 것이 없습니다.
///   여는 버튼에는 OpenRankingButton 을 붙이고 이 컴포넌트를 연결하세요.
///
/// [데이터]  RankingManager.FetchTop 으로 받아 와 그리기만 합니다 (값을 들고 있지 않음).
///   너무 자주 열고 닫아도 서버 조회가 몰리지 않도록 refreshCooldown 동안은 이전 결과를 다시 씁니다.
/// </summary>
public class RankingPanel : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("한글이 들어 있는 TMP 폰트 (예: BMJUA_ttf SDF). 비우면 TMP 기본 폰트 — 한글이 □ 로 보일 수 있습니다.")]
    [SerializeField] private TMP_FontAsset font;

    [Tooltip("다른 UI 위에 뜨도록. (플레이 시간 알림 AlarmClock 은 1000 이라 그보다 낮게)")]
    [SerializeField] private int sortingOrder = 500;

    [Header("조회")]
    [Tooltip("이 시간(초) 안에 다시 열면 서버에 다시 묻지 않고 이전 결과를 보여 줍니다.")]
    [SerializeField] private float refreshCooldown = 30f;

    private static readonly Color DimColor    = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color WindowColor = new Color(0.12f, 0.13f, 0.18f, 0.97f);
    private static readonly Color HeaderColor = new Color(0.22f, 0.24f, 0.32f, 1f);
    private static readonly Color RowColor    = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color MyRowColor  = new Color(1f, 0.85f, 0.2f, 0.25f);
    private static readonly Color GoldColor   = new Color(1f, 0.85f, 0.2f);

    // 구분자. 가운뎃점(·)은 BMJUA 폰트에 없어서 □ 로 보이므로 폰트에 있는 '|' 를 씁니다.
    private const string Sep = "  |  ";

    private GameObject      root;
    private RectTransform   content;
    private TextMeshProUGUI statusText;
    private TextMeshProUGUI myRankText;

    private readonly List<GameObject> rows = new List<GameObject>();

    private List<RankingManager.Entry> cached;
    private float cachedTime = float.NegativeInfinity;
    private bool  loading;

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        Build();
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root);   // 캔버스가 내 자식이 아니므로 직접 정리 (Build 주석 참고)
    }

    // ───────────────────────────── 열기 / 닫기 ─────────────────────────────

    public void Open()
    {
        root.SetActive(true);

        if (cached != null && Time.unscaledTime - cachedTime < refreshCooldown)
        {
            Draw(cached);
            return;
        }
        Refresh();
    }

    public void Close() => root.SetActive(false);

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
            rows.Add(MakeRow(content, e.Rank.ToString(), e.Nickname, StageRecord.Format(e.Progress),
                             e.Uid == myUid ? MyRowColor : RowColor, e.Rank <= 3));

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

    // ───────────────────────────── UI 생성 ─────────────────────────────

    private void Build()
    {
        // 캔버스 (프로젝트 다른 씬과 같은 1920x1080 기준)
        // ★ 내 자식으로 두지 않고 씬 최상위에 만듭니다.
        //   이 컴포넌트가 다른 캔버스(예: Safe Panel) 안에 있으면 자식 캔버스는 '중첩 캔버스' 가 되어
        //   sortingOrder·CanvasScaler 가 무시되고, 어두운 배경도 Safe Area 만큼만 덮습니다.
        //   씬 최상위라도 같은 씬 소속이라 씬이 바뀌면 함께 사라지고, 이 컴포넌트가 먼저 파괴되면 OnDestroy 가 지웁니다.
        root = new GameObject("RankingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // 어두운 배경 — 뒤 화면 클릭을 막고, 누르면 닫힘
        RectTransform dim = MakeImage("Dim", root.transform, DimColor, true);
        Stretch(dim);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

        // 창
        RectTransform window = MakeImage("Window", root.transform, WindowColor, true);
        window.sizeDelta = new Vector2(1000f, 900f);

        // 제목
        TextMeshProUGUI title = MakeText("Title", window, "스테이지 랭킹", 52f, TextAlignmentOptions.Center);
        Place(title.rectTransform, 120f, 120f, -20f, 80f);   // 좌우 여백 = 닫기 버튼 자리
        title.color = GoldColor;

        // 닫기 버튼
        RectTransform closeRt = MakeImage("Close", window, HeaderColor, true);
        closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-20f, -20f);
        closeRt.sizeDelta = new Vector2(80f, 80f);
        closeRt.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        Stretch(MakeText("X", closeRt, "X", 44f, TextAlignmentOptions.Center).rectTransform);

        // 머리줄
        GameObject header = MakeRow(window, "순위", "닉네임", "스테이지", HeaderColor, false);
        Place((RectTransform)header.transform, 40f, 40f, -115f, 70f);

        // 목록 (스크롤)
        RectTransform viewport = MakeImage("Viewport", window, Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(40f, 120f);
        viewport.offsetMax = new Vector2(-40f, -195f);

        content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = 6f;
        layout.childControlWidth      = true;
        layout.childControlHeight     = true;
        layout.childForceExpandWidth  = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport          = viewport;
        scroll.content           = content;
        scroll.horizontal        = false;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        // 상태 문구 (불러오는 중 / 오류 / 기록 없음)
        statusText = MakeText("Status", viewport, string.Empty, 36f, TextAlignmentOptions.Center);
        Stretch(statusText.rectTransform);
        statusText.color = new Color(1f, 1f, 1f, 0.7f);

        // 내 순위
        RectTransform myBar = MakeImage("MyRank", window, MyRowColor, false);
        myBar.anchorMin = new Vector2(0f, 0f);
        myBar.anchorMax = new Vector2(1f, 0f);
        myBar.pivot     = new Vector2(0.5f, 0f);
        myBar.offsetMin = new Vector2(40f, 30f);
        myBar.offsetMax = new Vector2(-40f, 100f);
        myRankText = MakeText("Text", myBar, string.Empty, 34f, TextAlignmentOptions.Center);
        Stretch(myRankText.rectTransform);
    }

    /// <summary>순위 / 닉네임 / 스테이지 한 줄. 머리줄도 같은 모양을 씁니다.</summary>
    private GameObject MakeRow(Transform parent, string rank, string nickname, string stage, Color bg, bool highlight)
    {
        RectTransform row = MakeImage("Row", parent, bg, false);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        Color textColor = highlight ? GoldColor : Color.white;
        AddCell(row, rank,     0f,    0.18f, TextAlignmentOptions.Center, textColor);
        AddCell(row, nickname, 0.18f, 0.72f, TextAlignmentOptions.Center, textColor);
        AddCell(row, stage,    0.72f, 1f,    TextAlignmentOptions.Center, textColor);
        return row.gameObject;
    }

    private void AddCell(RectTransform row, string value, float xMin, float xMax, TextAlignmentOptions align, Color color)
    {
        TextMeshProUGUI t = MakeText("Cell", row, value, 34f, align);
        t.rectTransform.anchorMin = new Vector2(xMin, 0f);
        t.rectTransform.anchorMax = new Vector2(xMax, 1f);
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        t.color = color;
    }

    private static RectTransform MakeImage(string name, Transform parent, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = raycast;
        return (RectTransform)go.transform;
    }

    private TextMeshProUGUI MakeText(string name, Transform parent, string value, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text               = value;
        t.fontSize           = size;
        t.alignment          = align;
        t.raycastTarget      = false;
        t.textWrappingMode   = TextWrappingModes.NoWrap;
        t.overflowMode       = TextOverflowModes.Ellipsis;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>창 위쪽에 가로로 꽉 차게 배치 (left/right 여백, 위에서 top 만큼, 높이 height).</summary>
    private static void Place(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, top - height);
        rt.offsetMax = new Vector2(-right, top);
    }
}

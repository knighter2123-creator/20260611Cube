using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>창 UI 를 '처음 한 번' 만들어 Assets/Prefabs/UI 프리팹으로 저장하고 인스펙터 칸을 채우는 Tools/UI 굽기 메뉴 — 이미 있으면 덮지 않고 안내만 (손으로 고친 UI 보호), 실행 후 씬 저장.</summary>
// 메뉴: 랭킹 창·우편함·채팅 탭·친구 창(StageScene) / 저장 데이터 선택 팝업(LoginScene) / 친구 검색 칸 / 설정 창 효과음·배경음 슬라이더 / 열린 씬의 창을 프리팹으로 저장
// ⚠ UI 를 고칠 땐 메뉴를 다시 누르지 말고 프리팹을 하이어라키에서 직접 수정
public static class UIBaker
{
    // 굽고 나면 런타임 코드가 바꾸지 않는 값 — 여기에만 둡니다 (목록 줄 색처럼 런타임도 쓰는 값은 각 컴포넌트에)
    private static readonly Color RankDimColor    = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color RankWindowColor = new Color(0.12f, 0.13f, 0.18f, 0.97f);
    private static readonly Color RankHeaderColor = new Color(0.22f, 0.24f, 0.32f, 1f);

    private const int PopupSortingOrder = 3000;   // AccountReset 의 입력 차단막(short.MaxValue) 보다는 아래, 다른 UI 보다는 위
    private static readonly Color PopupDimColor    = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color PopupWindowColor = new Color(0.12f, 0.13f, 0.18f, 0.98f);
    private static readonly Color PopupBoxColor    = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color PopupLocalColor  = new Color(0.30f, 0.55f, 0.95f, 1f);
    private static readonly Color PopupCloudColor  = new Color(0.30f, 0.75f, 0.45f, 1f);
    private static readonly Color GoldColor        = new Color(1f, 0.85f, 0.2f);

    // ═════════════════════════════ 랭킹 창 ═════════════════════════════

    [MenuItem("Tools/UI 굽기/랭킹 창 (StageScene)")]
    private static void BakeRankingPanel()
    {
        RankingPanel panel = FindInOpenScenes<RankingPanel>();
        if (panel == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 RankingPanel 이 없습니다. StageScene 을 열고 다시 실행하세요.", "확인");
            return;
        }

        var so = new SerializedObject(panel);
        if (AlreadyExists(so.FindProperty("root").objectReferenceValue as GameObject, "랭킹 창")) return;

        TMP_FontAsset font = panel.Font != null ? panel.Font : MostUsedFont(panel.gameObject.scene);
        var b = new Builder(font);

        // 캔버스 — 씬 최상위 (RankingPanel 클래스 주석의 이유)
        GameObject root = b.Canvas("RankingCanvas", panel.SortingOrder, panel.gameObject.scene);

        RectTransform dim = b.Image("Dim", root.transform, RankDimColor, true);
        Builder.Stretch(dim);
        Button dimButton = dim.gameObject.AddComponent<Button>();

        RectTransform window = b.Image("Window", root.transform, RankWindowColor, true);
        window.sizeDelta = new Vector2(1000f, 900f);

        TextMeshProUGUI title = b.Text("Title", window, "스테이지 랭킹", 52f, TextAlignmentOptions.Center, false);
        Builder.PlaceTop(title.rectTransform, 120f, 120f, -20f, 80f);   // 좌우 여백 = 닫기 버튼 자리
        title.color = GoldColor;

        RectTransform closeRt = b.Image("Close", window, RankHeaderColor, true);
        closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-20f, -20f);
        closeRt.sizeDelta = new Vector2(80f, 80f);
        Button closeButton = closeRt.gameObject.AddComponent<Button>();
        Builder.Stretch(b.Text("X", closeRt, "X", 44f, TextAlignmentOptions.Center, false).rectTransform);

        // 머리줄 — 목록 줄과 같은 칸 배치. 글자가 바뀌지 않으므로 RankingRow 는 붙이지 않음
        RectTransform header = RowCells(b, window, "Header", "순위", "닉네임", "스테이지", RankHeaderColor, out _, out _, out _);
        Builder.PlaceTop(header, 40f, 40f, -115f, 70f);

        // 목록 (스크롤)
        RectTransform viewport = b.Image("Viewport", window, Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(40f, 120f);
        viewport.offsetMax = new Vector2(-40f, -195f);

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.gameObject.layer = root.layer;
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

        // 목록 한 줄의 모양 — 꺼 둔 채로 content 아래에 (RankingPanel 이 복제해서 씀)
        RankingRow rowTemplate = RankingRowObject(b, content, "Row", "1", "닉네임", "1-1", RankingPanel.RowColor);
        rowTemplate.gameObject.SetActive(false);

        // 상태 문구 (불러오는 중 / 오류 / 기록 없음)
        TextMeshProUGUI status = b.Text("Status", viewport, string.Empty, 36f, TextAlignmentOptions.Center, false);
        Builder.Stretch(status.rectTransform);
        status.color = new Color(1f, 1f, 1f, 0.7f);

        // 내 순위
        RectTransform myBar = b.Image("MyRank", window, RankingPanel.MyRowColor, false);
        myBar.anchorMin = new Vector2(0f, 0f);
        myBar.anchorMax = new Vector2(1f, 0f);
        myBar.pivot     = new Vector2(0.5f, 0f);
        myBar.offsetMin = new Vector2(40f, 30f);
        myBar.offsetMax = new Vector2(-40f, 100f);
        TextMeshProUGUI myRank = b.Text("Text", myBar, string.Empty, 34f, TextAlignmentOptions.Center, false);
        Builder.Stretch(myRank.rectTransform);

        root.SetActive(false);   // 평소엔 닫혀 있음 (RankingPanel.Awake 도 끔)
        Undo.RegisterCreatedObjectUndo(root, "랭킹 창 굽기");

        so.Update();   // ApplyModifiedProperties 가 RankingPanel 변경을 Undo 에 기록함
        so.FindProperty("root").objectReferenceValue        = root;
        so.FindProperty("dimButton").objectReferenceValue   = dimButton;
        so.FindProperty("closeButton").objectReferenceValue = closeButton;
        so.FindProperty("content").objectReferenceValue     = content;
        so.FindProperty("rowTemplate").objectReferenceValue = rowTemplate;
        so.FindProperty("statusText").objectReferenceValue  = status;
        so.FindProperty("myRankText").objectReferenceValue  = myRank;
        if (panel.Font == null) so.FindProperty("font").objectReferenceValue = font;
        so.ApplyModifiedProperties();

        Finish(panel.gameObject.scene, root, "랭킹 창", SaveAsPrefab(root, "RankingCanvas"));
    }

    /// <summary>순위 / 닉네임 / 스테이지 칸 세 개가 있는 줄 (배경 Image)</summary>
    private static RectTransform RowCells(Builder b, Transform parent, string name, string rank, string nickname, string stage, Color bg,
                                          out TextMeshProUGUI rankText, out TextMeshProUGUI nameText, out TextMeshProUGUI stageText)
    {
        RectTransform row = b.Image(name, parent, bg, false);
        rankText  = Cell(b, row, rank,     0f,    0.18f);
        nameText  = Cell(b, row, nickname, 0.18f, 0.72f);
        stageText = Cell(b, row, stage,    0.72f, 1f);
        return row;
    }

    /// <summary>목록 한 줄 — RowCells + 줄 높이(LayoutElement, 목록의 VerticalLayoutGroup 용) + RankingRow 칸 채우기</summary>
    private static RankingRow RankingRowObject(Builder b, Transform parent, string name, string rank, string nickname, string stage, Color bg)
    {
        RectTransform row = RowCells(b, parent, name, rank, nickname, stage, bg,
                                     out TextMeshProUGUI rankText, out TextMeshProUGUI nameText, out TextMeshProUGUI stageText);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

        var comp = row.gameObject.AddComponent<RankingRow>();
        var so = new SerializedObject(comp);
        so.FindProperty("background").objectReferenceValue = row.GetComponent<Image>();
        so.FindProperty("rankText").objectReferenceValue   = rankText;
        so.FindProperty("nameText").objectReferenceValue   = nameText;
        so.FindProperty("stageText").objectReferenceValue  = stageText;
        so.ApplyModifiedPropertiesWithoutUndo();   // 막 만든 오브젝트 — 생성 Undo 에 함께 묶임
        return comp;
    }

    private static TextMeshProUGUI Cell(Builder b, RectTransform row, string value, float xMin, float xMax)
    {
        TextMeshProUGUI t = b.Text("Cell", row, value, 34f, TextAlignmentOptions.Center, false);
        t.rectTransform.anchorMin = new Vector2(xMin, 0f);
        t.rectTransform.anchorMax = new Vector2(xMax, 1f);
        t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        return t;
    }

    // ═════════════════════════════ 저장 데이터 선택 팝업 ═════════════════════════════

    [MenuItem("Tools/UI 굽기/저장 데이터 선택 팝업 (LoginScene)")]
    private static void BakeCloudSavePopup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != SceneLoader.LOGIN_SCENE &&
            !EditorUtility.DisplayDialog("UI 굽기", $"지금 활성 씬은 '{scene.name}' 입니다. 이 팝업은 LoginScene 에 있어야 합니다.\n그래도 이 씬에 만들까요?", "만들기", "취소"))
            return;

        CloudSaveConflictPopup old = FindInOpenScenes<CloudSaveConflictPopup>();
        if (AlreadyExists(old != null ? old.gameObject : null, "저장 데이터 선택 팝업")) return;
        TMP_FontAsset font = MostUsedFont(scene);

        var b = new Builder(font);

        // 캔버스 — 씬 최상위, 항상 켜 둠 (Awake 에서 Instance 등록). 창(panel)만 끔
        GameObject root = b.Canvas("CloudSaveConflictCanvas", PopupSortingOrder, scene);
        var popup = root.AddComponent<CloudSaveConflictPopup>();

        var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.gameObject.layer = root.layer;
        panel.SetParent(root.transform, false);
        Builder.Stretch(panel);

        // 어두운 배경 — 뒤 화면 터치만 막음 (Button 없음 → 눌러도 닫히지 않음)
        Builder.Stretch(b.Image("Dim", panel, PopupDimColor, true));

        RectTransform window = b.Image("Window", panel, PopupWindowColor, true);
        window.sizeDelta = new Vector2(1300f, 760f);

        TextMeshProUGUI title = b.Text("Title", window, "저장 데이터 선택", 54f, TextAlignmentOptions.Center, true);
        Builder.PlaceTop(title.rectTransform, 40f, 40f, -30f, 80f);
        title.color = GoldColor;

        TextMeshProUGUI desc = b.Text("Desc", window,
            "이 기기와 클라우드에 서로 다른 진행이 있습니다.\n사용할 데이터를 골라 주세요. 고르지 않은 쪽은 사라집니다.",
            32f, TextAlignmentOptions.Center, true);
        Builder.PlaceTop(desc.rectTransform, 60f, 60f, -115f, 100f);

        const string sample = "스테이지 1-1  (최고 -)\n레벨 1   골드 0   보석 0   동료 0명\n저장 시각 알 수 없음";   // 미리보기용 — 실행 중에 바뀜
        Side local = PopupSide(b, window, 0f,   0.5f, "이 기기",  sample, PopupLocalColor, CloudSaveConflictPopup.LocalButtonText);
        Side cloud = PopupSide(b, window, 0.5f, 1f,   "클라우드", sample, PopupCloudColor, CloudSaveConflictPopup.CloudButtonText);

        TextMeshProUGUI notice = b.Text("Notice", window, string.Empty, 28f, TextAlignmentOptions.Center, true);
        RectTransform n = notice.rectTransform;
        n.anchorMin = new Vector2(0f, 0f);
        n.anchorMax = new Vector2(1f, 0f);
        n.pivot     = new Vector2(0.5f, 0f);
        n.offsetMin = new Vector2(40f, 25f);
        n.offsetMax = new Vector2(-40f, 75f);

        panel.gameObject.SetActive(false);   // 평소엔 닫혀 있음 (Awake 도 끔)

        var so = new SerializedObject(popup);
        so.FindProperty("panel").objectReferenceValue        = panel.gameObject;
        so.FindProperty("localSummary").objectReferenceValue = local.Summary;
        so.FindProperty("cloudSummary").objectReferenceValue = cloud.Summary;
        so.FindProperty("localButton").objectReferenceValue  = local.Button;
        so.FindProperty("cloudButton").objectReferenceValue  = cloud.Button;
        so.FindProperty("localLabel").objectReferenceValue   = local.Label;
        so.FindProperty("cloudLabel").objectReferenceValue   = cloud.Label;
        so.FindProperty("noticeText").objectReferenceValue   = notice;
        so.FindProperty("font").objectReferenceValue         = font;
        so.ApplyModifiedPropertiesWithoutUndo();   // 막 만든 오브젝트 — 아래 생성 Undo 에 함께 묶임

        Undo.RegisterCreatedObjectUndo(root, "저장 데이터 선택 팝업 굽기");
        Finish(scene, root, "저장 데이터 선택 팝업", SaveAsPrefab(root, "CloudSaveConflictCanvas"));
    }

    private struct Side
    {
        public TextMeshProUGUI Summary;
        public Button          Button;
        public TextMeshProUGUI Label;
    }

    /// <summary>한쪽 칸 — 이름, 요약, 고르기 버튼 (xMin~xMax 는 창 가로 비율)</summary>
    private static Side PopupSide(Builder b, RectTransform window, float xMin, float xMax, string name, string summary, Color color, string buttonText)
    {
        RectTransform box = b.Image(name, window, PopupBoxColor, false);
        box.anchorMin = new Vector2(xMin, 0f);
        box.anchorMax = new Vector2(xMax, 1f);
        box.offsetMin = new Vector2(xMin == 0f ? 50f : 15f, 95f);
        box.offsetMax = new Vector2(xMax == 1f ? -50f : -15f, -235f);

        TextMeshProUGUI header = b.Text("Name", box, name, 40f, TextAlignmentOptions.Center, true);
        Builder.PlaceTop(header.rectTransform, 20f, 20f, -15f, 55f);
        header.color = color;

        TextMeshProUGUI body = b.Text("Summary", box, summary, 28f, TextAlignmentOptions.Center, true);
        Builder.PlaceTop(body.rectTransform, 20f, 20f, -80f, 150f);

        RectTransform button = b.Image("Button", box, color, true);
        button.anchorMin = new Vector2(0f, 0f);
        button.anchorMax = new Vector2(1f, 0f);
        button.pivot     = new Vector2(0.5f, 0f);
        button.offsetMin = new Vector2(40f, 20f);
        button.offsetMax = new Vector2(-40f, 105f);
        Button btn = button.gameObject.AddComponent<Button>();

        TextMeshProUGUI label = b.Text("Label", button, buttonText, 34f, TextAlignmentOptions.Center, true);
        Builder.Stretch(label.rectTransform);

        return new Side { Summary = body, Button = btn, Label = label };
    }

    // ═════════════════════════════ 채팅 탭 · 유저 메뉴 · 친구 창 ═════════════════════════════

    // 채팅 Window 왼쪽에 사이드바를 넣으면서 Window 를 이만큼 넓히고, 왼쪽에 붙은 기존 UI 를 이만큼 오른쪽으로 밉니다.
    private const float ChatSidebarLeft  = 30f;
    private const float ChatSidebarWidth = 260f;
    private const float ChatSidebarSpace = ChatSidebarLeft + ChatSidebarWidth + 10f;   // = 기존 UI 가 밀리는 거리

    private static readonly Color ChatSidebarColor = new Color(0f, 0f, 0f, 0.25f);
    private static readonly Color MenuDimColor     = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color DangerColor      = new Color(0.75f, 0.28f, 0.28f, 1f);

    [MenuItem("Tools/UI 굽기/채팅 탭·친구 창 (StageScene)")]
    private static void BakeChatSocial()
    {
        ChatPanel chat = FindInOpenScenes<ChatPanel>();
        if (chat == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 ChatPanel 이 없습니다. StageScene 을 열고 다시 실행하세요.", "확인");
            return;
        }

        var so = new SerializedObject(chat);
        var scroll  = so.FindProperty("scroll").objectReferenceValue as ScrollRect;
        var panelGo = so.FindProperty("panel").objectReferenceValue as GameObject;
        if (scroll == null || panelGo == null || scroll.transform.parent == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "ChatPanel 의 panel·scroll 칸이 비어 있습니다. 채팅 창을 먼저 연결하세요.", "확인");
            return;
        }

        var window = (RectTransform)scroll.transform.parent;   // Viewport 의 부모 = 채팅 Window
        var panel  = (RectTransform)panelGo.transform;

        // 친구 버튼은 랭킹 버튼을 복제해 그 오른쪽에 둠 — 랭킹 버튼이 없으면 시작 전에 멈춤
        OpenRankingButton rankingButton = FindInOpenScenes<OpenRankingButton>();
        if (rankingButton == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 OpenRankingButton 이 없습니다. 친구 버튼을 그 옆에 만들 수 없습니다.", "확인");
            return;
        }

        // 이미 구운 것이 하나라도 있으면 멈춤 — 지우고 새로 만들면 손으로 고친 내용이 사라짐
        FriendPanel friends = chat.GetComponent<FriendPanel>();
        bool baked = window.Find("Sidebar") != null || window.Find("Leave") != null
                  || so.FindProperty("userMenu").objectReferenceValue != null
                  || (friends != null && new SerializedObject(friends).FindProperty("root").objectReferenceValue != null)
                  || FindInOpenScenes<OpenFriendsButton>() != null;
        if (AlreadyExists(baked ? chat.gameObject : null, "채팅 탭·친구 창")) return;

        if (PrefabUtility.IsPartOfPrefabInstance(chat.gameObject) &&
            !EditorUtility.DisplayDialog("UI 굽기", "ChatCanvas 가 이미 프리팹입니다. 씬에만 만들어지고 프리팹에는 자동 반영되지 않습니다 " +
                                                   "(만든 뒤 인스펙터의 Overrides > Apply All 로 반영하세요). 계속할까요?", "계속", "취소"))
            return;

        const float shifted = 0f;

        var lineTemplate = so.FindProperty("logLineTemplate").objectReferenceValue as TMP_Text;
        TMP_FontAsset font = lineTemplate != null && lineTemplate.font != null ? lineTemplate.font : MostUsedFont(chat.gameObject.scene);
        var b = new Builder(font);

        // ── 사이드바 자리 만들기 — Window 를 넓히고, 왼쪽(또는 가운데)에 붙은 자식을 오른쪽으로 밈 ──
        float delta = ChatSidebarSpace - shifted;
        if (!Mathf.Approximately(delta, 0f))
        {
            Undo.RecordObject(window, "채팅 탭 굽기");
            window.sizeDelta += new Vector2(delta, 0f);
            foreach (RectTransform child in window)
            {
                Undo.RecordObject(child, "채팅 탭 굽기");
                if (Mathf.Approximately(child.anchorMin.x, 0f))
                {
                    child.offsetMin += new Vector2(delta, 0f);
                    if (Mathf.Approximately(child.anchorMax.x, 0f)) child.offsetMax += new Vector2(delta, 0f);
                }
                else if (Mathf.Approximately(child.anchorMin.x, 0.5f) && Mathf.Approximately(child.anchorMax.x, 0.5f))
                    child.anchoredPosition += new Vector2(delta * 0.5f, 0f);   // 가운데 정렬 UI 는 남은 칸의 가운데로
            }
        }

        // ── 사이드바 ──
        RectTransform sidebar = b.Image("Sidebar", window, ChatSidebarColor, true);
        sidebar.anchorMin = new Vector2(0f, 0f);
        sidebar.anchorMax = new Vector2(0f, 1f);
        sidebar.pivot     = new Vector2(0f, 0.5f);
        sidebar.offsetMin = new Vector2(ChatSidebarLeft, 30f);
        sidebar.offsetMax = new Vector2(ChatSidebarLeft + ChatSidebarWidth, -30f);
        sidebar.SetAsFirstSibling();   // 다른 UI 뒤에

        RectTransform tabList = VerticalScroll(b, sidebar, "Tabs", "TabList", 8f, out RectTransform tabsViewport);
        tabsViewport.offsetMin = new Vector2(10f, 10f);
        tabsViewport.offsetMax = new Vector2(-10f, -10f);

        RectTransform tabRt = b.Image("Tab", tabList, ChatPanel.TabColor, true);
        tabRt.gameObject.AddComponent<LayoutElement>().preferredHeight = 80f;
        Button tabTemplate = tabRt.gameObject.AddComponent<Button>();
        TextMeshProUGUI tabLabel = b.Text("Label", tabRt, "전체 채팅", 32f, TextAlignmentOptions.MidlineLeft, false);
        Builder.Stretch(tabLabel.rectTransform);
        tabLabel.rectTransform.offsetMin = new Vector2(20f, 0f);
        tabLabel.rectTransform.offsetMax = new Vector2(-12f, 0f);
        tabRt.gameObject.SetActive(false);

        Undo.RegisterCreatedObjectUndo(sidebar.gameObject, "채팅 탭 굽기");

        // ── 개인 채팅 [나가기] — 오른쪽 위, 닫기 버튼 왼쪽 (개인 탭을 볼 때만 ChatPanel 이 켬) ──
        Button leaveButton = LabeledButton(b, window, "Leave", "나가기", DangerColor, 32f, out _);
        var lb = (RectTransform)leaveButton.transform;
        lb.anchorMin = lb.anchorMax = lb.pivot = new Vector2(1f, 1f);
        lb.anchoredPosition = new Vector2(-120f, -20f);
        lb.sizeDelta        = new Vector2(200f, 80f);
        leaveButton.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(leaveButton.gameObject, "채팅 나가기 버튼 굽기");

        // ── 유저 메뉴 (누르기) ──
        RectTransform menu = Container("UserMenu", panel);
        RectTransform menuDim = b.Image("Dim", menu, MenuDimColor, true);
        Builder.Stretch(menuDim);
        Button menuDimButton = menuDim.gameObject.AddComponent<Button>();

        RectTransform box = b.Image("Box", menu, PopupWindowColor, true);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0.5f);
        box.pivot     = new Vector2(0f, 1f);
        box.sizeDelta = new Vector2(440f, 410f);

        TextMeshProUGUI menuTitle = b.Text("Title", box, "닉네임", 36f, TextAlignmentOptions.Center, false);
        Builder.PlaceTop(menuTitle.rectTransform, 20f, 20f, -20f, 60f);
        menuTitle.color = GoldColor;

        Button menuFriend  = LabeledButton(b, box, "Friend",  "친구 추가",   PopupCloudColor, 34f, out TextMeshProUGUI menuFriendLabel);
        Button menuMessage = LabeledButton(b, box, "Message", "개인 메시지", PopupLocalColor, 34f, out _);
        Button menuBlock   = LabeledButton(b, box, "Block",   "채팅 차단",   DangerColor,     34f, out _);
        Builder.PlaceTop((RectTransform)menuFriend.transform,  20f, 20f, -95f,  90f);
        Builder.PlaceTop((RectTransform)menuMessage.transform, 20f, 20f, -200f, 90f);
        Builder.PlaceTop((RectTransform)menuBlock.transform,   20f, 20f, -305f, 90f);

        menu.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(menu.gameObject, "채팅 유저 메뉴 굽기");

        // ── 친구 창 — 채팅 창(Panel) 밖, ChatCanvas 바로 아래: 채팅을 열지 않아도 랭킹 옆 버튼으로 열림 ──
        RectTransform friendsRoot = Container("Friends", chat.transform);
        RectTransform friendsDim = b.Image("Dim", friendsRoot, RankDimColor, true);
        Builder.Stretch(friendsDim);
        Button friendsDimButton = friendsDim.gameObject.AddComponent<Button>();

        RectTransform fw = b.Image("Window", friendsRoot, RankWindowColor, true);
        fw.sizeDelta = new Vector2(1000f, 860f);

        TextMeshProUGUI fTitle = b.Text("Title", fw, "친구", 52f, TextAlignmentOptions.Center, false);
        Builder.PlaceTop(fTitle.rectTransform, 260f, 260f, -20f, 80f);
        fTitle.color = GoldColor;

        // 친구 목록 ↔ 차단 목록 전환 (왼쪽 위)
        Button fBlockList = LabeledButton(b, fw, "BlockList", "차단 목록", RankHeaderColor, 30f, out TextMeshProUGUI fBlockListLabel);
        var blRt = (RectTransform)fBlockList.transform;
        blRt.anchorMin = blRt.anchorMax = blRt.pivot = new Vector2(0f, 1f);
        blRt.anchoredPosition = new Vector2(20f, -20f);
        blRt.sizeDelta        = new Vector2(220f, 80f);

        RectTransform fClose = b.Image("Close", fw, RankHeaderColor, true);
        fClose.anchorMin = fClose.anchorMax = fClose.pivot = new Vector2(1f, 1f);
        fClose.anchoredPosition = new Vector2(-20f, -20f);
        fClose.sizeDelta = new Vector2(80f, 80f);
        Button fCloseButton = fClose.gameObject.AddComponent<Button>();
        Builder.Stretch(b.Text("X", fClose, "X", 44f, TextAlignmentOptions.Center, false).rectTransform);

        RectTransform fContent = VerticalScroll(b, fw, "Viewport", "Content", 6f, out RectTransform fViewport);
        fViewport.offsetMin = new Vector2(40f, 120f);
        fViewport.offsetMax = new Vector2(-40f, -120f);

        TextMeshProUGUI header = b.Text("Header", fContent, "받은 친구 요청", 34f, TextAlignmentOptions.BottomLeft, false);
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 60f;
        header.margin = new Vector4(10f, 0f, 0f, 6f);
        header.gameObject.SetActive(false);

        FriendRow rowTemplate = FriendRowObject(b, fContent);
        rowTemplate.gameObject.SetActive(false);

        TextMeshProUGUI fStatus = b.Text("Status", fViewport, string.Empty, 36f, TextAlignmentOptions.Center, true);
        Builder.Stretch(fStatus.rectTransform);
        fStatus.color = new Color(1f, 1f, 1f, 0.7f);

        TextMeshProUGUI fNotice = b.Text("Notice", fw, "채팅에서 다른 유저의 메시지를 누르면 친구 신청을 할 수 있습니다.",
                                         28f, TextAlignmentOptions.Center, true);
        RectTransform nRt = fNotice.rectTransform;
        nRt.anchorMin = new Vector2(0f, 0f);
        nRt.anchorMax = new Vector2(1f, 0f);
        nRt.pivot     = new Vector2(0.5f, 0f);
        nRt.offsetMin = new Vector2(40f, 30f);
        nRt.offsetMax = new Vector2(-40f, 100f);
        fNotice.color = new Color(1f, 1f, 1f, 0.75f);

        friendsRoot.gameObject.SetActive(false);
        Undo.RegisterCreatedObjectUndo(friendsRoot.gameObject, "친구 창 굽기");

        if (friends == null) friends = Undo.AddComponent<FriendPanel>(chat.gameObject);   // ChatCanvas — 항상 켜져 있음
        var fso = new SerializedObject(friends);
        fso.FindProperty("root").objectReferenceValue           = friendsRoot.gameObject;
        fso.FindProperty("dimButton").objectReferenceValue      = friendsDimButton;
        fso.FindProperty("closeButton").objectReferenceValue    = fCloseButton;
        fso.FindProperty("titleText").objectReferenceValue      = fTitle;
        fso.FindProperty("blockListButton").objectReferenceValue = fBlockList;
        fso.FindProperty("blockListLabel").objectReferenceValue  = fBlockListLabel;
        fso.FindProperty("content").objectReferenceValue        = fContent;
        fso.FindProperty("headerTemplate").objectReferenceValue = header;
        fso.FindProperty("rowTemplate").objectReferenceValue    = rowTemplate;
        fso.FindProperty("statusText").objectReferenceValue     = fStatus;
        fso.FindProperty("noticeText").objectReferenceValue     = fNotice;
        fso.FindProperty("chatPanel").objectReferenceValue      = chat;
        fso.ApplyModifiedProperties();

        // ── ChatPanel 칸 채우기 ──
        Transform titleTr = window.Find("Title");
        TextMeshProUGUI title = titleTr != null ? titleTr.GetComponent<TextMeshProUGUI>() : null;

        so.Update();
        so.FindProperty("tabList").objectReferenceValue           = tabList;
        so.FindProperty("tabTemplate").objectReferenceValue       = tabTemplate;
        so.FindProperty("leaveButton").objectReferenceValue       = leaveButton;
        so.FindProperty("userMenu").objectReferenceValue          = menu.gameObject;
        so.FindProperty("userMenuDim").objectReferenceValue       = menuDimButton;
        so.FindProperty("userMenuBox").objectReferenceValue       = box;
        so.FindProperty("userMenuTitle").objectReferenceValue     = menuTitle;
        so.FindProperty("menuFriendButton").objectReferenceValue  = menuFriend;
        so.FindProperty("menuFriendLabel").objectReferenceValue   = menuFriendLabel;
        so.FindProperty("menuMessageButton").objectReferenceValue = menuMessage;
        so.FindProperty("menuBlockButton").objectReferenceValue   = menuBlock;
        if (title != null) so.FindProperty("titleText").objectReferenceValue = title;
        so.ApplyModifiedProperties();

        // ── 친구 창 위쪽의 닉네임 검색 칸 ──
        AddFriendSearchObjects(b, friends);

        // ── 친구 버튼 — 랭킹 버튼 오른쪽, 그 줄의 버튼 간격 그대로 (HUD 에 있으므로 씬 오브젝트로 둠) ──
        GameObject friendButton = FriendsButtonFrom(rankingButton, friends);

        string prefab = PrefabUtility.IsPartOfPrefabInstance(chat.gameObject) ? null : SaveAsPrefab(chat.gameObject, "ChatCanvas");
        Finish(chat.gameObject.scene, friendButton, "채팅 탭·유저 메뉴·친구 창·친구 버튼", prefab);
    }

    // 검색 칸 자리 — 목록(Viewport) 위쪽을 이만큼 내려서 만듦
    private const float FriendSearchTop    = -120f;
    private const float FriendSearchHeight = 90f;
    private const float FriendListTopWithSearch = -230f;

    /// <summary>친구 창에 닉네임 검색 칸이 없을 때만 더함 (프리팹이면 그 변경만 프리팹에도 반영).</summary>
    [MenuItem("Tools/UI 굽기/친구 창 - 닉네임 검색 칸 (StageScene)")]
    private static void BakeFriendSearch()
    {
        FriendPanel friends = FindInOpenScenes<FriendPanel>();
        if (friends == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 FriendPanel 이 없습니다. StageScene 을 열고 '채팅 탭·친구 창' 부터 구우세요.", "확인");
            return;
        }

        if (!TryAddFriendSearch(friends, out string error, out string prefabNote))
        {
            if (error != null) EditorUtility.DisplayDialog("UI 굽기", error, "확인");
            return;
        }
        Finish(friends.gameObject.scene, friends.gameObject, "친구 창 닉네임 검색 칸", prefabNote);
    }

    /// <summary>검색 칸이 없으면 만들고 프리팹에 반영해 true, 이미 있으면 false + 안내 (프리팹 저장 전에도 호출).</summary>
    private static bool TryAddFriendSearch(FriendPanel friends, out string error, out string prefabNote)
    {
        error = null;
        prefabNote = null;

        var so = new SerializedObject(friends);
        if (so.FindProperty("searchInput").objectReferenceValue != null)
        {
            error = "친구 창에 이미 닉네임 검색 칸이 있습니다. 모양은 하이어라키(프리팹)에서 직접 고치세요.";
            return false;
        }

        var content = so.FindProperty("content").objectReferenceValue as RectTransform;
        TMP_FontAsset font = content != null ? content.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null) : null;
        if (font == null) font = MostUsedFont(friends.gameObject.scene);

        if (!AddFriendSearchObjects(new Builder(font), friends, out RectTransform bar, out RectTransform viewport))
        {
            error = "FriendPanel 의 content 칸이 비어 있습니다. '채팅 탭·친구 창' 부터 구우세요.";
            return false;
        }

        // 프리팹 인스턴스면 이번에 바꾼 것만 프리팹에 반영 (Apply All 처럼 다른 수정까지 밀어 넣지 않음)
        if (PrefabUtility.IsPartOfPrefabInstance(friends.gameObject))
        {
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(friends.gameObject);
            PrefabUtility.ApplyAddedGameObject(bar.gameObject, path, InteractionMode.UserAction);
            PrefabUtility.ApplyObjectOverride(viewport, path, InteractionMode.UserAction);
            PrefabUtility.ApplyObjectOverride(friends,  path, InteractionMode.UserAction);
            prefabNote = $"프리팹에도 반영: {path}";
        }
        return true;
    }

    /// <summary>검색 칸 오브젝트를 만들고 FriendPanel 칸을 채웁니다 (있는지 검사는 부른 쪽에서).</summary>
    private static bool AddFriendSearchObjects(Builder b, FriendPanel friends) => AddFriendSearchObjects(b, friends, out _, out _);

    private static bool AddFriendSearchObjects(Builder b, FriendPanel friends, out RectTransform bar, out RectTransform viewport)
    {
        bar = null;
        var so = new SerializedObject(friends);
        var content = so.FindProperty("content").objectReferenceValue as RectTransform;
        viewport = content != null ? content.parent as RectTransform : null;
        var window = viewport != null ? viewport.parent as RectTransform : null;
        if (window == null) return false;

        // 목록을 아래로 내려 자리 만들기
        Undo.RecordObject(viewport, "친구 검색 칸 굽기");
        viewport.offsetMax = new Vector2(viewport.offsetMax.x, FriendListTopWithSearch);

        bar = Container("Search", window);
        Builder.PlaceTop(bar, 40f, 40f, FriendSearchTop, FriendSearchHeight);

        TMP_InputField input = InputBox(b, bar, "Input", "친구 닉네임", 34f, PlayerProfile.MAX_LENGTH);
        var inRt = (RectTransform)input.transform;
        inRt.anchorMin = Vector2.zero;
        inRt.anchorMax = Vector2.one;
        inRt.offsetMin = Vector2.zero;
        inRt.offsetMax = new Vector2(-250f, 0f);

        Button send = LabeledButton(b, bar, "Request", "친구 요청", PopupCloudColor, 32f, out _);
        var sRt = (RectTransform)send.transform;
        sRt.anchorMin = new Vector2(1f, 0f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot     = new Vector2(1f, 0.5f);
        sRt.offsetMin = new Vector2(-230f, 0f);
        sRt.offsetMax = Vector2.zero;

        Undo.RegisterCreatedObjectUndo(bar.gameObject, "친구 검색 칸 굽기");

        so.Update();
        so.FindProperty("searchInput").objectReferenceValue  = input;
        so.FindProperty("searchButton").objectReferenceValue = send;
        so.ApplyModifiedProperties();
        return true;
    }

    /// <summary>한 줄 입력 칸 (배경 + 글자 영역 RectMask2D + 안내 글자 + 입력 글자)</summary>
    private static TMP_InputField InputBox(Builder b, Transform parent, string name, string placeholder, float size, int limit)
    {
        RectTransform bg = b.Image(name, parent, new Color(1f, 1f, 1f, 0.08f), true);

        RectTransform area = Container("Text Area", bg);
        area.offsetMin = new Vector2(20f, 6f);
        area.offsetMax = new Vector2(-20f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI hint = b.Text("Placeholder", area, placeholder, size, TextAlignmentOptions.MidlineLeft, false);
        Builder.Stretch(hint.rectTransform);
        hint.color = new Color(1f, 1f, 1f, 0.4f);

        TextMeshProUGUI text = b.Text("Text", area, string.Empty, size, TextAlignmentOptions.MidlineLeft, false);
        Builder.Stretch(text.rectTransform);
        text.richText = false;   // 입력한 <b> 같은 글자가 서식으로 바뀌지 않게

        var input = bg.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic  = bg.GetComponent<Image>();
        input.textViewport   = area;
        input.textComponent  = text;
        input.placeholder    = hint;
        input.characterLimit = limit;
        input.lineType       = TMP_InputField.LineType.SingleLine;
        input.richText       = false;
        input.pointSize      = size;
        return input;
    }

    /// <summary>랭킹 버튼을 복제해 오른쪽에 친구 버튼 생성 (간격 = 왼쪽 이웃과의 간격, 없으면 폭 + 16).</summary>
    private static GameObject FriendsButtonFrom(OpenRankingButton ranking, FriendPanel friends)
    {
        GameObject go = CloneButtonRightOf(ranking, "Open Friends Button", "친구", out TextMeshProUGUI label);

        var open = go.AddComponent<OpenFriendsButton>();
        var so = new SerializedObject(open);
        so.FindProperty("friendPanel").objectReferenceValue = friends;
        so.FindProperty("label").objectReferenceValue       = label;
        so.ApplyModifiedPropertiesWithoutUndo();   // 막 만든 오브젝트 — 아래 생성 Undo 에 함께 묶임

        Undo.RegisterCreatedObjectUndo(go, "친구 버튼 굽기");
        return go;
    }

    /// <summary>버튼(source)을 복제해 오른쪽에 둠(간격 규칙 동일) — 복제본의 여는 컴포넌트·OnClick 은 지우고 새 컴포넌트는 부른 쪽이 붙임.</summary>
    private static GameObject CloneButtonRightOf(Component source, string name, string text, out TextMeshProUGUI label)
    {
        var srcRt = (RectTransform)source.transform;

        float spacing = srcRt.rect.width + 16f;
        float nearest = float.NegativeInfinity;
        foreach (Transform t in srcRt.parent)
        {
            if (t == srcRt || !(t is RectTransform rt)) continue;
            bool sameRow = rt.anchorMin == srcRt.anchorMin && rt.anchorMax == srcRt.anchorMax
                        && Mathf.Abs(rt.anchoredPosition.y - srcRt.anchoredPosition.y) < 1f;
            float x = rt.anchoredPosition.x;
            if (sameRow && x < srcRt.anchoredPosition.x && x > nearest) nearest = x;
        }
        if (!float.IsNegativeInfinity(nearest)) spacing = srcRt.anchoredPosition.x - nearest;

        GameObject go = Object.Instantiate(source.gameObject, srcRt.parent);
        go.name = name;
        go.transform.SetSiblingIndex(srcRt.GetSiblingIndex() + 1);
        ((RectTransform)go.transform).anchoredPosition = srcRt.anchoredPosition + new Vector2(spacing, 0f);

        Object.DestroyImmediate(go.GetComponent(source.GetType()));
        go.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();   // 씬에서 원래 창을 여는 OnClick 을 달아 뒀어도 따라오지 않게

        label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = text;
        return go;
    }

    // ═════════════════════════════ 우편함 ═════════════════════════════

    [MenuItem("Tools/UI 굽기/우편함 (StageScene)")]
    private static void BakeMailbox()
    {
        // 우편함 버튼은 친구 버튼(없으면 랭킹 버튼) 오른쪽에 — 둘 다 없으면 시작 전에 멈춤
        Component neighbor = FindInOpenScenes<OpenFriendsButton>();
        if (neighbor == null) neighbor = FindInOpenScenes<OpenRankingButton>();
        if (neighbor == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 OpenRankingButton 이 없습니다. StageScene 을 열고 다시 실행하세요.", "확인");
            return;
        }
        Scene scene = neighbor.gameObject.scene;

        MailboxPanel panel = FindInOpenScenes<MailboxPanel>();
        OpenMailboxButton oldButton = FindInOpenScenes<OpenMailboxButton>();
        GameObject oldRoot = panel != null ? new SerializedObject(panel).FindProperty("root").objectReferenceValue as GameObject : null;

        if (AlreadyExists(oldRoot != null ? oldRoot : oldButton != null ? oldButton.gameObject : null, "우편함")) return;

        // 컴포넌트를 둘 항상 켜진 빈 오브젝트 — 없으면 씬 최상위에 만듦
        if (panel == null)
        {
            var host = new GameObject("MailboxPanel");
            if (host.scene != scene) SceneManager.MoveGameObjectToScene(host, scene);
            Undo.RegisterCreatedObjectUndo(host, "우편함 굽기");
            panel = Undo.AddComponent<MailboxPanel>(host);
        }

        TMP_FontAsset font = panel.Font != null ? panel.Font : MostUsedFont(scene);
        var b = new Builder(font);

        // 캔버스 — 씬 최상위 (RankingPanel 클래스 주석의 이유)
        GameObject root = b.Canvas("MailboxCanvas", panel.SortingOrder, scene);

        RectTransform dim = b.Image("Dim", root.transform, RankDimColor, true);
        Builder.Stretch(dim);
        Button dimButton = dim.gameObject.AddComponent<Button>();

        RectTransform window = b.Image("Window", root.transform, RankWindowColor, true);
        window.sizeDelta = new Vector2(1100f, 900f);

        TextMeshProUGUI title = b.Text("Title", window, "우편함", 52f, TextAlignmentOptions.Center, false);
        Builder.PlaceTop(title.rectTransform, 120f, 120f, -20f, 80f);
        title.color = GoldColor;

        RectTransform closeRt = b.Image("Close", window, RankHeaderColor, true);
        closeRt.anchorMin = closeRt.anchorMax = closeRt.pivot = new Vector2(1f, 1f);
        closeRt.anchoredPosition = new Vector2(-20f, -20f);
        closeRt.sizeDelta = new Vector2(80f, 80f);
        Button closeButton = closeRt.gameObject.AddComponent<Button>();
        Builder.Stretch(b.Text("X", closeRt, "X", 44f, TextAlignmentOptions.Center, false).rectTransform);

        RectTransform content = VerticalScroll(b, window, "Viewport", "Content", 8f, out RectTransform viewport);
        viewport.offsetMin = new Vector2(40f, 140f);
        viewport.offsetMax = new Vector2(-40f, -120f);

        MailRow rowTemplate = MailRowObject(b, content);
        rowTemplate.gameObject.SetActive(false);

        TextMeshProUGUI status = b.Text("Status", viewport, string.Empty, 36f, TextAlignmentOptions.Center, false);
        Builder.Stretch(status.rectTransform);
        status.color = new Color(1f, 1f, 1f, 0.7f);

        // 모두 받기 — 아래 가운데
        Button claimAll = LabeledButton(b, window, "ClaimAll", "모두 받기", PopupCloudColor, 38f, out _);
        var caRt = (RectTransform)claimAll.transform;
        caRt.anchorMin = caRt.anchorMax = caRt.pivot = new Vector2(0.5f, 0f);
        caRt.anchoredPosition = new Vector2(0f, 30f);
        caRt.sizeDelta        = new Vector2(360f, 90f);

        root.SetActive(false);   // 평소엔 닫혀 있음 (MailboxPanel.Awake 도 끔)
        Undo.RegisterCreatedObjectUndo(root, "우편함 굽기");

        var so = new SerializedObject(panel);
        so.Update();
        so.FindProperty("root").objectReferenceValue           = root;
        so.FindProperty("dimButton").objectReferenceValue      = dimButton;
        so.FindProperty("closeButton").objectReferenceValue    = closeButton;
        so.FindProperty("content").objectReferenceValue        = content;
        so.FindProperty("rowTemplate").objectReferenceValue    = rowTemplate;
        so.FindProperty("statusText").objectReferenceValue     = status;
        so.FindProperty("claimAllButton").objectReferenceValue = claimAll;
        if (panel.Font == null) so.FindProperty("font").objectReferenceValue = font;
        so.ApplyModifiedProperties();

        // ── 우편함 버튼 ──
        GameObject button = CloneButtonRightOf(neighbor, "Open Mailbox Button", "우편", out TextMeshProUGUI label);
        var open = button.AddComponent<OpenMailboxButton>();
        var bso = new SerializedObject(open);
        bso.FindProperty("mailboxPanel").objectReferenceValue = panel;
        bso.FindProperty("label").objectReferenceValue        = label;
        bso.ApplyModifiedPropertiesWithoutUndo();   // 막 만든 오브젝트 — 아래 생성 Undo 에 함께 묶임
        Undo.RegisterCreatedObjectUndo(button, "우편함 버튼 굽기");

        Finish(scene, root, "우편함·우편함 버튼", SaveAsPrefab(root, "MailboxCanvas"));
    }

    /// <summary>우편 한 줄 — 제목 / 내용 / 보상 세 줄 + 오른쪽 [받기]</summary>
    private static MailRow MailRowObject(Builder b, Transform parent)
    {
        RectTransform row = b.Image("Row", parent, MailboxPanel.RowColor, false);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 150f;

        TextMeshProUGUI title  = MailLine(b, row, "Title",  "주간 랭킹 1위 보상", 36f, 0.64f, 1f);
        TextMeshProUGUI body   = MailLine(b, row, "Body",   "내용",               28f, 0.34f, 0.64f);
        TextMeshProUGUI reward = MailLine(b, row, "Reward", "보석 3,000",         30f, 0.04f, 0.34f);
        body.color   = new Color(1f, 1f, 1f, 0.7f);
        reward.color = MailboxPanel.RewardColor;

        Button claim = LabeledButton(b, row, "Claim", "받기", PopupCloudColor, 32f, out _);
        PlaceRight((RectTransform)claim.transform, -16f, 180f);
        var cRt = (RectTransform)claim.transform;
        cRt.offsetMin = new Vector2(cRt.offsetMin.x, 30f);
        cRt.offsetMax = new Vector2(cRt.offsetMax.x, -30f);

        var comp = row.gameObject.AddComponent<MailRow>();
        var so = new SerializedObject(comp);
        so.FindProperty("titleText").objectReferenceValue   = title;
        so.FindProperty("bodyText").objectReferenceValue    = body;
        so.FindProperty("rewardText").objectReferenceValue  = reward;
        so.FindProperty("claimButton").objectReferenceValue = claim;
        so.ApplyModifiedPropertiesWithoutUndo();   // 막 만든 오브젝트 — 생성 Undo 에 함께 묶임
        return comp;
    }

    private static TextMeshProUGUI MailLine(Builder b, RectTransform row, string name, string value, float size, float yMin, float yMax)
    {
        TextMeshProUGUI t = b.Text(name, row, value, size, TextAlignmentOptions.MidlineLeft, false);
        t.rectTransform.anchorMin = new Vector2(0f, yMin);
        t.rectTransform.anchorMax = new Vector2(1f, yMax);
        t.rectTransform.offsetMin = new Vector2(24f, 0f);
        t.rectTransform.offsetMax = new Vector2(-220f, 0f);
        return t;
    }

    /// <summary>친구 창 한 줄 — 닉네임 + 오른쪽 버튼 두 개</summary>
    private static FriendRow FriendRowObject(Builder b, Transform parent)
    {
        RectTransform row = b.Image("Row", parent, RankingPanel.RowColor, false);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 90f;

        TextMeshProUGUI name = b.Text("Name", row, "닉네임", 34f, TextAlignmentOptions.MidlineLeft, false);
        name.rectTransform.anchorMin = new Vector2(0f, 0f);
        name.rectTransform.anchorMax = new Vector2(1f, 1f);
        name.rectTransform.offsetMin = new Vector2(24f, 0f);
        name.rectTransform.offsetMax = new Vector2(-420f, 0f);

        Button primary   = LabeledButton(b, row, "Primary",   "수락", PopupCloudColor, 30f, out TextMeshProUGUI primaryLabel);
        Button secondary = LabeledButton(b, row, "Secondary", "거절", DangerColor,     30f, out TextMeshProUGUI secondaryLabel);
        PlaceRight((RectTransform)primary.transform,   -210f, 190f);
        PlaceRight((RectTransform)secondary.transform, -12f,  190f);

        var comp = row.gameObject.AddComponent<FriendRow>();
        var so = new SerializedObject(comp);
        so.FindProperty("nameText").objectReferenceValue        = name;
        so.FindProperty("primaryButton").objectReferenceValue   = primary;
        so.FindProperty("primaryLabel").objectReferenceValue    = primaryLabel;
        so.FindProperty("secondaryButton").objectReferenceValue = secondary;
        so.FindProperty("secondaryLabel").objectReferenceValue  = secondaryLabel;
        so.ApplyModifiedPropertiesWithoutUndo();   // 막 만든 오브젝트 — 생성 Undo 에 함께 묶임
        return comp;
    }

    /// <summary>줄 오른쪽 끝에서 right 만큼 떨어진 곳에 폭 width 버튼 (위아래 10 여백)</summary>
    private static void PlaceRight(RectTransform rt, float right, float width)
    {
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.offsetMin = new Vector2(right - width, 10f);
        rt.offsetMax = new Vector2(right, -10f);
    }

    /// <summary>배경 Image + Button + 가운데 글자</summary>
    private static Button LabeledButton(Builder b, Transform parent, string name, string text, Color color, float size, out TextMeshProUGUI label)
    {
        RectTransform rt = b.Image(name, parent, color, true);
        Button button = rt.gameObject.AddComponent<Button>();
        label = b.Text("Label", rt, text, size, TextAlignmentOptions.Center, false);
        Builder.Stretch(label.rectTransform);
        return button;
    }

    /// <summary>부모를 꽉 채우는 빈 RectTransform (창 묶음)</summary>
    private static RectTransform Container(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.gameObject.layer = parent.gameObject.layer;
        rt.SetParent(parent, false);
        Builder.Stretch(rt);
        return rt;
    }

    /// <summary>세로 스크롤 목록 (Viewport + RectMask2D + VerticalLayoutGroup 콘텐츠). 콘텐츠를 돌려주고, 뷰포트 위치는 호출한 쪽이 정함.</summary>
    private static RectTransform VerticalScroll(Builder b, Transform parent, string viewportName, string contentName, float spacing,
                                                out RectTransform viewport)
    {
        viewport = b.Image(viewportName, parent, Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;

        RectTransform content = Container(contentName, viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = spacing;
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
        return content;
    }

    // ── 설정 창 효과음·배경음 슬라이더 — 게임 씬마다 한 번, 블룸 슬라이더 줄을 복제해 창 왼쪽에 두 줄 (가운데 버튼 열과 안 겹치게 라벨 폭 250) ──

    private const float SoundLabelRight = 270f;   // 창 왼쪽 끝 → 라벨 오른쪽 끝 (라벨 피벗이 오른쪽 — 블룸 줄과 같음)
    private const float SoundLabelWidth = 250f;   // "효과음" (글자 50) 이 들어가는 폭
    private const float SoundRowOffsetY = 60f;    // 창 가운데에서 위(효과음) / 아래(배경음)

    [MenuItem("Tools/UI 굽기/설정 창 - 효과음·배경음 슬라이더 (게임 씬)")]
    private static void BakeSoundSliders()
    {
        GameSettingManager[] managers = Object.FindObjectsByType<GameSettingManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                              .Where(m => !EditorUtility.IsPersistent(m)).ToArray();
        if (managers.Length == 0)
        {
            EditorUtility.DisplayDialog("UI 굽기",
                "열린 씬에 GameSettingManager 가 없습니다. StageScene · DailyDungeonScene · EvolveScene 중 하나를 열고 다시 실행하세요.", "확인");
            return;
        }

        var lines = new System.Collections.Generic.List<string>();
        foreach (GameSettingManager manager in managers)
            lines.Add($"{manager.gameObject.scene.name}: {BakeSoundSlidersFor(manager)}");

        EditorUtility.DisplayDialog("UI 굽기",
            string.Join("\n", lines) +
            "\n\n씬을 저장하세요. 설정 창은 씬마다 따로 있으니 다른 게임 씬(StageScene · DailyDungeonScene · EvolveScene)에서도 한 번씩 실행하세요.",
            "확인");
    }

    /// <summary>한 설정 창에 두 줄을 만들고 GameSettingManager 칸을 채웁니다. 결과 한 줄.</summary>
    private static string BakeSoundSlidersFor(GameSettingManager manager)
    {
        var so = new SerializedObject(manager);
        if (so.FindProperty("sfxSlider").objectReferenceValue != null || so.FindProperty("bgmSlider").objectReferenceValue != null)
            return "이미 소리 슬라이더가 있어 건너뜀 (모양은 하이어라키에서 직접 고치세요)";

        var bloom = so.FindProperty("bloomSlider").objectReferenceValue as Slider;
        var row   = bloom != null ? bloom.transform.parent as RectTransform : null;   // "Bloom Slide" — 라벨 글자 + 슬라이더 자식
        var panel = row != null ? row.parent as RectTransform : null;                // "Setting Panel"
        if (panel == null)
            return "블룸 슬라이더(Bloom Slider 칸)가 연결돼 있지 않아 모양을 따라 만들 수 없어 건너뜀";

        TMP_Text template = row.GetComponent<TMP_Text>();
        var b = new Builder(template != null ? template.font : MostUsedFont(manager.gameObject.scene));

        Slider sfx = SoundRow(b, row, panel, "Sfx", "효과음",  SoundRowOffsetY, out TMP_Text sfxValue);
        Slider bgm = SoundRow(b, row, panel, "Bgm", "배경음", -SoundRowOffsetY, out TMP_Text bgmValue);

        so.Update();
        so.FindProperty("sfxSlider").objectReferenceValue     = sfx;
        so.FindProperty("sfxValueLabel").objectReferenceValue = sfxValue;
        so.FindProperty("bgmSlider").objectReferenceValue     = bgm;
        so.FindProperty("bgmValueLabel").objectReferenceValue = bgmValue;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Selection.activeGameObject = sfx.transform.parent.gameObject;
        EditorGUIUtility.PingObject(sfx);
        return "효과음 · 배경음 슬라이더를 설정 창 왼쪽에 만들고 인스펙터 칸을 채웠습니다";
    }

    /// <summary>블룸 줄(template)을 복제해 한 줄 — 라벨 + 슬라이더 + 오른쪽 퍼센트 글자.</summary>
    private static Slider SoundRow(Builder b, RectTransform template, RectTransform panel, string id, string title, float y,
                                   out TMP_Text valueLabel)
    {
        GameObject go = Object.Instantiate(template.gameObject, panel);
        go.name = $"{id} Volume";
        Undo.RegisterCreatedObjectUndo(go, "소리 슬라이더 굽기");

        // 창 왼쪽 가운데 기준으로 — 블룸 줄은 오른쪽 기준이라 앵커만 바꾸고 피벗(오른쪽)은 그대로
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.sizeDelta        = new Vector2(SoundLabelWidth, rt.sizeDelta.y);
        rt.anchoredPosition = new Vector2(SoundLabelRight, y);

        TMP_Text label = go.GetComponent<TMP_Text>();
        if (label != null) label.text = title;

        Slider slider = go.GetComponentInChildren<Slider>(true);
        slider.name           = $"{id} Slider";
        slider.onValueChanged = new Slider.SliderEvent();   // 블룸 슬라이더에 인스펙터로 건 연결이 있어도 따라오지 않게 (런타임에 SoundManager 가 검)
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(1f);

        // 퍼센트 — 슬라이더 오른쪽 (GameSettingManager 가 열 때 채움)
        float size = label != null ? label.fontSize * 0.8f : 40f;
        valueLabel = b.Text("Value", slider.transform, "100%", size, TextAlignmentOptions.MidlineLeft, false);
        if (label != null) valueLabel.color = label.color;
        RectTransform vrt = valueLabel.rectTransform;
        vrt.anchorMin = vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.pivot            = new Vector2(0f, 0.5f);
        vrt.sizeDelta        = new Vector2(110f, 50f);
        vrt.anchoredPosition = new Vector2(12f, 0f);
        return slider;
    }

    // ═════════════════════════════ 공용 ═════════════════════════════

    /// <summary>이미 있으면 안내하고 true(부른 쪽은 멈춤) — 손으로 고친 UI 를 지키려고 지우거나 덮지 않음.</summary>
    private static bool AlreadyExists(GameObject existing, string what)
    {
        if (existing == null) return false;

        string where = PrefabUtility.IsPartOfPrefabInstance(existing)
            ? $"프리팹 '{PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing)}' 을 열어"
            : "하이어라키에서 (또는 'Tools/UI 굽기/열린 씬의 창을 프리팹으로 저장' 으로 프리팹을 만든 뒤)";
        EditorUtility.DisplayDialog("UI 굽기",
            $"이미 {what}이(가) 있습니다 ({existing.name}).\n이 메뉴는 처음 한 번만 만듭니다. 모양은 {where} 직접 고치세요.", "확인");
        EditorGUIUtility.PingObject(existing);
        return true;
    }

    private const string PrefabFolder = "Assets/Prefabs/UI";

    /// <summary>씬 오브젝트를 프리팹으로 저장하고 인스턴스로 교체(결과 한 줄) — 창 밖을 가리키는 칸은 프리팹엔 빈칸, 씬 인스턴스엔 오버라이드로 남음.</summary>
    private static string SaveAsPrefab(GameObject root, string fileName)
    {
        if (root == null) return null;
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            return $"{root.name}: 이미 프리팹 ({PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root)})";

        EnsureFolder(PrefabFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/{fileName}.prefab");
        GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction, out bool ok);
        return ok && asset != null ? $"{root.name} → {path}" : $"{root.name}: 프리팹 저장 실패 (콘솔 확인)";
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];   // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    /// <summary>열린 씬에 구워 둔 창들을 프리팹으로 (이미 프리팹이면 건너뜀, 검색 칸이 없으면 먼저 더함) — StageScene·LoginScene 에서 한 번씩 실행 후 저장.</summary>
    [MenuItem("Tools/UI 굽기/열린 씬의 창을 프리팹으로 저장")]
    private static void SaveAllAsPrefabs()
    {
        var lines = new System.Collections.Generic.List<string>();
        var dirty = new System.Collections.Generic.HashSet<Scene>();

        void Save(Component owner, GameObject root, string fileName)
        {
            if (owner == null || root == null) return;
            string line = SaveAsPrefab(root, fileName);
            if (line != null) lines.Add(line);
            dirty.Add(root.scene);
        }

        RankingPanel ranking = FindInOpenScenes<RankingPanel>();
        if (ranking != null) Save(ranking, new SerializedObject(ranking).FindProperty("root").objectReferenceValue as GameObject, "RankingCanvas");

        MailboxPanel mailbox = FindInOpenScenes<MailboxPanel>();
        if (mailbox != null) Save(mailbox, new SerializedObject(mailbox).FindProperty("root").objectReferenceValue as GameObject, "MailboxCanvas");

        ChatPanel chat = FindInOpenScenes<ChatPanel>();
        if (chat != null)
        {
            FriendPanel friends = chat.GetComponent<FriendPanel>();
            if (friends != null && !PrefabUtility.IsPartOfPrefabInstance(chat.gameObject)
                && TryAddFriendSearch(friends, out _, out _))
                lines.Add("친구 창에 닉네임 검색 칸을 더했습니다.");
            Save(chat, chat.gameObject, "ChatCanvas");
        }

        CloudSaveConflictPopup popup = FindInOpenScenes<CloudSaveConflictPopup>();
        if (popup != null) Save(popup, popup.gameObject, "CloudSaveConflictCanvas");

        foreach (Scene s in dirty) EditorSceneManager.MarkSceneDirty(s);

        EditorUtility.DisplayDialog("UI 굽기",
            lines.Count == 0
                ? "열린 씬에서 프리팹으로 바꿀 창을 찾지 못했습니다. StageScene(또는 LoginScene)을 열고 다시 실행하세요."
                : string.Join("\n", lines) + "\n\n씬을 저장하세요. 이제부터는 프리팹을 열어(또는 씬에서 고친 뒤 Overrides > Apply) 모양을 고치면 됩니다.",
            "확인");
    }

    private static T FindInOpenScenes<T>() where T : Component
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                 .FirstOrDefault(c => !EditorUtility.IsPersistent(c));   // 프리팹 에셋 말고 씬에 있는 것만

    /// <summary>폰트를 정하지 않았을 때 — 그 씬의 글자들이 가장 많이 쓰는 TMP 폰트 (한글 폰트일 가능성이 가장 큼)</summary>
    private static TMP_FontAsset MostUsedFont(Scene scene)
    {
        TMP_FontAsset font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && t.font != null)
            .GroupBy(t => t.font)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        if (font == null)
            Debug.LogWarning("[UIBaker] 씬에서 TMP 폰트를 찾지 못해 기본 폰트로 만듭니다. 한글이 □ 로 보이면 글자들의 폰트를 바꾸세요.");
        return font;
    }

    private static void Finish(Scene scene, GameObject root, string what, string prefabNote = null)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        string note = string.IsNullOrEmpty(prefabNote) ? string.Empty : $" 프리팹: {prefabNote}.";
        Debug.Log($"[UIBaker] {what}을(를) '{scene.name}' 에 만들고 인스펙터 칸을 채웠습니다.{note} 씬을 저장하세요. " +
                  "이후 모양은 프리팹/하이어라키에서 직접 고치세요 (이 메뉴는 다시 만들지 않습니다).", root);
    }

    /// <summary>UI 오브젝트를 만드는 도우미 (폰트를 한 번만 넘기려고 인스턴스로 둠)</summary>
    private class Builder
    {
        private readonly TMP_FontAsset font;
        public Builder(TMP_FontAsset font) { this.font = font; }

        /// <summary>1920x1080 기준 오버레이 캔버스를 그 씬의 최상위에 만듭니다.</summary>
        public GameObject Canvas(string name, int sortingOrder, Scene scene)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            go.layer = LayerMask.NameToLayer("UI");

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;
            return go;
        }

        public RectTransform Image(string name, Transform parent, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color         = color;
            img.raycastTarget = raycast;
            return (RectTransform)go.transform;
        }

        /// <summary>wrap = true 면 줄바꿈 (팝업 문구), false 면 한 줄 + 말줄임 (랭킹 칸)</summary>
        public TextMeshProUGUI Text(string name, Transform parent, string value, float size, TextAlignmentOptions align, bool wrap)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.richText         = true;
            t.text             = value;
            t.fontSize         = size;
            t.alignment        = align;
            t.raycastTarget    = false;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode     = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            return t;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>부모 위쪽에 가로로 꽉 차게 배치 (left/right 여백, 위에서 top 만큼, 높이 height). top 은 음수.</summary>
        public static void PlaceTop(RectTransform rt, float left, float right, float top, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, top - height);
            rt.offsetMax = new Vector2(-right, top);
        }
    }
}

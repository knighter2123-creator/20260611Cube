using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>길드 창 UI 를 '한 번' 만들어 Assets/Prefabs/UI/GuildCanvas 프리팹으로 저장하고 GuildPanel · 길드 버튼 칸을 채움.</summary>
// 메뉴: Tools/UI 굽기/길드 창 (StageScene) → 씬 저장. 다 만든 뒤 이 파일은 지워도 됨 (런타임 코드는 이 파일을 쓰지 않음)
public static class GuildUIBaker
{
    private static readonly Color DimColor    = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color WindowColor = new Color(0.12f, 0.13f, 0.18f, 0.97f);
    private static readonly Color HeaderColor = new Color(0.22f, 0.24f, 0.32f, 1f);
    private static readonly Color BoxColor    = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color GreenColor  = new Color(0.30f, 0.75f, 0.45f, 1f);
    private static readonly Color BlueColor   = new Color(0.30f, 0.55f, 0.95f, 1f);
    private static readonly Color RedColor    = new Color(0.75f, 0.28f, 0.28f, 1f);
    private static readonly Color GoldColor   = new Color(1f, 0.85f, 0.2f);
    private static readonly Color DimText     = new Color(1f, 1f, 1f, 0.6f);

    private const string PrefabFolder = "Assets/Prefabs/UI";

    [MenuItem("Tools/UI 굽기/길드 창 (StageScene)")]
    private static void Bake()
    {
        // 길드 버튼은 공지 → 우편 → 친구 → 랭킹 버튼 중 있는 것의 오른쪽에
        Component neighbor = Find<OpenNoticeButton>();
        if (neighbor == null) neighbor = Find<OpenMailboxButton>();
        if (neighbor == null) neighbor = Find<OpenFriendsButton>();
        if (neighbor == null) neighbor = Find<OpenRankingButton>();
        if (neighbor == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 공지·우편·친구·랭킹 버튼이 없습니다. StageScene 을 열고 다시 실행하세요.", "확인");
            return;
        }
        Scene scene = neighbor.gameObject.scene;

        GuildPanel panel = Find<GuildPanel>();
        GameObject oldRoot = panel != null ? new SerializedObject(panel).FindProperty("root").objectReferenceValue as GameObject : null;
        OpenGuildButton oldButton = Find<OpenGuildButton>();
        GameObject existing = oldRoot != null ? oldRoot : oldButton != null ? oldButton.gameObject : null;
        if (existing != null)
        {
            EditorUtility.DisplayDialog("UI 굽기", $"이미 길드 창이 있습니다 ({existing.name}). 모양은 프리팹에서 직접 고치세요.", "확인");
            EditorGUIUtility.PingObject(existing);
            return;
        }

        if (panel == null)
        {
            var host = new GameObject("GuildPanel");
            if (host.scene != scene) SceneManager.MoveGameObjectToScene(host, scene);
            Undo.RegisterCreatedObjectUndo(host, "길드 창 굽기");
            panel = Undo.AddComponent<GuildPanel>(host);
        }

        TMP_FontAsset font = panel.Font != null ? panel.Font : MostUsedFont(scene);
        var b = new Builder(font);
        var refs = new SerializedObject(panel);
        void Set(string field, Object value) => refs.FindProperty(field).objectReferenceValue = value;

        // ── 창 ──
        GameObject root = b.Canvas("GuildCanvas", panel.SortingOrder, scene);
        RectTransform dim = b.Image("Dim", root.transform, DimColor, true);
        Stretch(dim);
        Set("dimButton", dim.gameObject.AddComponent<Button>());

        RectTransform window = b.Image("Window", root.transform, WindowColor, true);
        window.sizeDelta = new Vector2(1300f, 950f);

        TextMeshProUGUI title = b.Text("Title", window, "길드", 52f, TextAlignmentOptions.Center, false);
        PlaceTop(title.rectTransform, 120f, 120f, -20f, 80f);
        title.color = GoldColor;
        Set("titleText", title);

        RectTransform close = b.Image("Close", window, HeaderColor, true);
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-20f, -20f);
        close.sizeDelta = new Vector2(80f, 80f);
        Set("closeButton", close.gameObject.AddComponent<Button>());
        Stretch(b.Text("X", close, "X", 44f, TextAlignmentOptions.Center, false).rectTransform);

        // ── 길드 없음: 검색 + 목록 ──
        RectTransform noGuild = Container("NoGuildView", window);
        Set("noGuildView", noGuild.gameObject);

        TMP_InputField search = InputBox(b, noGuild, "Search", "길드 이름으로 찾기", 32f, 20);
        PlaceTop((RectTransform)search.transform, 40f, 480f, -120f, 80f);
        Set("searchInput", search);
        Set("searchButton",  TopButton(b, noGuild, "SearchButton", "검색", BlueColor, -260f, 200f, -120f));
        Set("showAllButton", TopButton(b, noGuild, "ShowAll", "전체 보기", HeaderColor, -40f, 200f, -120f));

        RectTransform browse = Scroll(b, noGuild, "Viewport", new Vector2(40f, 140f), new Vector2(-40f, -220f), out RectTransform browseViewport);
        Set("browseContent", browse);
        Set("browseStatus",  Status(b, browseViewport));

        Set("moreButton",       BottomButton(b, noGuild, "More", "더 보기", HeaderColor, 0f, 40f, 260f));
        Set("openCreateButton", BottomButton(b, noGuild, "OpenCreate", "길드 만들기", GreenColor, 1f, -40f, 360f));

        // ── 길드 만들기 ──
        RectTransform create = Container("CreateView", window);
        Set("createView", create.gameObject);

        Label(b, create, "NameLabel", "길드 이름", -130f);
        TMP_InputField nameInput = InputBox(b, create, "NameInput", "길드 이름", 34f, 12);
        PlaceTop((RectTransform)nameInput.transform, 120f, 120f, -185f, 84f);
        Set("createNameInput", nameInput);

        Label(b, create, "IntroLabel", "길드 소개 (선택)", -300f);
        TMP_InputField introInput = InputBox(b, create, "IntroInput", "어떤 길드인지 적어 주세요", 30f, 60);
        PlaceTop((RectTransform)introInput.transform, 120f, 120f, -355f, 84f);
        Set("createIntroInput", introInput);

        TextMeshProUGUI cost = b.Text("Cost", create, "필요한 보석: 500", 34f, TextAlignmentOptions.Center, false);
        PlaceTop(cost.rectTransform, 120f, 120f, -480f, 60f);
        cost.color = GoldColor;
        Set("createCostText", cost);

        Set("createCancelButton",  BottomButton(b, create, "Cancel", "취소", HeaderColor, 0.5f, -200f, 360f));
        Set("createConfirmButton", BottomButton(b, create, "Confirm", "만들기", GreenColor, 0.5f, 200f, 360f));

        // ── 내 길드 ──
        RectTransform guild = Container("GuildView", window);
        Set("guildView", guild.gameObject);

        TextMeshProUGUI info = b.Text("Info", guild, "길드장 | 멤버 0명", 30f, TextAlignmentOptions.Center, false);
        PlaceTop(info.rectTransform, 120f, 120f, -100f, 44f);
        info.color = DimText;
        Set("guildInfoText", info);

        RectTransform introBox = b.Image("IntroBox", guild, BoxColor, false);
        PlaceTop(introBox, 40f, 40f, -155f, 90f);
        TextMeshProUGUI intro = b.Text("Intro", introBox, "길드 소개", 28f, TextAlignmentOptions.MidlineLeft, true);
        Stretch(intro.rectTransform);
        intro.rectTransform.offsetMin = new Vector2(20f, 6f);
        intro.rectTransform.offsetMax = new Vector2(-20f, -6f);
        intro.overflowMode = TextOverflowModes.Ellipsis;
        Set("guildIntroText", intro);

        Button membersTab = LabeledButton(b, guild, "MembersTab", "멤버", HeaderColor, 32f, out _);
        PlaceTopLeft((RectTransform)membersTab.transform, 40f, -262f, 240f, 70f);
        Set("membersTab", membersTab);
        Button applicantsTab = LabeledButton(b, guild, "ApplicantsTab", "가입 신청", HeaderColor, 32f, out TextMeshProUGUI applicantsLabel);
        PlaceTopLeft((RectTransform)applicantsTab.transform, 292f, -262f, 300f, 70f);
        Set("applicantsTab", applicantsTab);
        Set("applicantsTabLabel", applicantsLabel);

        RectTransform members = Scroll(b, guild, "Viewport", new Vector2(40f, 140f), new Vector2(-40f, -345f), out RectTransform memberViewport);
        Set("memberContent", members);
        Set("memberStatus",  Status(b, memberViewport));

        Set("editIntroButton", BottomButton(b, guild, "EditIntro", "소개 수정", HeaderColor, 0f, 40f, 260f));
        Button auto = BottomButton(b, guild, "AutoApprove", "자동 가입: 꺼짐", HeaderColor, 0f, 320f, 360f);
        Set("autoApproveButton", auto);
        Set("autoApproveLabel",  auto.GetComponentInChildren<TextMeshProUGUI>(true));
        Set("leaveButton",       BottomButton(b, guild, "Leave", "길드 탈퇴", RedColor, 1f, -40f, 260f));

        // ── 목록 한 줄 템플릿 (목록·멤버·신청 공용) — 길드 없음 목록 안에 둠 ──
        GuildRow row = RowObject(b, browse);
        row.gameObject.SetActive(false);
        Set("rowTemplate", row);

        // ── 결과 한 줄 ──
        TextMeshProUGUI feedback = b.Text("Feedback", window, string.Empty, 30f, TextAlignmentOptions.Center, true);
        feedback.rectTransform.anchorMin = new Vector2(0f, 0f);
        feedback.rectTransform.anchorMax = new Vector2(1f, 0f);
        feedback.rectTransform.pivot     = new Vector2(0.5f, 0f);
        feedback.rectTransform.offsetMin = new Vector2(60f, 125f);
        feedback.rectTransform.offsetMax = new Vector2(-60f, 175f);
        feedback.color = GoldColor;
        feedback.gameObject.SetActive(false);
        Set("feedbackText", feedback);

        // ── 확인 · 입력 창 (창 전체를 덮음) ──
        RectTransform dialog = b.Image("Dialog", window, new Color(0f, 0f, 0f, 0.55f), true);
        Stretch(dialog);
        RectTransform box = b.Image("Box", dialog, HeaderColor, true);
        box.sizeDelta = new Vector2(820f, 440f);

        TextMeshProUGUI message = b.Text("Message", box, "정말 할까요?", 34f, TextAlignmentOptions.Center, true);
        PlaceTop(message.rectTransform, 40f, 40f, -30f, 150f);
        Set("dialogMessage", message);

        TMP_InputField dialogInput = InputBox(b, box, "Input", "입력", 30f, 60);
        PlaceTop((RectTransform)dialogInput.transform, 40f, 40f, -195f, 84f);
        Set("dialogInput", dialogInput);

        Set("dialogCancel", BottomButton(b, box, "Cancel", "취소", WindowColor, 0.5f, -190f, 340f));
        Set("dialogOk",     BottomButton(b, box, "Ok", "확인", GreenColor, 0.5f, 190f, 340f));
        dialog.gameObject.SetActive(false);
        Set("dialog", dialog.gameObject);

        create.gameObject.SetActive(false);
        guild.gameObject.SetActive(false);
        root.SetActive(false);   // 평소엔 닫혀 있음
        Undo.RegisterCreatedObjectUndo(root, "길드 창 굽기");

        Set("root", root);
        if (panel.Font == null) Set("font", font);
        refs.ApplyModifiedProperties();

        // ── 길드 버튼 ──
        GameObject button = CloneButtonRightOf(neighbor, "Open Guild Button", "길드", out TextMeshProUGUI label);
        var open = button.AddComponent<OpenGuildButton>();
        var bso = new SerializedObject(open);
        bso.FindProperty("guildPanel").objectReferenceValue = panel;
        bso.FindProperty("label").objectReferenceValue      = label;
        bso.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(button, "길드 버튼 굽기");

        string prefabNote = SaveAsPrefab(root, "GuildCanvas");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[GuildUIBaker] 길드 창·길드 버튼을 '{scene.name}' 에 만들었습니다. 프리팹: {prefabNote}. 씬을 저장하세요. " +
                  "이후 모양은 프리팹에서 직접 고치고, 이 굽기 스크립트(Assets/Scripts/Editor/GuildUIBaker.cs)는 지워도 됩니다.", root);
    }

    // ───────────────────────────── 부품 ─────────────────────────────

    /// <summary>한 줄 — 위 제목 / 아래 설명 + 오른쪽 버튼 3개 (0 = 맨 오른쪽)</summary>
    private static GuildRow RowObject(Builder b, Transform parent)
    {
        RectTransform row = b.Image("Row", parent, GuildPanel.RowColor, false);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;

        const float buttonWidth = 190f, gap = 10f, right = 12f;
        float textRight = right + 3f * (buttonWidth + gap) + 10f;

        TextMeshProUGUI title = b.Text("Title", row, "이름", 34f, TextAlignmentOptions.MidlineLeft, false);
        title.rectTransform.anchorMin = new Vector2(0f, 0.48f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(24f, 0f);
        title.rectTransform.offsetMax = new Vector2(-textRight, -6f);

        TextMeshProUGUI sub = b.Text("Sub", row, "설명", 26f, TextAlignmentOptions.MidlineLeft, false);
        sub.rectTransform.anchorMin = new Vector2(0f, 0f);
        sub.rectTransform.anchorMax = new Vector2(1f, 0.48f);
        sub.rectTransform.offsetMin = new Vector2(24f, 6f);
        sub.rectTransform.offsetMax = new Vector2(-textRight, 0f);
        sub.color = DimText;

        Color[] colors = { GreenColor, BlueColor, RedColor };
        var buttons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            Button button = LabeledButton(b, row, "Button" + i, "버튼", colors[i], 28f, out _);
            var rt = (RectTransform)button.transform;
            float r = right + i * (buttonWidth + gap);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-r - buttonWidth, 20f);
            rt.offsetMax = new Vector2(-r, -20f);
            buttons[i] = button;
        }

        var comp = row.gameObject.AddComponent<GuildRow>();
        var so = new SerializedObject(comp);
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subText").objectReferenceValue   = sub;
        SerializedProperty list = so.FindProperty("buttons");
        list.arraySize = buttons.Length;
        for (int i = 0; i < buttons.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    private static Button TopButton(Builder b, Transform parent, string name, string text, Color color, float right, float width, float top)
    {
        Button button = LabeledButton(b, parent, name, text, color, 32f, out _);
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(right, top);
        rt.sizeDelta = new Vector2(width, 80f);
        return button;
    }

    /// <summary>아래쪽 버튼. anchorX 0 = 왼쪽 끝 기준, 0.5 = 가운데, 1 = 오른쪽 끝 기준</summary>
    private static Button BottomButton(Builder b, Transform parent, string name, string text, Color color, float anchorX, float x, float width)
    {
        Button button = LabeledButton(b, parent, name, text, color, 34f, out _);
        var rt = (RectTransform)button.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(anchorX, 0f);
        rt.anchoredPosition = new Vector2(x, 30f);
        rt.sizeDelta = new Vector2(width, 90f);
        return button;
    }

    private static void Label(Builder b, Transform parent, string name, string text, float top)
    {
        TextMeshProUGUI t = b.Text(name, parent, text, 30f, TextAlignmentOptions.MidlineLeft, false);
        PlaceTop(t.rectTransform, 120f, 120f, top, 50f);
        t.color = DimText;
    }

    private static TextMeshProUGUI Status(Builder b, RectTransform viewport)
    {
        TextMeshProUGUI status = b.Text("Status", viewport, string.Empty, 34f, TextAlignmentOptions.Center, true);
        Stretch(status.rectTransform);
        status.color = new Color(1f, 1f, 1f, 0.7f);
        return status;
    }

    private static Button LabeledButton(Builder b, Transform parent, string name, string text, Color color, float size, out TextMeshProUGUI label)
    {
        RectTransform rt = b.Image(name, parent, color, true);
        Button button = rt.gameObject.AddComponent<Button>();
        label = b.Text("Label", rt, text, size, TextAlignmentOptions.Center, false);
        Stretch(label.rectTransform);
        return button;
    }

    private static TMP_InputField InputBox(Builder b, Transform parent, string name, string placeholder, float size, int limit)
    {
        RectTransform bg = b.Image(name, parent, new Color(1f, 1f, 1f, 0.08f), true);

        RectTransform area = Container("Text Area", bg);
        area.offsetMin = new Vector2(20f, 6f);
        area.offsetMax = new Vector2(-20f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI hint = b.Text("Placeholder", area, placeholder, size, TextAlignmentOptions.MidlineLeft, false);
        Stretch(hint.rectTransform);
        hint.color = new Color(1f, 1f, 1f, 0.4f);

        TextMeshProUGUI text = b.Text("Text", area, string.Empty, size, TextAlignmentOptions.MidlineLeft, false);
        Stretch(text.rectTransform);
        text.richText = false;

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

    /// <summary>세로 스크롤 목록 — 뷰포트 여백(offsetMin/Max)을 정하고 콘텐츠를 돌려줌</summary>
    private static RectTransform Scroll(Builder b, Transform parent, string name, Vector2 offsetMin, Vector2 offsetMax, out RectTransform viewport)
    {
        viewport = b.Image(name, parent, Color.clear, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = offsetMin;
        viewport.offsetMax = offsetMax;

        RectTransform content = Container("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing                = 8f;
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

    private static RectTransform Container(string name, Transform parent)
    {
        var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rt.gameObject.layer = parent.gameObject.layer;
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// <summary>부모 위쪽에 가로로 꽉 차게 (left/right 여백, 위에서 top(음수) 만큼, 높이 height)</summary>
    private static void PlaceTop(RectTransform rt, float left, float right, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, top - height);
        rt.offsetMax = new Vector2(-right, top);
    }

    private static void PlaceTopLeft(RectTransform rt, float left, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, top);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>버튼을 복제해 오른쪽에 둠 (간격 = 왼쪽 이웃과의 간격, 없으면 폭 + 16) — 복제본의 원래 컴포넌트·OnClick 은 지움</summary>
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
        go.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();

        label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = text;
        return go;
    }

    private static string SaveAsPrefab(GameObject root, string fileName)
    {
        string[] parts = PrefabFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/{fileName}.prefab");
        GameObject asset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.UserAction, out bool ok);
        return ok && asset != null ? path : "저장 실패 (콘솔 확인)";
    }

    private static T Find<T>() where T : Component
        => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                 .FirstOrDefault(c => !EditorUtility.IsPersistent(c));

    private static TMP_FontAsset MostUsedFont(Scene scene)
    {
        TMP_FontAsset font = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && t.font != null)
            .GroupBy(t => t.font)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        if (font == null) Debug.LogWarning("[GuildUIBaker] 씬에서 TMP 폰트를 찾지 못해 기본 폰트로 만듭니다.");
        return font;
    }

    private class Builder
    {
        private readonly TMP_FontAsset font;
        public Builder(TMP_FontAsset font) { this.font = font; }

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
    }
}

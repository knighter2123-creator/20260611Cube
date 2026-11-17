using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>인벤토리 UI 일회용 굽기 (다 쓰면 지워도 됨).</summary>
public static class InventoryUIBaker
{
    private static readonly Color DimColor    = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color WindowColor = new Color(0.12f, 0.13f, 0.18f, 0.97f);
    private static readonly Color HeaderColor = new Color(0.22f, 0.24f, 0.32f, 1f);
    private static readonly Color BoxColor    = new Color(1f, 1f, 1f, 0.06f);
    private static readonly Color SlotColor   = new Color(0.18f, 0.19f, 0.25f, 1f);
    private static readonly Color FrameColor  = new Color(0.45f, 0.47f, 0.55f, 1f);
    private static readonly Color GreenColor  = new Color(0.30f, 0.75f, 0.45f, 1f);
    private static readonly Color BlueColor   = new Color(0.30f, 0.55f, 0.95f, 1f);
    private static readonly Color GoldColor   = new Color(1f, 0.85f, 0.2f);
    private static readonly Color DimText     = new Color(1f, 1f, 1f, 0.6f);

    private const string PrefabFolder = "Assets/Prefabs/UI";
    private const int    SortingOrder = 40;

    // ───────────────────────────── ① 인벤토리 창 ─────────────────────────────

    [MenuItem("Tools/UI 굽기/인벤토리 창")]
    private static void BakeInventory()
    {
        InventoryPanel existing = Find<InventoryPanel>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("UI 굽기", $"이미 인벤토리 창이 있습니다 ({existing.name}). 모양은 프리팹에서 직접 고치세요.", "확인");
            EditorGUIUtility.PingObject(existing);
            return;
        }

        // 열기 버튼은 길드·공지·우편·친구·랭킹 버튼 중 있는 것의 오른쪽에
        Component neighbor = Find<OpenGuildButton>();
        if (neighbor == null) neighbor = Find<OpenNoticeButton>();
        if (neighbor == null) neighbor = Find<OpenMailboxButton>();
        if (neighbor == null) neighbor = Find<OpenFriendsButton>();
        if (neighbor == null) neighbor = Find<OpenRankingButton>();
        if (neighbor == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 길드·공지·우편·친구·랭킹 버튼이 없습니다. StageScene 을 열고 다시 실행하세요.", "확인");
            return;
        }
        Scene scene = neighbor.gameObject.scene;

        var b = new Builder(MostUsedFont(scene));

        // ── 슬롯 프리팹 (격자 한 칸) ──
        InventorySlot slotPrefab = BakeSlotPrefab(b, scene);
        if (slotPrefab == null) return;

        // ── 창 ──
        GameObject root = b.Canvas("InventoryCanvas", SortingOrder, scene);
        var panel = root.AddComponent<InventoryPanel>();
        var refs  = new SerializedObject(panel);
        void Set(string field, Object value) => refs.FindProperty(field).objectReferenceValue = value;

        RectTransform dim = b.Image("Dim", root.transform, DimColor, true);
        Stretch(dim);
        Set("dimButton", dim.gameObject.AddComponent<Button>());

        RectTransform window = b.Image("Window", root.transform, WindowColor, true);
        window.sizeDelta = new Vector2(1400f, 950f);

        TextMeshProUGUI title = b.Text("Title", window, "인벤토리", 52f, TextAlignmentOptions.Center, false);
        PlaceTop(title.rectTransform, 120f, 120f, -20f, 80f);
        title.color = GoldColor;

        Set("closeButton", CloseButton(b, window));

        // 분류 탭: 전체 / 조각 / 소환권
        string[] tabNames = { "전체", "조각", "소환권" };
        string[] tabTypes = { "", ItemTypes.Fragment, ItemTypes.GachaTicket };
        SerializedProperty filters = refs.FindProperty("filters");
        filters.arraySize = tabNames.Length;
        for (int i = 0; i < tabNames.Length; i++)
        {
            Button tab = LabeledButton(b, window, "Tab_" + tabTypes[i], tabNames[i], HeaderColor, 32f, out _);
            PlaceTopLeft((RectTransform)tab.transform, 40f + i * 220f, -120f, 200f, 72f);

            RectTransform mark = b.Image("Selected", tab.transform, BlueColor, false);
            mark.anchorMin = new Vector2(0f, 0f);
            mark.anchorMax = new Vector2(1f, 0f);
            mark.pivot     = new Vector2(0.5f, 0f);
            mark.sizeDelta = new Vector2(0f, 8f);

            SerializedProperty e = filters.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("itemType").stringValue                = tabTypes[i];
            e.FindPropertyRelative("button").objectReferenceValue         = tab;
            e.FindPropertyRelative("selectedMark").objectReferenceValue   = mark.gameObject;
        }

        TextMeshProUGUI summary = b.Text("Summary", window, "아이템 0종", 30f, TextAlignmentOptions.MidlineRight, false);
        PlaceTop(summary.rectTransform, 700f, 40f, -120f, 72f);
        summary.color = DimText;
        Set("summaryText", summary);

        // 격자
        RectTransform viewport = b.Image("Viewport", window, BoxColor, true);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(40f, 40f);
        viewport.offsetMax = new Vector2(-40f, -215f);

        RectTransform content = Container("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot     = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding         = new RectOffset(20, 20, 20, 20);
        grid.cellSize        = new Vector2(180f, 220f);
        grid.spacing         = new Vector2(16f, 16f);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 6;
        grid.childAlignment  = TextAnchor.UpperCenter;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport          = viewport;
        scroll.content           = content;
        scroll.horizontal        = false;
        scroll.movementType      = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        Set("gridContent", content);
        Set("slotPrefab",  slotPrefab);

        TextMeshProUGUI empty = b.Text("Empty", viewport, "가진 아이템이 없습니다.", 36f, TextAlignmentOptions.Center, true);
        Stretch(empty.rectTransform);
        empty.color = DimText;
        Set("emptyMark", empty.gameObject);

        // ── 상세창 (창 안을 덮음) ──
        Set("detail", BakeDetail(b, window));

        root.SetActive(false);   // 평소엔 닫혀 있음
        refs.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(root, "인벤토리 창 굽기");

        // ── 열기 버튼 ──
        GameObject button = CloneButtonRightOf(neighbor, "Open Inventory Button", "가방");
        var open = button.AddComponent<OpenInventoryButton>();
        var bso  = new SerializedObject(open);
        bso.FindProperty("inventoryPanel").objectReferenceValue = panel;
        bso.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(button, "인벤토리 버튼 굽기");

        string prefabNote = SaveAsPrefab(root, "InventoryCanvas");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[InventoryUIBaker] 인벤토리 창·가방 버튼을 '{scene.name}' 에 만들었습니다. 프리팹: {prefabNote}. 씬을 저장하세요. " +
                  "다른 씬에도 필요하면 InventoryCanvas 프리팹을 놓고 버튼에 OpenInventoryButton 을 붙이세요.", root);
    }

    private static InventorySlot BakeSlotPrefab(Builder b, Scene scene)
    {
        var temp = new GameObject("InventorySlot", typeof(RectTransform), typeof(Image), typeof(Button));
        if (temp.scene != scene) SceneManager.MoveGameObjectToScene(temp, scene);
        temp.layer = LayerMask.NameToLayer("UI");
        ((RectTransform)temp.transform).sizeDelta = new Vector2(180f, 220f);
        Image frame = temp.GetComponent<Image>();
        frame.color = FrameColor;

        RectTransform inner = b.Image("Inner", temp.transform, SlotColor, false);
        Stretch(inner);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);

        RectTransform icon = b.Image("Icon", inner, Color.white, false);
        PlaceTop(icon, 20f, 20f, -12f, 130f);
        icon.GetComponent<Image>().preserveAspect = true;

        TextMeshProUGUI count = b.Text("Count", inner, "x0", 26f, TextAlignmentOptions.BottomRight, false);
        PlaceTop(count.rectTransform, 10f, 12f, -110f, 36f);
        count.outlineWidth = 0.2f;
        count.outlineColor = Color.black;

        TextMeshProUGUI name = b.Text("Name", inner, "아이템", 22f, TextAlignmentOptions.Center, false);
        PlaceTop(name.rectTransform, 8f, 8f, -150f, 52f);
        name.enableAutoSizing = true;
        name.fontSizeMin      = 14f;
        name.fontSizeMax      = 22f;

        var slot = temp.AddComponent<InventorySlot>();
        var so   = new SerializedObject(slot);
        so.FindProperty("iconImage").objectReferenceValue  = icon.GetComponent<Image>();
        so.FindProperty("gradeFrame").objectReferenceValue = frame;
        so.FindProperty("defaultFrameColor").colorValue    = FrameColor;
        so.FindProperty("nameText").objectReferenceValue   = name;
        so.FindProperty("countText").objectReferenceValue  = count;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/InventorySlot.prefab");
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(temp, path, out bool ok);
        Object.DestroyImmediate(temp);
        if (!ok || asset == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "슬롯 프리팹 저장에 실패했습니다. 콘솔을 확인하세요.", "확인");
            return null;
        }
        return asset.GetComponent<InventorySlot>();
    }

    private static InventoryItemDetail BakeDetail(Builder b, RectTransform window)
    {
        RectTransform root = b.Image("DetailPanel", window, DimColor, true);
        Stretch(root);
        var detail = root.gameObject.AddComponent<InventoryItemDetail>();
        var so = new SerializedObject(detail);
        void Set(string field, Object value) => so.FindProperty(field).objectReferenceValue = value;

        Set("dimmedButton", root.gameObject.AddComponent<Button>());

        RectTransform box = b.Image("Box", root, WindowColor, true);
        box.sizeDelta = new Vector2(1000f, 760f);
        Set("closeButton", CloseButton(b, box));

        // 왼쪽 위: 아이콘 (등급 테두리)
        RectTransform frame = b.Image("Frame", box, FrameColor, false);
        PlaceTopLeft(frame, 40f, -40f, 220f, 220f);
        Set("gradeFrame", frame.GetComponent<Image>());
        so.FindProperty("defaultFrameColor").colorValue = FrameColor;
        RectTransform iconBg = b.Image("Inner", frame, SlotColor, false);
        Stretch(iconBg);
        iconBg.offsetMin = new Vector2(6f, 6f);
        iconBg.offsetMax = new Vector2(-6f, -6f);
        RectTransform icon = b.Image("Icon", iconBg, Color.white, false);
        Stretch(icon);
        icon.offsetMin = new Vector2(14f, 14f);
        icon.offsetMax = new Vector2(-14f, -14f);
        icon.GetComponent<Image>().preserveAspect = true;
        Set("iconImage", icon.GetComponent<Image>());

        // 오른쪽: 이름 / 수량 / 설명
        TextMeshProUGUI name = b.Text("Name", box, "아이템 이름", 44f, TextAlignmentOptions.MidlineLeft, false);
        PlaceTop(name.rectTransform, 300f, 130f, -40f, 70f);
        name.color = GoldColor;
        Set("nameText", name);

        TextMeshProUGUI count = b.Text("Count", box, "보유 0개", 32f, TextAlignmentOptions.MidlineLeft, false);
        PlaceTop(count.rectTransform, 300f, 40f, -115f, 50f);
        Set("countText", count);

        TextMeshProUGUI desc = b.Text("Description", box, "설명", 28f, TextAlignmentOptions.TopLeft, true);
        PlaceTop(desc.rectTransform, 300f, 40f, -175f, 90f);
        desc.color = DimText;
        Set("descriptionText", desc);

        // 아래: 조각 전용
        RectTransform section = b.Image("FragmentSection", box, BoxColor, false);
        PlaceTop(section, 40f, 40f, -290f, 430f);
        Set("fragmentSection", section.gameObject);

        TextMeshProUGUI companion = b.Text("Companion", section, "동료  ★☆☆☆☆ 1성", 36f, TextAlignmentOptions.Center, false);
        PlaceTop(companion.rectTransform, 30f, 30f, -30f, 60f);
        Set("companionText", companion);

        TextMeshProUGUI progress = b.Text("Progress", section, "조각 0 / 100", 32f, TextAlignmentOptions.Center, false);
        PlaceTop(progress.rectTransform, 30f, 30f, -100f, 50f);
        Set("progressText", progress);

        Button starUp = LabeledButton(b, section, "StarUp", "성급 올리기", GreenColor, 36f, out _);
        PlaceCenter((RectTransform)starUp.transform, 0f, -210f, 420f, 100f);
        Set("starUpButton", starUp);

        Button evolve = LabeledButton(b, section, "Evolve", "진화", BlueColor, 32f, out TextMeshProUGUI evolveLabel);
        PlaceCenter((RectTransform)evolve.transform, 0f, -210f, 560f, 100f);
        Set("evolveButton", evolve);
        Set("evolveButtonLabel", evolveLabel);

        TextMeshProUGUI notice = b.Text("Notice", section, string.Empty, 28f, TextAlignmentOptions.Center, true);
        PlaceTop(notice.rectTransform, 30f, 30f, -300f, 90f);
        notice.color = DimText;
        Set("noticeText", notice);

        so.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
        return detail;
    }

    // ───────────────────────────── ② 도감 상세창 ─────────────────────────────

    [MenuItem("Tools/UI 굽기/도감 상세창 - 성급별 스펙 칸")]
    private static void BakeCodexStarTable()
    {
        CompanionDetailPanel panel = Find<CompanionDetailPanel>();
        if (panel == null)
        {
            EditorUtility.DisplayDialog("UI 굽기", "열린 씬에 CompanionDetailPanel(도감 상세창)이 없습니다.", "확인");
            return;
        }

        var so = new SerializedObject(panel);
        SerializedProperty table = so.FindProperty("starTableText");
        var stats = so.FindProperty("statsText").objectReferenceValue as TMP_Text;

        string[] legacyFields = { "fragmentText", "starUpButton", "evolveButton" };
        var legacy = legacyFields
            .Select(f => so.FindProperty(f).objectReferenceValue as Component)
            .Where(c => c != null && !EditorUtility.IsPersistent(c))
            .ToList();

        if (table.objectReferenceValue != null && legacy.Count == 0)
        {
            EditorUtility.DisplayDialog("UI 굽기", "이미 성급별 스펙 칸이 있고 예전 성장 UI 도 없습니다.", "확인");
            return;
        }

        if (legacy.Count > 0)
        {
            string names = string.Join(", ", legacy.Select(c => c.gameObject.name));
            if (EditorUtility.DisplayDialog("UI 굽기",
                    $"인벤토리로 옮긴 예전 성장 UI 를 씬에서 지울까요?\n({names})\n\nCtrl+Z 로 되돌릴 수 있습니다.", "지우기", "남겨 두기"))
            {
                foreach (Component c in legacy)
                    if (c != null && c.gameObject != panel.gameObject) Undo.DestroyObjectImmediate(c.gameObject);
                foreach (string f in legacyFields) so.FindProperty(f).objectReferenceValue = null;
            }
        }

        if (table.objectReferenceValue == null)
        {
            if (stats == null)
            {
                EditorUtility.DisplayDialog("UI 굽기", "Stats Text 가 연결돼 있지 않아 스펙 칸 위치를 정할 수 없습니다. 칸을 직접 만들어 Star Table Text 에 넣으세요.", "확인");
            }
            else
            {
                // 능력치 글자를 복제해 같은 글꼴·크기로 바로 아래에
                GameObject copy = Object.Instantiate(stats.gameObject, stats.transform.parent);
                copy.name = "StarTableText";
                copy.transform.SetSiblingIndex(stats.transform.GetSiblingIndex() + 1);
                var rt = (RectTransform)copy.transform;
                rt.anchoredPosition -= new Vector2(0f, ((RectTransform)stats.transform).rect.height + 10f);
                var t = copy.GetComponent<TMP_Text>();
                t.text = "1성  피해 +0  재사용 0초\n2성 ...";
                Undo.RegisterCreatedObjectUndo(copy, "성급별 스펙 칸 굽기");
                table.objectReferenceValue = t;
                EditorGUIUtility.PingObject(copy);
            }
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log($"[InventoryUIBaker] '{panel.gameObject.scene.name}' 도감 상세창 정리 완료. 스펙 칸 위치·크기를 확인하고 씬을 저장하세요.", panel);
    }

    // ───────────────────────────── 부품 ─────────────────────────────

    private static Button CloseButton(Builder b, RectTransform parent)
    {
        RectTransform close = b.Image("Close", parent, HeaderColor, true);
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-20f, -20f);
        close.sizeDelta = new Vector2(80f, 80f);
        Stretch(b.Text("X", close, "X", 44f, TextAlignmentOptions.Center, false).rectTransform);
        return close.gameObject.AddComponent<Button>();
    }

    private static Button LabeledButton(Builder b, Transform parent, string name, string text, Color color, float size, out TextMeshProUGUI label)
    {
        RectTransform rt = b.Image(name, parent, color, true);
        Button button = rt.gameObject.AddComponent<Button>();
        label = b.Text("Label", rt, text, size, TextAlignmentOptions.Center, false);
        Stretch(label.rectTransform);
        return button;
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

    /// <summary>부모 위 가운데 기준 (x 가로 이동, top 위에서 아래로(음수), 크기)</summary>
    private static void PlaceCenter(RectTransform rt, float x, float top, float width, float height)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, top);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>버튼을 복제해 오른쪽에 둠 (간격 = 왼쪽 이웃과의 간격, 없으면 폭 + 16) — 복제본의 원래 컴포넌트·OnClick 은 지움</summary>
    private static GameObject CloneButtonRightOf(Component source, string name, string text)
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

        TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = text;
        return go;
    }

    private static void EnsureFolder()
    {
        string[] parts = PrefabFolder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SaveAsPrefab(GameObject root, string fileName)
    {
        EnsureFolder();
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
        if (font == null) Debug.LogWarning("[InventoryUIBaker] 씬에서 TMP 폰트를 찾지 못해 기본 폰트로 만듭니다.");
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

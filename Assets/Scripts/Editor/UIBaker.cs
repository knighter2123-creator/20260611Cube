using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 예전에 런타임 코드로 만들던 창을 씬에 실제 오브젝트로 "굽고", 컴포넌트의 인스펙터 칸을 채우는 메뉴.
///   Tools/UI 굽기/랭킹 창            — StageScene 을 열고 실행 (RankingPanel)
///   Tools/UI 굽기/저장 데이터 선택 팝업 — LoginScene 을 열고 실행 (CloudSaveConflictPopup)
/// 실행한 뒤 씬을 저장하세요. 이미 구운 창이 있으면 지우고 새로 만듭니다 (Ctrl+Z 로 되돌릴 수 있음).
/// 굽고 나면 모양은 씬에서 자유롭게 고쳐도 됩니다 — 다시 굽기 전까지는 고친 모양이 그대로 쓰입니다.
/// </summary>
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
        if (!ConfirmRebuild(so.FindProperty("root"), "랭킹 창")) return;

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

        Finish(panel.gameObject.scene, root, "랭킹 창");
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
        TMP_FontAsset font = old != null && old.Font != null ? old.Font : MostUsedFont(scene);

        if (old != null)
        {
            if (!EditorUtility.DisplayDialog("UI 굽기", "이미 저장 데이터 선택 팝업이 있습니다. 지우고 새로 만들까요?", "새로 만들기", "취소")) return;
            scene = old.gameObject.scene;
            Undo.DestroyObjectImmediate(old.gameObject);
        }

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
        Finish(scene, root, "저장 데이터 선택 팝업");
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

    // ═════════════════════════════ 공용 ═════════════════════════════

    /// <summary>예전에 구운 창이 있으면 지울지 묻고 지웁니다. 계속하면 true.</summary>
    private static bool ConfirmRebuild(SerializedProperty rootProp, string what)
    {
        var old = rootProp.objectReferenceValue as GameObject;
        if (old == null) return true;

        if (!EditorUtility.DisplayDialog("UI 굽기", $"이미 {what}이 있습니다 ({old.name}). 지우고 새로 만들까요?", "새로 만들기", "취소"))
            return false;

        Undo.DestroyObjectImmediate(old);
        return true;
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

    private static void Finish(Scene scene, GameObject root, string what)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log($"[UIBaker] {what}을(를) '{scene.name}' 에 만들고 인스펙터 칸을 채웠습니다. 씬을 저장하세요.", root);
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

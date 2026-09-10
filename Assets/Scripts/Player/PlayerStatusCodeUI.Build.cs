using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 스탯 창 — 코드 생성 버전의 **생김새** 담당.
///
/// 이 파일에는 "언제 열리는가" 가 한 줄도 없습니다. 그건 코어 파일의 일입니다.
/// 여기 있는 건 전부 "무엇을 어떻게 그리는가" 입니다.
///
/// ─────────────────────────────────────────────────────────────
/// [코드로 UI 를 만들 때 알아야 하는 것들]
///
/// ① 유니티 UI 는 결국 RectTransform + Graphic(Image/TMP) 의 조합입니다.
///    프리팹에서 손으로 하는 일을 그대로 코드로 옮기는 것뿐입니다.
///
/// ② 레이아웃은 직접 좌표를 계산하지 말고 LayoutGroup 에게 맡깁니다.
///    "위에서부터 18픽셀 간격으로 쌓기" 를 VerticalLayoutGroup 한 줄로 해결하면,
///    스탯을 하나 추가할 때 아래 항목들의 y좌표를 다시 계산할 필요가 없습니다.
///    좌표를 직접 다루기 시작하면 기기 해상도가 바뀔 때마다 깨집니다.
///
/// ③ 둥근 사각형처럼 스프라이트가 필요한 것은 런타임에 텍스처를 그려서 만듭니다.
///    에셋을 준비하지 않아도 되는 게 코드 생성 방식의 장점입니다.
/// ─────────────────────────────────────────────────────────────
/// </summary>
public partial class PlayerStatusCodeUI
{
    // ══════════════════════════════════════════════════════════
    //  인스펙터 설정 — 생김새
    // ══════════════════════════════════════════════════════════
    [Header("창 모양")]
    [SerializeField] private float windowWidth  = 920f;
    [SerializeField] private int   cornerRadius = 28;
    [SerializeField] private Color windowColor  = new Color(0.11f, 0.13f, 0.18f, 0.98f);
    [SerializeField] private Color dimColor     = new Color(0f, 0f, 0f, 0.65f);
    [SerializeField] private Color dividerColor = new Color(1f, 1f, 1f, 0.10f);

    [Header("글자")]
    [Tooltip("비워두면 TMP 기본 폰트를 씁니다. 한글 폰트를 쓰려면 여기에 넣으세요")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float titleFontSize  = 42f;
    [SerializeField] private float labelFontSize  = 30f;
    [SerializeField] private float totalFontSize  = 42f;
    [SerializeField] private float detailFontSize = 21f;
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color labelColor = new Color(0.78f, 0.82f, 0.90f);
    [SerializeField] private Color totalColor = Color.white;

    [Header("버튼 색")]
    [SerializeField] private Color buttonColor     = new Color(0.20f, 0.42f, 0.85f, 1f);
    [SerializeField] private Color buttonTextColor = Color.white;

    // ══════════════════════════════════════════════════════════
    //  만들어진 부품들 — 코어의 Refresh() 가 이걸 채웁니다
    // ══════════════════════════════════════════════════════════
    private bool          built;
    private GameObject    panelRoot;
    private CanvasGroup   panelCanvasGroup;
    private RectTransform windowRect;

    private TMP_Text levelText;
    private TMP_Text expText;
    private Image    expFillImage;

    private TMP_Text damageTotal,      damageDetail;
    private TMP_Text critDamageTotal,  critDamageDetail;
    private TMP_Text attackSpeedTotal, attackSpeedDetail;
    private TMP_Text critChanceTotal,  critChanceDetail;

    // ══════════════════════════════════════════════════════════
    //  진입점
    // ══════════════════════════════════════════════════════════

    /// <summary>아직 만들지 않았으면 만듭니다. 여러 번 불러도 안전합니다.</summary>
    private void EnsureBuilt()
    {
        if (built) return;
        built = true;   // ★ 실제 생성 전에 세웁니다. 생성 중에 다시 불려도 재귀하지 않게

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogError("[PlayerStatusCodeUI] Canvas 를 만들 수 없어 UI 생성을 중단합니다.");
            built = false;
            return;
        }

        // 클릭이 동작하려면 씬에 EventSystem 이 있어야 합니다.
        // 자동으로 만들지 않는 이유: 프로젝트가 이미 하나를 관리하고 있고,
        // 두 개가 생기면 입력이 이상하게 갈립니다. 경고만 띄우고 판단은 사람에게 맡깁니다.
        if (UnityEngine.EventSystems.EventSystem.current == null)
            Debug.LogWarning("[PlayerStatusCodeUI] 씬에 EventSystem 이 없어 버튼이 눌리지 않습니다.");

        BuildPanel(canvas.transform);
        BuildOpenButton(canvas.transform);

        panelRoot.SetActive(false);
    }

    private Canvas ResolveCanvas()
    {
        if (targetCanvas != null) return targetCanvas;

        targetCanvas = FindFirstObjectByType<Canvas>();
        if (targetCanvas != null) return targetCanvas;

        // 씬에 캔버스가 아예 없을 때만 새로 만듭니다.
        var go = new GameObject("Canvas(Auto)");
        targetCanvas = go.AddComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // CanvasScaler 가 없으면 기기 해상도마다 UI 크기가 달라집니다.
        // 세로 모바일 기준 1080×1920, 높이를 기준으로 맞춥니다.
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 1f;

        go.AddComponent<GraphicRaycaster>();
        return targetCanvas;
    }

    // ══════════════════════════════════════════════════════════
    //  패널
    // ══════════════════════════════════════════════════════════

    private void BuildPanel(Transform canvasTransform)
    {
        // ── 루트 (화면 전체를 덮음) ──
        RectTransform root = NewRect("PlayerStatusPanel(Auto)", canvasTransform);
        Stretch(root);
        panelRoot        = root.gameObject;
        panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();

        // ── 어두운 배경: 눌러서 닫기 ──
        RectTransform dim = NewRect("Dim", root);
        Stretch(dim);
        Image dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = dimColor;
        AddClick(dim.gameObject, dimImage, Close);

        // ── 창 ──
        // 높이를 0 으로 두고 ContentSizeFitter 에게 맡깁니다.
        // 스탯을 추가하면 창이 알아서 길어집니다.
        windowRect = NewRect("Window", root);
        Center(windowRect, new Vector2(windowWidth, 0f));

        Image windowImage = windowRect.gameObject.AddComponent<Image>();
        windowImage.sprite = RoundedSprite(cornerRadius);
        windowImage.type   = Image.Type.Sliced;   // 9분할 — 모서리 곡률이 늘어나지 않습니다
        windowImage.color  = windowColor;

        VerticalLayoutGroup column = windowRect.gameObject.AddComponent<VerticalLayoutGroup>();
        column.padding                = new RectOffset(36, 36, 32, 32);
        column.spacing                = 18f;
        column.childControlWidth      = true;
        column.childControlHeight     = true;
        column.childForceExpandWidth  = true;
        column.childForceExpandHeight = false;

        ContentSizeFitter fitter = windowRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;   // 너비는 우리가 정한 값 유지
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;   // 높이는 내용에 맞춤

        BuildHeader(windowRect);
        BuildDivider(windowRect);

        BuildStatRow(windowRect, PlayerStatusText.LABEL_DAMAGE,       out damageTotal,      out damageDetail);
        BuildStatRow(windowRect, PlayerStatusText.LABEL_CRIT_DAMAGE,  out critDamageTotal,  out critDamageDetail);
        BuildStatRow(windowRect, PlayerStatusText.LABEL_ATTACK_SPEED, out attackSpeedTotal, out attackSpeedDetail);
        BuildStatRow(windowRect, PlayerStatusText.LABEL_CRIT_CHANCE,  out critChanceTotal,  out critChanceDetail);
    }

    // ── 헤더: 제목 · 닫기 버튼 · 레벨 · 경험치 ──────────────────
    private void BuildHeader(RectTransform parent)
    {
        // 제목 줄
        RectTransform titleRow = NewRow("TitleRow", parent, 8f);

        TMP_Text title = NewText("Title", titleRow, PlayerStatusText.TITLE,
                                 titleFontSize, titleColor, TextAlignmentOptions.Left);
        Flexible(title.gameObject, 1f);

        // 닫기 버튼 — 정사각형이라 레이아웃이 늘리지 않게 고정 크기를 줍니다
        RectTransform closeRect = NewRect("CloseButton", titleRow);
        Fixed(closeRect.gameObject, 56f, 56f);

        Image closeImage = closeRect.gameObject.AddComponent<Image>();
        closeImage.sprite = RoundedSprite(14);
        closeImage.type   = Image.Type.Sliced;
        closeImage.color  = new Color(1f, 1f, 1f, 0.12f);
        AddClick(closeRect.gameObject, closeImage, Close);

        RectTransform closeLabelRect = NewRect("Label", closeRect);
        Stretch(closeLabelRect);
        // 닫기 라벨도 PlayerStatusText 의 상수를 씁니다.
        // 폰트에 없는 기호를 직접 박아두면 나중에 찾기 어렵습니다.
        NewTextOn(closeLabelRect, PlayerStatusText.CLOSE_LABEL, 30f, Color.white, TextAlignmentOptions.Center);

        // 레벨 + 경험치 숫자 줄
        RectTransform levelRow = NewRow("LevelRow", parent, 8f);

        levelText = NewText("LevelText", levelRow, "Lv. -",
                            labelFontSize + 6f, titleColor, TextAlignmentOptions.Left);
        Flexible(levelText.gameObject, 1f);

        expText = NewText("ExpText", levelRow, "-",
                          detailFontSize + 3f, labelColor, TextAlignmentOptions.Right);
        Flexible(expText.gameObject, 1f);

        // 경험치 게이지
        RectTransform barBg = NewRect("ExpBar", parent);
        Fixed(barBg.gameObject, -1f, 16f);   // 너비는 부모가 늘려 주고, 높이만 고정

        Image barBgImage = barBg.gameObject.AddComponent<Image>();
        barBgImage.sprite = RoundedSprite(8);
        barBgImage.type   = Image.Type.Sliced;
        barBgImage.color  = new Color(1f, 1f, 1f, 0.10f);

        RectTransform fill = NewRect("Fill", barBg);
        Stretch(fill);

        expFillImage = fill.gameObject.AddComponent<Image>();
        expFillImage.sprite     = RoundedSprite(8);
        expFillImage.type       = Image.Type.Filled;            // ★ 게이지의 핵심
        expFillImage.fillMethod = Image.FillMethod.Horizontal;
        expFillImage.fillOrigin = 0;                            // 왼쪽부터 채움
        expFillImage.color      = style.augmentColor;
        expFillImage.fillAmount = 0f;
    }

    private void BuildDivider(RectTransform parent)
    {
        RectTransform line = NewRect("Divider", parent);
        Fixed(line.gameObject, -1f, 2f);

        Image image = line.gameObject.AddComponent<Image>();
        image.color         = dividerColor;
        image.raycastTarget = false;
    }

    // ── 스탯 한 줄 ────────────────────────────────────────────
    //
    // Row (Vertical)
    // ├─ Top (Horizontal) : 라벨 ────── 합계
    // └─ Detail           : 기본 · 강화 · 증강
    //
    // 이 함수 하나로 네 줄을 다 만듭니다. 스탯을 추가할 때 호출 한 줄만 늘어납니다.
    private void BuildStatRow(RectTransform parent, string label,
                              out TMP_Text total, out TMP_Text detail)
    {
        RectTransform row = NewRect("Row_" + label, parent);

        VerticalLayoutGroup column = row.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing                = 2f;
        column.childControlWidth      = true;
        column.childControlHeight     = true;
        column.childForceExpandWidth  = true;
        column.childForceExpandHeight = false;

        RectTransform top = NewRow("Top", row, 12f);

        TMP_Text labelText = NewText("Label", top, label,
                                     labelFontSize, labelColor, TextAlignmentOptions.Left);
        Flexible(labelText.gameObject, 1f);

        total = NewText("Total", top, "-", totalFontSize, totalColor, TextAlignmentOptions.Right);
        Flexible(total.gameObject, 1f);

        detail = NewText("Detail", row, "", detailFontSize, style.detailColor, TextAlignmentOptions.Left);
    }

    // ══════════════════════════════════════════════════════════
    //  Status 버튼
    // ══════════════════════════════════════════════════════════

    private void BuildOpenButton(Transform canvasTransform)
    {
        // 이미 만들어 둔 버튼이 있으면 그걸 씁니다.
        if (externalOpenButton != null)
        {
            externalOpenButton.onClick.AddListener(Toggle);
            return;
        }

        if (!createOpenButton) return;

        // ★ 버튼은 패널 **밖**에 만듭니다.
        //   패널 안에 두면 panelRoot.SetActive(false) 로 버튼까지 꺼져서
        //   창을 한 번 닫은 뒤 다시 열 방법이 없어집니다.
        RectTransform rect = NewRect("StatusButton(Auto)", canvasTransform);
        PlaceInCorner(rect, buttonCorner, buttonOffset, buttonSize);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSprite(18);
        image.type   = Image.Type.Sliced;
        image.color  = buttonColor;
        AddClick(rect.gameObject, image, Toggle);

        RectTransform labelRect = NewRect("Label", rect);
        Stretch(labelRect);
        NewTextOn(labelRect, buttonLabel, labelFontSize, buttonTextColor, TextAlignmentOptions.Center);
    }

    // ══════════════════════════════════════════════════════════
    //  범용 UI 도구 — 여기부터는 '스탯' 이라는 단어를 모릅니다
    // ══════════════════════════════════════════════════════════
    //
    // AugmentUIFactory 를 뺐던 것과 같은 성격의 코드입니다.
    // 프로젝트에 이미 AugmentUIFactory 가 있으니, 나중에 두 파일의 도구를 합쳐서
    // UIFactory 하나로 만들면 더 깔끔합니다. 지금은 이 파일만 복사해도
    // 동작하도록 자족적으로 두었습니다.

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);   // false = 로컬 스케일·위치를 유지하지 않고 부모 기준으로 재설정
        return rect;
    }

    /// <summary>부모를 꽉 채웁니다. (배경, 오버레이용)</summary>
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin        = Vector2.zero;
        rect.anchorMax        = Vector2.one;
        rect.offsetMin        = Vector2.zero;
        rect.offsetMax        = Vector2.zero;
        rect.localScale       = Vector3.one;
    }

    /// <summary>부모의 중앙에 고정 크기로 놓습니다.</summary>
    private static void Center(RectTransform rect, Vector2 size)
    {
        rect.anchorMin        = new Vector2(0.5f, 0.5f);
        rect.anchorMax        = new Vector2(0.5f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta        = size;
        rect.localScale       = Vector3.one;
    }

    /// <summary>화면 모서리에 붙입니다. 앵커를 모서리에 맞추므로 해상도가 바뀌어도 유지됩니다.</summary>
    private static void PlaceInCorner(RectTransform rect, ScreenCorner corner, Vector2 offset, Vector2 size)
    {
        // 모서리별 앵커와 부호를 표로 다룹니다. if 를 네 번 쓰는 것보다 읽기 쉽습니다.
        Vector2 anchor;
        Vector2 sign;

        switch (corner)
        {
            case ScreenCorner.TopLeft:     anchor = new Vector2(0f, 1f); sign = new Vector2( 1f, -1f); break;
            case ScreenCorner.BottomLeft:  anchor = new Vector2(0f, 0f); sign = new Vector2( 1f,  1f); break;
            case ScreenCorner.BottomRight: anchor = new Vector2(1f, 0f); sign = new Vector2(-1f,  1f); break;
            default:                       anchor = new Vector2(1f, 1f); sign = new Vector2(-1f, -1f); break;
        }

        rect.anchorMin        = anchor;
        rect.anchorMax        = anchor;
        rect.pivot            = anchor;
        rect.sizeDelta        = size;
        rect.anchoredPosition = new Vector2(offset.x * sign.x, offset.y * sign.y);
        rect.localScale       = Vector3.one;
    }

    /// <summary>가로로 아이템을 늘어놓는 줄을 만듭니다.</summary>
    private static RectTransform NewRow(string name, Transform parent, float spacing)
    {
        RectTransform rect = NewRect(name, parent);

        HorizontalLayoutGroup row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing                = spacing;
        row.childControlWidth      = true;
        row.childControlHeight     = true;
        row.childForceExpandWidth  = false;
        row.childForceExpandHeight = false;
        row.childAlignment         = TextAnchor.MiddleLeft;

        return rect;
    }

    private TMP_Text NewText(string name, Transform parent, string content,
                             float size, Color color, TextAlignmentOptions align)
    {
        RectTransform rect = NewRect(name, parent);
        return NewTextOn(rect, content, size, color, align);
    }

    private TMP_Text NewTextOn(RectTransform rect, string content,
                               float size, Color color, TextAlignmentOptions align)
    {
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;

        text.text          = content;
        text.fontSize      = size;
        text.color         = color;
        text.alignment     = align;
        text.richText      = true;    // <color> 태그로 증강 부분을 강조합니다
        text.raycastTarget = false;   // 글자가 클릭을 가로채지 않게 — 잊기 쉬운 최적화입니다

        return text;
    }

    /// <summary>LayoutGroup 안에서 남는 너비를 나눠 갖게 합니다.</summary>
    private static void Flexible(GameObject go, float weight)
    {
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.flexibleWidth = weight;
    }

    /// <summary>고정 크기. 음수를 넣으면 그 축은 부모가 정하게 둡니다.</summary>
    private static void Fixed(GameObject go, float width, float height)
    {
        LayoutElement element = go.AddComponent<LayoutElement>();
        if (width  > 0f) element.preferredWidth  = width;
        if (height > 0f) element.preferredHeight = height;
    }

    /// <summary>클릭 가능하게 만듭니다. targetGraphic 을 지정해야 눌림 표현이 동작합니다.</summary>
    private static void AddClick(GameObject go, Graphic graphic, UnityEngine.Events.UnityAction action)
    {
        Button button = go.AddComponent<Button>();
        button.targetGraphic = graphic;
        button.onClick.AddListener(action);
    }

    // ══════════════════════════════════════════════════════════
    //  둥근 사각형 스프라이트 생성
    // ══════════════════════════════════════════════════════════
    //
    // 반지름별로 하나만 만들어 캐시합니다. 창·버튼·게이지가 같은 반지름을 쓰면 재사용됩니다.

    private static readonly Dictionary<int, Sprite> roundedCache = new Dictionary<int, Sprite>();

    private static Sprite RoundedSprite(int radius)
    {
        radius = Mathf.Max(1, radius);

        if (roundedCache.TryGetValue(radius, out Sprite cached) && cached != null)
            return cached;

        int size = radius * 2 + 4;   // 가운데 4px 이 늘어나는 부분이 됩니다

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 각 축에서 "모서리 원의 중심" 까지의 거리.
                // 가운데 영역이면 0 이 되어 알파가 1 이 됩니다.
                float dx = x < radius ? radius - x : (x > size - 1 - radius ? x - (size - 1 - radius) : 0f);
                float dy = y < radius ? radius - y : (y > size - 1 - radius ? y - (size - 1 - radius) : 0f);

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // +0.5f 로 경계 1픽셀에 부드러운 계조를 줍니다. 없으면 모서리가 톱니처럼 보입니다.
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        // border 를 주면 Image.Type.Sliced 에서 모서리가 늘어나지 않습니다.
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));

        roundedCache[radius] = sprite;
        return sprite;
    }

    // ══════════════════════════════════════════════════════════
    //  static 캐시와 도메인 리로드
    // ══════════════════════════════════════════════════════════
    //
    // Project Settings → Editor → Enter Play Mode Options 에서 "Reload Domain" 을 끄면
    // static 값이 플레이 종료 후에도 남습니다. 이미 파괴된 텍스처를 가리킬 수 있어요.
    // (위에서 `cached != null` 을 한 번 더 검사하는 것도 같은 이유입니다 —
    //  유니티는 파괴된 오브젝트를 == null 로 취급해 줍니다)
    //
    // 확실하게 하려면 진입점에서 비워줍니다. 모든 static 캐시의 공통 함정입니다.

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticCache()
    {
        roundedCache.Clear();
    }
}
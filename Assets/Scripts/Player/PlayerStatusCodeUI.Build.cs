using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스탯 창 생김새(코드 생성): Build Parent 아래 창·헤더·경험치 게이지·스탯 4줄을 LayoutGroup 으로 쌓음 — 좌표 대신 LayoutGroup 이라 해상도·항목 추가에 강하고, 둥근 스프라이트도 런타임에 그림.</summary>
public partial class PlayerStatusCodeUI
{
    // ── 인스펙터 설정 — 생김새 ──
    [Header("창 모양")]
    [Tooltip("창 너비(px). 0 이하로 두면 부모 너비를 꽉 채웁니다.\n" +
             "탭 안에 넣을 때는 0 을 권장합니다 — 고정 폭은 탭 영역과 거의 항상 어긋납니다.")]
    [SerializeField] private float windowWidth  = 920f;
    [SerializeField] private int   cornerRadius = 28;
    [SerializeField] private Color windowColor  = new Color(0.11f, 0.13f, 0.18f, 0.98f);
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

    // ── 만들어진 부품들 — 코어의 Refresh() 가 이걸 채웁니다 ──
    private bool          built;
    private GameObject    panelRoot;
    private RectTransform windowRect;

    private TMP_Text levelText;
    private TMP_Text expText;
    private Image    expFillImage;

    private TMP_Text damageTotal,      damageDetail;
    private TMP_Text critDamageTotal,  critDamageDetail;
    private TMP_Text attackSpeedTotal, attackSpeedDetail;
    private TMP_Text critChanceTotal,  critChanceDetail;

    // ── 진입점 ──

    /// <summary>아직 만들지 않았으면 만듭니다. 여러 번 불러도 안전합니다.</summary>
    private void EnsureBuilt()
    {
        if (built) return;
        built = true;   // ★ 실제 생성 전에 세웁니다. 생성 중에 다시 불려도 재귀하지 않게

        Transform parent = ResolveBuildParent();
        if (parent == null)
        {
            Debug.LogError("[PlayerStatusCodeUI] 붙일 곳을 찾지 못해 UI 생성을 중단합니다. " +
                           "Build Parent 를 연결하거나 씬에 Canvas 를 두세요.");
            built = false;
            return;
        }

        // EventSystem 이 없으면 경고만 (자동 생성하면 두 개가 되어 입력이 갈릴 수 있음)
        if (UnityEngine.EventSystems.EventSystem.current == null)
            Debug.LogWarning("[PlayerStatusCodeUI] 씬에 EventSystem 이 없어 버튼이 눌리지 않습니다.");

        BuildPanel(parent);
        panelRoot.SetActive(false);
    }

    /// <summary>UI 를 붙일 부모 — Build Parent (탭 내용). 비어 있으면 경고하고 씬의 첫 Canvas.</summary>
    private Transform ResolveBuildParent()
    {
        if (buildParent != null) return buildParent;

        // 그냥 두면 캔버스 한가운데에 만들어져 탭과 무관하게 화면을 덮습니다 — 원인을 찾기 어려운 증상이라 경고.
        Debug.LogWarning("[PlayerStatusCodeUI] Build Parent 가 비어 있습니다. 탭 내용(예: Status_Content)을 연결하세요. " +
                         "지금은 씬의 첫 Canvas 아래에 만들어집니다.", this);
        Canvas canvas = FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    // ── 패널 ──

    private void BuildPanel(Transform parentTransform)
    {
        // ── 루트 (부모를 꽉 채움) ──
        RectTransform root = NewRect("PlayerStatusPanel(Auto)", parentTransform);
        Stretch(root);
        panelRoot = root.gameObject;

        // 어두운 배경(Dim)은 만들지 않습니다 — 탭 창(TabWindow)에 이미 깔려 있어, 또 깔면 창 '안쪽'만 한 겹 더 어두워집니다.

        // ── 창 ── 높이는 ContentSizeFitter 가 내용에 맞춤
        windowRect = NewRect("Window", root);

        // Window Width ≤ 0 이면 가로만 부모를 채움 (탭 안에선 고정 폭이 어긋남, 네 방향 Stretch 는 Fitter 와 싸움)
        if (windowWidth > 0f)
        {
            Center(windowRect, new Vector2(windowWidth, 0f));
        }
        else
        {
            StretchWidth(windowRect);
        }

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

    // ── 헤더: 제목 · 레벨 · 경험치 ──────────────────
    // (닫기 ✕ 는 만들지 않습니다 — TabWindow 헤더에 있음)
    private void BuildHeader(RectTransform parent)
    {
        // 제목 줄
        RectTransform titleRow = NewRow("TitleRow", parent, 8f);

        TMP_Text title = NewText("Title", titleRow, PlayerStatusText.TITLE,
                                 titleFontSize, titleColor, TextAlignmentOptions.Left);
        Flexible(title.gameObject, 1f);

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

    // ── 스탯 한 줄: Top(라벨 ── 합계) + Detail(기본·강화·증강) — 스탯 추가는 호출 한 줄 ──
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

    // ── 범용 UI 도구 (AugmentUIFactory 와 중복 — 나중에 UIFactory 하나로 합치면 깔끔, 지금은 자족적으로) ──

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

    /// <summary>가로만 부모를 채우고 세로는 ContentSizeFitter 에 맡김 (네 방향 Stretch 면 앵커가 Fitter 높이를 덮어써 깜빡임).</summary>
    private static void StretchWidth(RectTransform rect)
    {
        rect.anchorMin        = new Vector2(0f, 0.5f);
        rect.anchorMax        = new Vector2(1f, 0.5f);
        rect.pivot            = new Vector2(0.5f, 0.5f);
        rect.offsetMin        = new Vector2(0f, rect.offsetMin.y);
        rect.offsetMax        = new Vector2(0f, rect.offsetMax.y);
        rect.anchoredPosition = new Vector2(0f, 0f);
        rect.sizeDelta        = new Vector2(0f, 0f);   // 가로는 앵커가, 세로는 Fitter 가 정함
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

    // ── 둥근 사각형 스프라이트 생성 (반지름별로 하나만 만들어 캐시) ──

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
                // 각 축에서 모서리 원 중심까지의 거리 (가운데 영역은 0 → 알파 1)
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

    // ── static 캐시 비우기 — Reload Domain 을 끄면 지난 플레이의 파괴된 텍스처를 가리킬 수 있어서 ──

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticCache()
    {
        roundedCache.Clear();
    }
}
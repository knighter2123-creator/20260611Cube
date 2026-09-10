using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코드로 UI를 만들 때 반복되는 잡일을 모아둔 도구 상자.
///
/// [왜 따로 뺐나]
/// 원래 AugmentSelectUI 안에 CreateImage / CreateText / Stretch 같은 함수가
/// 섞여 있었습니다. 그런데 이 함수들은 '증강'과 아무 상관이 없습니다.
/// 어떤 UI를 만들든 똑같이 쓰이는 범용 코드예요.
///
/// 이렇게 도메인(증강)과 무관한 부분을 밖으로 빼면 두 가지가 좋아집니다.
///   1) AugmentSelectUI 가 "증강 창은 이렇게 생겼다" 만 이야기하게 되어 읽기 쉬워집니다
///   2) 다음에 다른 팝업(설정창, 보상창)을 코드로 만들 때 그대로 재사용할 수 있습니다
///
/// 판단 기준은 간단합니다 — **"이 함수가 '증강'이라는 단어를 몰라도 되는가?"**
/// 몰라도 된다면 밖으로 뺄 후보입니다.
///
/// static 클래스라 인스턴스를 만들지 않고 바로 씁니다.
/// </summary>
public static class AugmentUIFactory
{
    // ─────────────────────────────────────────────────────────
    //  오브젝트 생성
    // ─────────────────────────────────────────────────────────

    /// <summary>Image 를 가진 자식 오브젝트를 만듭니다. sprite 를 주면 9슬라이스로 설정됩니다.</summary>
    public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = color;

        if (sprite != null)
        {
            img.sprite = sprite;
            // 9슬라이스 — 크기를 늘려도 모서리 곡률이 찌그러지지 않습니다.
            img.type = Image.Type.Sliced;
        }
        return img;
    }

    /// <summary>TextMeshProUGUI 를 가진 자식 오브젝트를 만듭니다.</summary>
    public static TextMeshProUGUI CreateText(string name, Transform parent, string text,
                                             float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.fontStyle     = style;
        tmp.color         = color;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;   // 글자가 클릭을 가로채지 않도록
        return tmp;
    }

    /// <summary>빈 RectTransform 컨테이너를 만듭니다. LayoutGroup 을 붙일 그릇으로 씁니다.</summary>
    public static RectTransform CreateContainer(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    // ─────────────────────────────────────────────────────────
    //  배치
    // ─────────────────────────────────────────────────────────

    /// <summary>부모를 꽉 채우도록 늘립니다. padding 만큼 안쪽으로 들어갑니다.</summary>
    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    /// <summary>부모의 특정 높이(0~1)에 가로로 꽉 찬 띠 형태로 배치합니다.</summary>
    public static void AnchorHorizontalBand(RectTransform rt, float anchorY, float height,
                                            float leftMargin = 0.05f, float rightMargin = 0.05f)
    {
        rt.anchorMin        = new Vector2(leftMargin, anchorY);
        rt.anchorMax        = new Vector2(1f - rightMargin, anchorY);
        rt.sizeDelta        = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>텍스트에 최소/선호 높이를 줘서 LayoutGroup 안에서 찌그러지지 않게 합니다.</summary>
    public static LayoutElement SetTextHeight(TextMeshProUGUI tmp, float height)
    {
        var le = tmp.gameObject.AddComponent<LayoutElement>();
        le.minHeight       = height;
        le.preferredHeight = height;
        le.flexibleWidth   = 1f;
        return le;
    }

    /// <summary>고정 크기 요소(아이콘 등)로 만듭니다.</summary>
    public static LayoutElement SetFixedSize(GameObject go, float width, float height)
    {
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth  = width;
        le.preferredHeight = height;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;
        return le;
    }

    /// <summary>세로 배치 LayoutGroup 을 붙입니다.</summary>
    public static VerticalLayoutGroup AddVerticalGroup(GameObject go, float spacing,
                                                       TextAnchor align, bool expandHeight)
    {
        var g = go.AddComponent<VerticalLayoutGroup>();
        g.spacing                = spacing;
        g.childControlWidth      = true;
        g.childControlHeight     = true;
        g.childForceExpandWidth  = true;
        g.childForceExpandHeight = expandHeight;
        g.childAlignment         = align;
        return g;
    }

    /// <summary>가로 배치 LayoutGroup 을 붙입니다.</summary>
    public static HorizontalLayoutGroup AddHorizontalGroup(GameObject go, float spacing,
                                                           TextAnchor align, bool expandWidth)
    {
        var g = go.AddComponent<HorizontalLayoutGroup>();
        g.spacing                = spacing;
        g.childControlWidth      = true;
        g.childControlHeight     = true;
        g.childForceExpandWidth  = expandWidth;
        g.childForceExpandHeight = true;
        g.childAlignment         = align;
        return g;
    }

    // ─────────────────────────────────────────────────────────
    //  절차적 스프라이트
    // ─────────────────────────────────────────────────────────
    //
    // 이미지 에셋 없이 둥근 사각형/원을 코드로 그립니다.
    // 최초 1회만 만들고 static 에 담아 모든 UI 가 공유합니다.

    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    /// <summary>모서리가 둥근 사각형. Image.Type.Sliced 로 늘려도 곡률이 유지됩니다.</summary>
    public static Sprite Rounded()
    {
        if (roundedSprite == null) roundedSprite = MakeRoundedRect(64, 18);
        return roundedSprite;
    }

    /// <summary>원. (반지름이 한 변의 절반인 둥근 사각형 = 원)</summary>
    public static Sprite Circle()
    {
        if (circleSprite == null) circleSprite = MakeRoundedRect(64, 32);
        return circleSprite;
    }

    private static Sprite MakeRoundedRect(int size, int radius)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode   = TextureWrapMode.Clamp
        };

        var px = new Color32[size * size];
        float r = Mathf.Clamp(radius, 1, size / 2);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // 각 픽셀이 모서리 원 바깥으로 얼마나 벗어났는지를 재서 알파를 정합니다.
                // 경계에서 부드럽게 깎아내면 계단 현상(에일리어싱)이 줄어듭니다.
                float dx = Mathf.Max(0f, Mathf.Max(r - (x + 0.5f), (x + 0.5f) - (size - r)));
                float dy = Mathf.Max(0f, Mathf.Max(r - (y + 0.5f), (y + 0.5f) - (size - r)));
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                float a  = Mathf.Clamp01(r - d + 0.5f);

                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        // 마지막 인자(border)를 주면 Sliced 모드에서 모서리가 보존됩니다.
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect,
                             new Vector4(radius, radius, radius, radius));
    }

    // ─────────────────────────────────────────────────────────
    //  이징 (연출용 곡선)
    // ─────────────────────────────────────────────────────────

    /// <summary>목표를 살짝 넘었다가 돌아오는 곡선. '툭' 튀어나오는 느낌을 줍니다.</summary>
    public static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}

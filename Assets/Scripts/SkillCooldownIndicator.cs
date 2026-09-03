using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 동료(Companion) 머리 위에 스킬 쿨다운을 표시하는 인디케이터.
/// 필요한 UI(월드 캔버스 / 원형 배경 / 라디얼 필 / 남은시간 텍스트)를 코드로 자동 생성하므로
/// 프리팹 세팅이 전혀 필요 없습니다.
///
/// 사용법 : SkillCooldownIndicator.Begin(companion, skill.icon, skill.cooldown);
/// </summary>
[DisallowMultipleComponent]
public class SkillCooldownIndicator : MonoBehaviour
{
    [Header("배치")]
    [Tooltip("동료 기준 표시 위치 오프셋")]
    public Vector3 offset = new Vector3(0f, 1.1f, 0f);
    [Tooltip("월드 기준 아이콘 지름(유닛)")]
    public float worldSize = 0.55f;
    [Tooltip("스프라이트 위에 그려지도록 하는 정렬 순서")]
    public int sortingOrder = 100;

    [Header("색상")]
    public Color readyColor  = Color.white;                            // 쿨다운 완료 시 아이콘 색
    public Color coolColor   = new Color(0.45f, 0.45f, 0.50f, 1f);     // 쿨다운 중 아이콘 색(회색)
    public Color maskColor   = new Color(0.02f, 0.02f, 0.05f, 0.70f);  // 라디얼 필 색
    public Color bgColor     = new Color(0.05f, 0.05f, 0.08f, 0.55f);  // 원형 배경
    public Color ringColor   = new Color(1f, 0.85f, 0.35f, 1f);        // 테두리(준비 완료 시 강조)

    [Header("연출")]
    [Tooltip("쿨다운 완료 후 아이콘이 잠깐 반짝이는 시간")]
    public float readyFlashTime = 0.35f;
    [Tooltip("남은 시간 숫자 표시")]
    public bool showRemainText = true;

    // ── 내부 ──────────────────────────────────────
    private RectTransform _root;
    private Image _ring, _bg, _icon, _mask;
    private Text  _text;

    private float _duration;
    private float _endTime;
    private bool  _running;
    private float _flashUntil;

    /// <summary>현재 쿨다운 중인지</summary>
    public bool IsCoolingDown => _running;
    /// <summary>남은 쿨다운 시간(초)</summary>
    public float Remaining => _running ? Mathf.Max(0f, _endTime - Time.time) : 0f;
    /// <summary>0(방금 사용) ~ 1(사용 가능)</summary>
    public float Progress01 => (_running && _duration > 0f) ? 1f - (Remaining / _duration) : 1f;

    // ─────────────────────────────────────────────
    // 정적 진입점
    // ─────────────────────────────────────────────
    public static SkillCooldownIndicator Begin(Component owner, Sprite icon, float cooldown)
    {
        if (owner == null || cooldown <= 0f) return null;

        SkillCooldownIndicator ind = owner.GetComponent<SkillCooldownIndicator>();
        if (ind == null) ind = owner.gameObject.AddComponent<SkillCooldownIndicator>();

        ind.StartCooldown(icon, cooldown);
        return ind;
    }

    public void StartCooldown(Sprite icon, float cooldown)
    {
        EnsureBuilt();

        _duration = Mathf.Max(0.01f, cooldown);
        _endTime  = Time.time + _duration;
        _running  = true;

        if (icon != null) _icon.sprite = icon;
        _icon.enabled = _icon.sprite != null;
        _icon.color   = coolColor;

        _mask.fillAmount = 1f;
        _ring.color      = new Color(ringColor.r, ringColor.g, ringColor.b, 0.25f);

        SetVisible(true);
    }

    /// <summary>쿨다운을 즉시 끝냅니다.(쿨감 아이템 등)</summary>
    public void FinishNow()
    {
        if (!_running) return;
        _endTime = Time.time;
    }

    // ─────────────────────────────────────────────
    private void LateUpdate()
    {
        if (_root == null) return;

        // 부모가 좌우 반전(scale.x = -1)돼도 UI는 항상 정방향 유지
        Vector3 ls = transform.localScale;
        float k = worldSize / 100f;
        _root.localScale = new Vector3(
            Mathf.Approximately(ls.x, 0f) ? k : k / ls.x,
            Mathf.Approximately(ls.y, 0f) ? k : k / ls.y,
            1f);
        _root.localPosition = offset;
        _root.rotation      = Quaternion.identity;

        if (_running)
        {
            float remain = Mathf.Max(0f, _endTime - Time.time);
            _mask.fillAmount = _duration > 0f ? remain / _duration : 0f;

            if (showRemainText && _text != null)
            {
                _text.enabled = true;
                _text.text = remain >= 1f
                    ? Mathf.CeilToInt(remain).ToString()
                    : remain.ToString("0.0");
            }

            if (remain <= 0f)
            {
                _running    = false;
                _flashUntil = Time.time + readyFlashTime;

                _mask.fillAmount = 0f;
                _icon.color      = readyColor;
                _ring.color      = ringColor;
                if (_text != null) _text.enabled = false;
            }
            return;
        }

        // 쿨다운 완료 직후 : 살짝 커졌다 작아지는 연출 후 숨김
        if (_flashUntil > 0f)
        {
            float t = 1f - Mathf.Clamp01((_flashUntil - Time.time) / Mathf.Max(0.01f, readyFlashTime));
            float pop = 1f + 0.35f * Mathf.Sin(t * Mathf.PI);
            _root.localScale *= pop;

            Color c = ringColor; c.a = 1f - t;
            _ring.color = c;

            if (Time.time >= _flashUntil)
            {
                _flashUntil = 0f;
                SetVisible(false);
            }
        }
    }

    private void SetVisible(bool on)
    {
        if (_root != null && _root.gameObject.activeSelf != on)
            _root.gameObject.SetActive(on);
    }

    // ─────────────────────────────────────────────
    // UI 생성
    // ─────────────────────────────────────────────
    private void EnsureBuilt()
    {
        if (_root != null) return;

        GameObject rootGo = new GameObject("SkillCooldownUI");
        rootGo.transform.SetParent(transform, false);

        Canvas canvas = rootGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = sortingOrder;
        if (Camera.main != null) canvas.worldCamera = Camera.main;

        CanvasScaler scaler = rootGo.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100f;

        _root = rootGo.GetComponent<RectTransform>();
        _root.sizeDelta     = new Vector2(100f, 100f);
        _root.localPosition = offset;
        _root.localScale    = Vector3.one * (worldSize / 100f);

        Sprite circle = CircleSprite();

        _ring = CreateImage("Ring", _root, circle, new Color(ringColor.r, ringColor.g, ringColor.b, 0.25f), 100f);
        _bg   = CreateImage("BG",   _root, circle, bgColor,   88f);
        _icon = CreateImage("Icon", _root, null,   coolColor, 70f);
        _icon.enabled = false;

        _mask = CreateImage("Mask", _root, circle, maskColor, 88f);
        _mask.type          = Image.Type.Filled;
        _mask.fillMethod    = Image.FillMethod.Radial360;
        _mask.fillOrigin    = (int)Image.Origin360.Top;
        _mask.fillClockwise = false;
        _mask.fillAmount    = 0f;

        if (showRemainText) _text = CreateText("Remain", _root);

        SetVisible(false);
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color, float size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);

        Image img = go.AddComponent<Image>();
        img.sprite         = sprite;
        img.color          = color;
        img.raycastTarget  = false;
        img.preserveAspect = true;
        return img;
    }

    private static Text CreateText(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(100f, 60f);

        Text t = go.AddComponent<Text>();
        t.font              = BuiltinFont();
        t.fontSize          = 44;
        t.alignment         = TextAnchor.MiddleCenter;
        t.color             = Color.white;
        t.raycastTarget     = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.enabled           = false;

        Outline o = go.AddComponent<Outline>();
        o.effectColor    = new Color(0f, 0f, 0f, 0.9f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    private static Font _font;
    private static Font BuiltinFont()
    {
        if (_font != null) return _font;
#if UNITY_2022_1_OR_NEWER
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        return _font;
    }

    // 라디얼 필용 원형 스프라이트를 런타임에 생성 (에셋 불필요)
    private static Sprite _circleSprite;
    private static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;

        const int S = 128;
        Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = "CooldownCircle"
        };

        Vector2 center = new Vector2((S - 1) * 0.5f, (S - 1) * 0.5f);
        float radius = S * 0.5f - 1f;
        Color32[] pixels = new Color32[S * S];

        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float a = Mathf.Clamp01(radius - d);   // 가장자리 1px 안티에일리어싱
                pixels[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        _circleSprite.name = "CooldownCircle";
        return _circleSprite;
    }
}

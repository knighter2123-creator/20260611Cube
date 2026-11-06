using UnityEngine;
using UnityEngine.UI;

/// <summary>동료 머리 위 스킬 쿨다운 표시(월드 캔버스·라디얼 필·남은 시간을 코드로 생성, 프리팹 불필요) — Begin(companion, icon, cooldown).</summary>
[DisallowMultipleComponent]
public class SkillCooldownIndicator : MonoBehaviour
{
    [Header("배치")]
    [Tooltip("동료 기준 표시 위치 오프셋")]
    // 0.6 = 머리(0.5) 바로 위 (1.1 이면 칸이 1유닛이라 위 칸 동료 것처럼 보임)
    public Vector3 offset = new Vector3(0f, 0.6f, 0f);
    [Tooltip("월드 기준 아이콘 지름(유닛)")]
    public float worldSize = 0.55f;
    [Tooltip("스프라이트 위에 그려지도록 하는 정렬 순서")]
    public int sortingOrder = 100;

    [Header("색상")]
    public Color iconColor   = Color.white;                            // 아이콘 색 (흰색 = 스킬 이미지 원본 그대로, 쿨다운 중에도 유지)
    public Color maskColor   = new Color(0.02f, 0.02f, 0.05f, 0.70f);  // 라디얼 필 색 (아이콘 '뒤'에 그려져 아이콘을 가리지 않음)
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
    private int   _lastTextKey = int.MinValue;   // 마지막으로 표시한 숫자 (같으면 문자열 재생성 생략)

    /// <summary>현재 쿨다운 중인지</summary>
    public bool IsCoolingDown => _running;
    /// <summary>남은 쿨다운 시간(초)</summary>
    public float Remaining => _running ? Mathf.Max(0f, _endTime - Time.time) : 0f;
    /// <summary>0(방금 사용) ~ 1(사용 가능)</summary>
    public float Progress01 => (_running && _duration > 0f) ? 1f - (Remaining / _duration) : 1f;

    // ── 정적 진입점 ──
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

        _duration    = Mathf.Max(0.01f, cooldown);
        _endTime     = Time.time + _duration;
        _running     = true;
        _flashUntil  = 0f;               // 반짝이는 도중 다시 시작해도 연출이 겹치지 않게
        _lastTextKey = int.MinValue;     // 숫자를 처음부터 다시 그리게

        if (icon != null) _icon.sprite = icon;
        _icon.enabled = _icon.sprite != null;
        _icon.color   = iconColor;

        _mask.fillAmount = 1f;
        _ring.color      = new Color(ringColor.r, ringColor.g, ringColor.b, 0.25f);

        SetVisible(true);
    }


    // ─────────────────────────────────────────────
    private void LateUpdate()
    {
        if (_root == null) return;

        // 쿨다운·반짝임이 없으면 할 일 없음 (동료마다 붙어 있어 매 프레임 계산을 건너뜀)
        if (!_running && _flashUntil <= 0f) return;

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

                // 표시 숫자가 바뀔 때만 문자열 생성(GC 방지) — 1초 이상은 정수(양수 키), 미만은 0.1초(음수 키)로 키 충돌 방지
                int key = remain >= 1f ? Mathf.CeilToInt(remain) : -1 - Mathf.RoundToInt(remain * 10f);
                if (key != _lastTextKey)
                {
                    _lastTextKey = key;
                    _text.text = remain >= 1f
                        ? Mathf.CeilToInt(remain).ToString()
                        : remain.ToString("0.0");
                }
            }

            if (remain <= 0f)
            {
                _running    = false;
                _flashUntil = Time.time + readyFlashTime;

                _mask.fillAmount = 0f;
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

    // ── UI 생성 ──
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

        // 생성 순서 = 그리는 순서(테두리 → 배경 → 필 → 아이콘 → 숫자) — 필을 아이콘 뒤에 깔아 진행은 둘레에서 보이고 아이콘은 원본 그대로
        _ring = CreateImage("Ring", _root, circle, new Color(ringColor.r, ringColor.g, ringColor.b, 0.25f), 100f);
        _bg   = CreateImage("BG",   _root, circle, bgColor,   88f);

        _mask = CreateImage("Mask", _root, circle, maskColor, 88f);
        _mask.type          = Image.Type.Filled;
        _mask.fillMethod    = Image.FillMethod.Radial360;
        _mask.fillOrigin    = (int)Image.Origin360.Top;
        _mask.fillClockwise = false;
        _mask.fillAmount    = 0f;

        _icon = CreateImage("Icon", _root, null,   iconColor, 70f);
        _icon.enabled = false;

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

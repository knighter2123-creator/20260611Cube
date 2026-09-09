using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 증강 카드 선택창. 프리팹 없이 코드로 UI를 전부 만듭니다.
///
/// [왜 코드로 만드나]
/// 프로젝트의 SkillCooldownIndicator 와 같은 방식입니다.
/// 장점 — 씬/프리팹을 안 건드려도 되고, 파일 하나만 복사하면 다른 프로젝트에서도 바로 돕니다.
/// 단점 — 디자인을 크게 바꾸려면 코드를 고쳐야 합니다.
/// 나중에 아트가 확정되면 이 스크립트를 '프리팹을 채우는' 방식으로 바꾸면 됩니다.
///
/// [일시정지 처리]
/// Time.timeScale = 0 으로 게임을 멈춥니다.
/// 그래서 이 스크립트의 모든 연출은 Time.deltaTime 이 아니라
/// Time.unscaledDeltaTime 을 씁니다. (deltaTime 을 쓰면 0이 곱해져 영원히 멈춥니다)
/// </summary>
public class AugmentSelectUI : MonoBehaviour
{
    public enum LayoutMode
    {
        Horizontal,  // 카드 3장을 가로로 (TFT 스타일. 가로 화면·태블릿에 적합)
        Vertical     // 카드 3장을 세로로 (세로 모바일에서 글자가 잘 보임)
    }

    // ─────────────────────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────────────────────
    [Header("레이아웃")]
    [SerializeField] private LayoutMode layout = LayoutMode.Vertical;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1080, 1920);
    [SerializeField] private int sortingOrder = 5000;

    [Header("문구")]
    [SerializeField] private string titleText = "증강 선택";
    [SerializeField] private string subtitleText = "하나를 선택하세요";

    [Header("자동 선택")]
    [Tooltip("이 시간 안에 고르지 않으면 자동으로 하나가 선택됩니다. 0 이하면 자동 선택 없음")]
    [SerializeField] private float autoPickSeconds = 15f;

    [Header("게임 일시정지")]
    [Tooltip("창이 떠 있는 동안 Time.timeScale 을 0으로 만듭니다")]
    [SerializeField] private bool pauseGame = true;

    [Header("색상")]
    [SerializeField] private Color dimColor        = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private Color cardBackColor   = new Color(0.10f, 0.12f, 0.16f, 1f);
    [SerializeField] private Color nameColor       = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color descColor       = new Color(0.72f, 0.77f, 0.85f, 1f);
    [SerializeField] private Color timerFillColor  = new Color(1f, 0.82f, 0.30f, 1f);

    [Header("등급 색상")]
    [SerializeField] private Color commonColor    = new Color(0.75f, 0.79f, 0.85f);
    [SerializeField] private Color rareColor      = new Color(0.35f, 0.65f, 1.00f);
    [SerializeField] private Color epicColor      = new Color(0.72f, 0.42f, 1.00f);
    [SerializeField] private Color legendaryColor = new Color(1.00f, 0.78f, 0.25f);

    [Header("사운드 (선택)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   openSfx;
    [SerializeField] private AudioClip   pickSfx;

    // ─────────────────────────────────────────────────────────
    //  런타임
    // ─────────────────────────────────────────────────────────
    private Canvas          canvas;
    private CanvasGroup     rootGroup;
    private RectTransform   cardArea;
    private Image           timerFill;
    private TextMeshProUGUI timerLabel;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    private Action<AugmentCard> onChosen;
    private bool   isOpen;
    private bool   picked;
    private float  savedTimeScale = 1f;
    private Coroutine timerRoutine;

    // 절차적으로 만든 스프라이트는 한 번만 만들어 재사용합니다.
    private static Sprite roundedSprite;
    private static Sprite circleSprite;

    // ─────────────────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────────────────

    /// <summary>카드 목록을 보여주고, 선택되면 콜백을 부릅니다.</summary>
    public void Show(List<AugmentCard> cards, Action<AugmentCard> callback)
    {
        if (isOpen) return;                       // 중복 오픈 방지
        if (cards == null || cards.Count == 0) return;

        onChosen = callback;
        picked   = false;
        isOpen   = true;

        EnsureBuilt();

        BuildCards(cards);

        canvas.gameObject.SetActive(true);

        if (pauseGame)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (audioSource != null && openSfx != null) audioSource.PlayOneShot(openSfx);

        StartCoroutine(PlayIntro());

        if (autoPickSeconds > 0f)
            timerRoutine = StartCoroutine(AutoPickCountdown(cards));
        else
            SetTimerVisible(false);
    }

    /// <summary>강제로 닫기 (선택 없이). 보통 쓸 일 없습니다.</summary>
    public void ForceClose() => Close();

    // ─────────────────────────────────────────────────────────
    //  UI 생성 — 최초 1회만 실행됩니다
    // ─────────────────────────────────────────────────────────
    private void EnsureBuilt()
    {
        if (canvas != null) return;

        // ── 캔버스 ──────────────────────────────────────────
        var canvasGo = new GameObject("AugmentSelectCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;   // 다른 HUD 위에 확실히 뜨도록 큰 값

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight  = 0.5f;    // 가로/세로 중간을 기준으로 — 기기별 편차에 무난

        rootGroup = canvasGo.GetComponent<CanvasGroup>();

        // ── 어두운 배경 ────────────────────────────────────
        var dim = CreateImage("Dim", canvasGo.transform, null, dimColor);
        Stretch(dim.rectTransform);
        // 배경을 클릭해도 뒤쪽 게임이 눌리지 않도록 raycastTarget 은 켜둔 채로 둡니다.

        // ── 제목 ───────────────────────────────────────────
        var title = CreateText("Title", canvasGo.transform, titleText, 74, FontStyles.Bold, nameColor);
        AnchorCenterTop(title.rectTransform, 0.845f, 100f);

        var subtitle = CreateText("Subtitle", canvasGo.transform, subtitleText, 36, FontStyles.Normal, descColor);
        AnchorCenterTop(subtitle.rectTransform, 0.785f, 60f);

        // ── 카드가 놓일 영역 ───────────────────────────────
        var areaGo = new GameObject("CardArea", typeof(RectTransform));
        areaGo.transform.SetParent(canvasGo.transform, false);
        cardArea = areaGo.GetComponent<RectTransform>();
        cardArea.anchorMin = new Vector2(0.06f, 0.20f);
        cardArea.anchorMax = new Vector2(0.94f, 0.755f);
        cardArea.offsetMin = Vector2.zero;
        cardArea.offsetMax = Vector2.zero;

        // LayoutGroup 이 자식들의 위치·크기를 자동으로 잡아줍니다.
        // 직접 좌표 계산을 하지 않아도 되고, 카드 장수가 2장이든 4장이든 알아서 배치됩니다.
        if (layout == LayoutMode.Horizontal)
        {
            var h = areaGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 24f;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleCenter;
        }
        else
        {
            var v = areaGo.AddComponent<VerticalLayoutGroup>();
            v.spacing = 24f;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = true;
            v.childAlignment = TextAnchor.MiddleCenter;
        }

        // ── 자동 선택 타이머 바 ────────────────────────────
        var barBg = CreateImage("TimerBg", canvasGo.transform, GetRounded(), new Color(1f, 1f, 1f, 0.14f));
        var bgRt = barBg.rectTransform;
        bgRt.anchorMin = new Vector2(0.18f, 0.135f);
        bgRt.anchorMax = new Vector2(0.82f, 0.135f);
        bgRt.sizeDelta = new Vector2(0f, 14f);
        bgRt.anchoredPosition = Vector2.zero;

        timerFill = CreateImage("TimerFill", barBg.transform, GetRounded(), timerFillColor);
        Stretch(timerFill.rectTransform);
        timerFill.type       = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillOrigin = 0;
        timerFill.fillAmount = 1f;

        timerLabel = CreateText("TimerLabel", canvasGo.transform, "", 30, FontStyles.Normal, descColor);
        AnchorCenterTop(timerLabel.rectTransform, 0.095f, 44f);

        canvasGo.SetActive(false);
    }

  

    // ─────────────────────────────────────────────────────────
    //  카드 만들기
    // ─────────────────────────────────────────────────────────
    private void BuildCards(List<AugmentCard> cards)
    {
        // 이전에 만든 카드 정리
        for (int i = 0; i < spawnedCards.Count; i++)
            if (spawnedCards[i] != null) Destroy(spawnedCards[i]);
        spawnedCards.Clear();

        for (int i = 0; i < cards.Count; i++)
            spawnedCards.Add(CreateCard(cards[i]));
    }

    private GameObject CreateCard(AugmentCard card)
    {
        Color rc = GetRarityColor(card.Rarity);

        // ── 바깥 테두리 (등급 색) ──────────────────────────
        var border = CreateImage("Card", cardArea, GetRounded(), rc);
        var cardGo = border.gameObject;
        cardGo.AddComponent<CanvasGroup>();          // 등장 연출에 사용

        var btn = cardGo.AddComponent<Button>();
        btn.targetGraphic = border;
        btn.transition    = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        // 캡처 주의: 람다가 card 변수를 붙잡아 둡니다(클로저).
        // for 루프 변수(i)를 직접 쓰면 마지막 값만 잡히는 고전 버그가 나므로,
        // 이렇게 매개변수로 받은 지역 변수를 쓰는 게 안전합니다.
        btn.onClick.AddListener(() => OnClickCard(card, cardGo));

        // ── 안쪽 배경 ──────────────────────────────────────
        var inner = CreateImage("Inner", cardGo.transform, GetRounded(), cardBackColor);
        Stretch(inner.rectTransform, 5f);            // 5px 만큼 안쪽으로 → 테두리가 보임
        inner.raycastTarget = false;

        // ── 내용 배치 ──────────────────────────────────────
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(inner.transform, false);
        Stretch(contentGo.GetComponent<RectTransform>(), 22f);

        bool iconOnTop = layout == LayoutMode.Horizontal;

        if (iconOnTop)
        {
            var g = contentGo.AddComponent<VerticalLayoutGroup>();
            g.spacing = 12f;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            g.childAlignment = TextAnchor.UpperCenter;
        }
        else
        {
            var g = contentGo.AddComponent<HorizontalLayoutGroup>();
            g.spacing = 20f;
            g.childControlWidth = g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = true;
            g.childAlignment = TextAnchor.MiddleLeft;
        }

        // 아이콘 (없으면 등급 색 원)
        var iconHolder = CreateImage("Icon", contentGo.transform,
            card.Icon != null ? card.Icon : GetCircle(),
            card.Icon != null ? Color.white : new Color(rc.r, rc.g, rc.b, 0.35f));
        iconHolder.raycastTarget = false;
        iconHolder.preserveAspect = true;

        var iconLe = iconHolder.gameObject.AddComponent<LayoutElement>();
        float iconSize = iconOnTop ? 120f : 110f;
        iconLe.preferredWidth  = iconSize;
        iconLe.preferredHeight = iconSize;
        iconLe.flexibleWidth   = 0f;
        iconLe.flexibleHeight  = 0f;

        // 글자 묶음
        var textGo = new GameObject("Texts", typeof(RectTransform));
        textGo.transform.SetParent(contentGo.transform, false);
        var tg = textGo.AddComponent<VerticalLayoutGroup>();
        tg.spacing = 6f;
        tg.childControlWidth = tg.childControlHeight = true;
        tg.childForceExpandWidth = true;
        tg.childForceExpandHeight = false;
        tg.childAlignment = iconOnTop ? TextAnchor.UpperCenter : TextAnchor.MiddleLeft;

        var textLe = textGo.AddComponent<LayoutElement>();
        textLe.flexibleWidth  = 1f;
        textLe.flexibleHeight = 1f;

        // 이름 (등급 색으로 칠하면 등급이 한눈에 들어옵니다)
        var nameTmp = CreateText("Name", textGo.transform, card.DisplayName, 40, FontStyles.Bold, rc);
        nameTmp.alignment = iconOnTop ? TextAlignmentOptions.Top : TextAlignmentOptions.Left;
        nameTmp.raycastTarget = false;
        AddFlexibleText(nameTmp, 48f);

        // 설명
        var descTmp = CreateText("Desc", textGo.transform, card.GetDescription(), 30, FontStyles.Normal, descColor);
        descTmp.alignment = iconOnTop ? TextAlignmentOptions.Top : TextAlignmentOptions.TopLeft;
        descTmp.enableWordWrapping = true;
        descTmp.raycastTarget = false;
        AddFlexibleText(descTmp, 76f);

        // 보유 스택 표시 (2번째부터)
        int stack = AugmentManager.Instance != null ? AugmentManager.Instance.StackOf(card) : 0;
        if (card.IsPermanent && stack > 0)
        {
            string stackText = card.MaxStack > 0 ? $"보유 {stack} / {card.MaxStack}" : $"보유 {stack}";
            var st = CreateText("Stack", textGo.transform, stackText, 26, FontStyles.Normal,
                                new Color(rc.r, rc.g, rc.b, 0.85f));
            st.alignment = iconOnTop ? TextAlignmentOptions.Top : TextAlignmentOptions.Left;
            st.raycastTarget = false;
            AddFlexibleText(st, 32f);
        }

        return cardGo;
    }

    // ─────────────────────────────────────────────────────────
    //  선택 / 닫기
    // ─────────────────────────────────────────────────────────
    private void OnClickCard(AugmentCard card, GameObject cardGo)
    {
        if (picked) return;      // 연타로 두 장 먹는 사고 방지
        picked = true;

        if (timerRoutine != null) { StopCoroutine(timerRoutine); timerRoutine = null; }
        if (audioSource != null && pickSfx != null) audioSource.PlayOneShot(pickSfx);

        StartCoroutine(PlayPickAndClose(card, cardGo));
    }

    private IEnumerator PlayPickAndClose(AugmentCard card, GameObject chosenGo)
    {
        // 고른 카드는 살짝 커지고, 나머지는 흐려집니다.
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null || spawnedCards[i] == chosenGo) continue;
            var cg = spawnedCards[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0.25f;
        }

        var rt = chosenGo != null ? chosenGo.GetComponent<RectTransform>() : null;
        float t = 0f;
        const float dur = 0.18f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;               // ★ 일시정지 중이므로 unscaled
            float k = Mathf.Sin(t / dur * Mathf.PI);   // 0 → 1 → 0 (튕기는 느낌)
            if (rt != null) rt.localScale = Vector3.one * (1f + 0.08f * k);
            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;

        // 전체 페이드 아웃
        t = 0f;
        const float fade = 0.15f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            rootGroup.alpha = 1f - t / fade;
            yield return null;
        }

        Close();

        // ★ 콜백은 창을 닫고 timeScale 을 되돌린 뒤에 부릅니다.
        //   그래야 카드 효과 안에서 연출(레벨업 이펙트 등)을 재생해도 정상 속도로 보입니다.
        onChosen?.Invoke(card);
        onChosen = null;
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        if (timerRoutine != null) { StopCoroutine(timerRoutine); timerRoutine = null; }

        if (pauseGame) Time.timeScale = savedTimeScale;

        if (canvas != null)
        {
            rootGroup.alpha = 1f;
            canvas.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  연출
    // ─────────────────────────────────────────────────────────
    private IEnumerator PlayIntro()
    {
        rootGroup.alpha = 0f;

        // 카드를 살짝 작게 시작해서 순서대로 튀어나오게 합니다.
        var rts = new List<RectTransform>();
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null) continue;
            var rt = spawnedCards[i].GetComponent<RectTransform>();
            rt.localScale = Vector3.one * 0.86f;
            rts.Add(rt);

            var cg = spawnedCards[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }

        float t = 0f;
        const float total = 0.42f;
        const float perCardDelay = 0.07f;

        while (t < total + perCardDelay * rts.Count)
        {
            t += Time.unscaledDeltaTime;

            rootGroup.alpha = Mathf.Clamp01(t / 0.15f);

            for (int i = 0; i < rts.Count; i++)
            {
                float local = Mathf.Clamp01((t - perCardDelay * i) / 0.28f);
                // EaseOutBack — 목표를 살짝 넘었다가 돌아오는 곡선. '툭' 튀어나오는 느낌을 줍니다.
                float e = EaseOutBack(local);
                rts[i].localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, e);

                var cg = rts[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = local;
            }
            yield return null;
        }

        rootGroup.alpha = 1f;
        for (int i = 0; i < rts.Count; i++)
        {
            rts[i].localScale = Vector3.one;
            var cg = rts[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    private IEnumerator AutoPickCountdown(List<AugmentCard> cards)
    {
        SetTimerVisible(true);

        float remain = autoPickSeconds;
        while (remain > 0f)
        {
            remain -= Time.unscaledDeltaTime;
            if (timerFill  != null) timerFill.fillAmount = Mathf.Clamp01(remain / autoPickSeconds);
            if (timerLabel != null) timerLabel.text = $"{Mathf.CeilToInt(Mathf.Max(0f, remain))}초 후 자동 선택";
            yield return null;
        }

        timerRoutine = null;
        if (picked) yield break;

        // 시간이 다 되면 랜덤으로 한 장 고릅니다.
        int idx = UnityEngine.Random.Range(0, cards.Count);
        GameObject go = idx < spawnedCards.Count ? spawnedCards[idx] : null;
        OnClickCard(cards[idx], go);
    }

    private void SetTimerVisible(bool on)
    {
        if (timerFill  != null) timerFill.transform.parent.gameObject.SetActive(on);
        if (timerLabel != null) timerLabel.gameObject.SetActive(on);
    }

    // ─────────────────────────────────────────────────────────
    //  UI 생성 도우미들
    // ─────────────────────────────────────────────────────────
    private Color GetRarityColor(AugmentRarity r)
    {
        switch (r)
        {
            case AugmentRarity.Legendary: return legendaryColor;
            case AugmentRarity.Epic:      return epicColor;
            case AugmentRarity.Rare:      return rareColor;
            default:                      return commonColor;
        }
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type   = Image.Type.Sliced;   // 9슬라이스 — 늘려도 모서리 곡률이 안 깨집니다
        }
        return img;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string text,
                                              float size, FontStyles style, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    /// <summary>텍스트에 최소 높이를 줘서 LayoutGroup 안에서 찌그러지지 않게 합니다.</summary>
    private static void AddFlexibleText(TextMeshProUGUI tmp, float minHeight)
    {
        var le = tmp.gameObject.AddComponent<LayoutElement>();
        le.minHeight       = minHeight;
        le.preferredHeight = minHeight;
        le.flexibleWidth   = 1f;
    }

    private static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    private static void AnchorCenterTop(RectTransform rt, float anchorY, float height)
    {
        rt.anchorMin = new Vector2(0.05f, anchorY);
        rt.anchorMax = new Vector2(0.95f, anchorY);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
    }

    // ── 절차적 스프라이트 ───────────────────────────────────
    // 이미지 에셋 없이 둥근 사각형/원을 코드로 그립니다.
    // 최초 1회만 만들고 static 에 담아 모든 카드가 공유합니다.

    private static Sprite GetRounded()
    {
        if (roundedSprite == null) roundedSprite = MakeRoundedRect(64, 18);
        return roundedSprite;
    }

    private static Sprite GetCircle()
    {
        if (circleSprite == null) circleSprite = MakeRoundedRect(64, 32);
        return circleSprite;   // 반지름 = 절반이면 원이 됩니다
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
                // 각 픽셀이 모서리 원 안에 있는지 계산해서 알파를 정합니다.
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

        // border 를 주면 Image.Type.Sliced 로 늘려도 모서리가 유지됩니다.
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             100f, 0, SpriteMeshType.FullRect,
                             new Vector4(radius, radius, radius, radius));
    }

    // 창이 열린 상태로 씬이 바뀌면 timeScale 이 0인 채로 남는 사고를 막습니다.
    private void OnDisable()
    {
        if (isOpen && pauseGame) Time.timeScale = savedTimeScale;
        isOpen = false;
    }
}

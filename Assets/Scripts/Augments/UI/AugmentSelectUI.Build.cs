using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>증강 선택창 모양: 캔버스 뼈대는 최초 1회(EnsureBuilt), 카드는 열 때마다(BuildCards) 코드로 생성 — 생성 잡일은 AugmentUIFactory 에 맡김.</summary>
public partial class AugmentSelectUI
{
    // 레이아웃 수치를 이름 붙여 한곳에 (매직 넘버 방지, 조정도 한 곳에서)
    private const float TitleAnchorY    = 0.845f;
    private const float SubtitleAnchorY = 0.785f;
    private const float TimerBarAnchorY = 0.135f;
    private const float TimerTextAnchorY = 0.095f;

    private const float TitleFontSize    = 74f;
    private const float SubtitleFontSize = 36f;
    private const float CardNameFontSize = 40f;
    private const float CardDescFontSize = 30f;
    private const float StackFontSize    = 26f;
    private const float TimerFontSize    = 30f;

    private const float CardSpacing   = 24f;
    private const float BorderWidth   = 5f;    // 등급 색 테두리 두께
    private const float CardPadding   = 22f;   // 카드 안쪽 여백
    private const float IconSize      = 115f;

    // ── 캔버스 뼈대 — 최초 1회 ──
    private void EnsureBuilt()
    {
        if (canvas != null) return;

        // ── 캔버스 ──────────────────────────────────────────
        var canvasGo = new GameObject("AugmentSelectCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;   // 다른 HUD 위에 확실히 뜨도록 큰 값

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight  = 0.5f;    // 가로/세로 중간 기준 — 기기별 편차에 무난

        rootGroup = canvasGo.GetComponent<CanvasGroup>();

        // ── 어두운 배경 ── raycastTarget 을 켜 둬서 뒤쪽 게임 버튼이 눌리지 않게 클릭 흡수
        var dim = AugmentUIFactory.CreateImage("Dim", canvasGo.transform, null, dimColor);
        AugmentUIFactory.Stretch(dim.rectTransform);

        // ── 제목 / 부제 ────────────────────────────────────
        var title = CreateText("Title", canvasGo.transform,
            titleText, TitleFontSize, FontStyles.Bold, nameColor);
        AugmentUIFactory.AnchorHorizontalBand(title.rectTransform, TitleAnchorY, 100f);

        var subtitle = CreateText("Subtitle", canvasGo.transform,
            subtitleText, SubtitleFontSize, FontStyles.Normal, descColor);
        AugmentUIFactory.AnchorHorizontalBand(subtitle.rectTransform, SubtitleAnchorY, 60f);

        BuildCardArea(canvasGo.transform);
        BuildTimerBar(canvasGo.transform);

        canvasGo.SetActive(false);
    }

    /// <summary>카드들이 놓일 영역. LayoutGroup 이 위치·크기를 자동으로 잡아줍니다.</summary>
    private void BuildCardArea(Transform parent)
    {
        cardArea = AugmentUIFactory.CreateContainer("CardArea", parent);
        cardArea.anchorMin = new Vector2(0.06f, 0.20f);
        cardArea.anchorMax = new Vector2(0.94f, 0.755f);
        cardArea.offsetMin = Vector2.zero;
        cardArea.offsetMax = Vector2.zero;

        // LayoutGroup 이 장수와 무관하게 균등 배치
        if (layout == LayoutMode.Horizontal)
            AugmentUIFactory.AddHorizontalGroup(cardArea.gameObject, CardSpacing,
                                                TextAnchor.MiddleCenter, expandWidth: true);
        else
            AugmentUIFactory.AddVerticalGroup(cardArea.gameObject, CardSpacing,
                                              TextAnchor.MiddleCenter, expandHeight: true);
    }

    /// <summary>자동 선택까지 남은 시간을 보여주는 게이지.</summary>
    private void BuildTimerBar(Transform parent)
    {
        var barBg = AugmentUIFactory.CreateImage("TimerBg", parent,
            AugmentUIFactory.Rounded(), new Color(1f, 1f, 1f, 0.14f));
        AugmentUIFactory.AnchorHorizontalBand(barBg.rectTransform, TimerBarAnchorY, 14f, 0.18f, 0.18f);

        timerFill = AugmentUIFactory.CreateImage("TimerFill", barBg.transform,
            AugmentUIFactory.Rounded(), timerFillColor);
        AugmentUIFactory.Stretch(timerFill.rectTransform);

        // Filled 타입 + Horizontal → fillAmount 로 좌우 게이지가 됩니다.
        timerFill.type       = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        timerFill.fillOrigin = 0;
        timerFill.fillAmount = 1f;

        timerLabel = CreateText("TimerLabel", parent,
            "", TimerFontSize, FontStyles.Normal, descColor);
        AugmentUIFactory.AnchorHorizontalBand(timerLabel.rectTransform, TimerTextAnchorY, 44f);
    }

    // ── 카드 — 창을 열 때마다 ──
    private void BuildCards(List<AugmentCard> cards)
    {
        ClearCards();

        for (int i = 0; i < cards.Count; i++)
            spawnedCards.Add(CreateCard(cards[i]));
    }

    /// <summary>이전 카드를 전부 치움 — Destroy 는 프레임 끝이라 SetActive(false) 로 즉시 레이아웃에서 빼고, 리스트가 아닌 실제 자식을 기준으로 지워 누락이 없게.</summary>
    private void ClearCards()
    {
        int destroyed = 0;

        // 자식을 지우면서 순회하므로 역순으로 돕니다.
        for (int i = cardArea.childCount - 1; i >= 0; i--)
        {
            var child = cardArea.GetChild(i).gameObject;

            child.SetActive(false);   // ★ 즉시 레이아웃에서 제외
            Destroy(child);           //   실제 파괴는 프레임 끝

            destroyed++;
        }

        // 기록과 실제 수가 다르면 이상 (대표적으로 AugmentSelectUI 가 둘)
        if (destroyed != spawnedCards.Count)
        {
            Debug.LogWarning(
                $"[AugmentSelectUI] 카드 정리 불일치 — 기록 {spawnedCards.Count}장 / 실제 {destroyed}장.\n" +
                $"  씬에 AugmentSelectUI 가 두 개 이상 있거나, 창이 비정상 종료된 적이 있습니다.");
        }

        spawnedCards.Clear();
    }

    private GameObject CreateCard(AugmentCard card)
    {
        Color rarityColor = GetRarityColor(card.Rarity);
        bool  iconOnTop   = layout == LayoutMode.Horizontal;

        // ── 바깥 테두리 (등급 색) = 카드 본체 ──────────────
        var border = AugmentUIFactory.CreateImage("Card", cardArea,
                                                  AugmentUIFactory.Rounded(), rarityColor);
        var cardGo = border.gameObject;
        cardGo.AddComponent<CanvasGroup>();          // 등장/선택 연출에 사용

        SetupCardButton(cardGo, border, card);

        // ── 안쪽 배경 ── BorderWidth 만큼 안쪽에 깔아 등급 색이 테두리처럼 보이게
        var inner = AugmentUIFactory.CreateImage("Inner", cardGo.transform,
                                                 AugmentUIFactory.Rounded(), cardBackColor);
        AugmentUIFactory.Stretch(inner.rectTransform, BorderWidth);
        inner.raycastTarget = false;

        // ── 내용 그릇 ──────────────────────────────────────
        var content = AugmentUIFactory.CreateContainer("Content", inner.transform);
        AugmentUIFactory.Stretch(content, CardPadding);

        if (iconOnTop)
            AugmentUIFactory.AddVerticalGroup(content.gameObject, 12f,
                                              TextAnchor.UpperCenter, expandHeight: false);
        else
            AugmentUIFactory.AddHorizontalGroup(content.gameObject, 20f,
                                                TextAnchor.MiddleLeft, expandWidth: false);

        BuildCardIcon(content, card, rarityColor);
        BuildCardTexts(content, card, rarityColor, iconOnTop);

        return cardGo;
    }

    /// <summary>카드를 누를 수 있게 만듭니다.</summary>
    private void SetupCardButton(GameObject cardGo, Image targetGraphic, AugmentCard card)
    {
        var btn = cardGo.AddComponent<Button>();
        btn.targetGraphic = targetGraphic;
        btn.transition    = Selectable.Transition.ColorTint;

        var colors = btn.colors;
        colors.normalColor      = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
        colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
        colors.fadeDuration     = 0.08f;
        btn.colors = colors;

        // 람다는 변수를 참조로 붙잡음 — 매개변수(card, cardGo)는 호출마다 별도라 안전 (루프 변수 i 는 위험)
        btn.onClick.AddListener(() => OnClickCard(card, cardGo));
    }

    /// <summary>정사각형 아이콘(없으면 등급 색 원) — LayoutGroup 이 늘려 납작해지지 않게 칸(IconSlot) 안 가운데에 고정 크기, Sliced 는 preserveAspect 가 무시돼 Simple 로.</summary>
    private void BuildCardIcon(RectTransform parent, AugmentCard card, Color rarityColor)
    {
        var slot = AugmentUIFactory.CreateContainer("IconSlot", parent);
        AugmentUIFactory.SetFixedSize(slot.gameObject, IconSize, IconSize);

        bool hasIcon = card.Icon != null;

        var icon = AugmentUIFactory.CreateImage("Icon", slot,
            hasIcon ? card.Icon : AugmentUIFactory.Circle(),
            hasIcon ? Color.white : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.35f));

        icon.type           = Image.Type.Simple;
        icon.raycastTarget  = false;
        icon.preserveAspect = true;

        RectTransform rt = icon.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(IconSize, IconSize);
    }

    /// <summary>모든 텍스트 생성 창구 — uiFont(한글 폰트)를 한 곳에서 적용 (비면 TMP 기본 폰트).</summary>
    private TextMeshProUGUI CreateText(string name, Transform parent, string text,
                                       float size, FontStyles style, Color color)
    {
        var tmp = AugmentUIFactory.CreateText(name, parent, text, size, style, color);
        if (uiFont != null) tmp.font = uiFont;
        return tmp;
    }

    /// <summary>이름 / 설명 / 보유 스택 묶음.</summary>
    private void BuildCardTexts(RectTransform parent, AugmentCard card,
                                Color rarityColor, bool iconOnTop)
    {
        var texts = AugmentUIFactory.CreateContainer("Texts", parent);
        AugmentUIFactory.AddVerticalGroup(texts.gameObject, 6f,
            iconOnTop ? TextAnchor.UpperCenter : TextAnchor.MiddleLeft, expandHeight: false);

        var le = texts.gameObject.AddComponent<LayoutElement>();
        le.flexibleWidth  = 1f;
        le.flexibleHeight = 1f;

        var nameAlign = iconOnTop ? TextAlignmentOptions.Top
                                  : TextAlignmentOptions.Left;
        var descAlign = iconOnTop ? TextAlignmentOptions.Top
                                  : TextAlignmentOptions.TopLeft;

        // 이름 — 등급 색으로 칠하면 등급이 한눈에 들어옵니다.
        var nameTmp = CreateText("Name", texts,
            card.DisplayName, CardNameFontSize, FontStyles.Bold, rarityColor);
        nameTmp.alignment = nameAlign;
        AugmentUIFactory.SetTextHeight(nameTmp, 48f);

        // 설명
        var descTmp = CreateText("Desc", texts,
            card.GetDescription(), CardDescFontSize, FontStyles.Normal, descColor);
        descTmp.alignment          = descAlign;
        descTmp.textWrappingMode = TextWrappingModes.Normal;
        AugmentUIFactory.SetTextHeight(descTmp, 76f);

        // 보유 스택 — 두 번째로 먹을 때부터 표시
        int stack = AugmentManager.Instance != null ? AugmentManager.Instance.StackOf(card) : 0;
        if (!card.IsPermanent || stack <= 0) return;

        string stackText = card.MaxStack > 0 ? $"보유 {stack} / {card.MaxStack}" : $"보유 {stack}";
        var stackTmp = CreateText("Stack", texts,
            stackText, StackFontSize, FontStyles.Normal,
            new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.85f));
        stackTmp.alignment = nameAlign;
        AugmentUIFactory.SetTextHeight(stackTmp, 32f);
    }
}
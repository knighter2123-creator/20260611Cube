using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 증강 카드 선택창 — "어떻게 생겼는가".
///
/// 캔버스 뼈대는 최초 1회만 만들고(EnsureBuilt), 카드는 열 때마다 다시 만듭니다(BuildCards).
/// 실제 생성 잡일은 AugmentUIFactory 에 맡기고, 여기서는 배치 의도만 서술합니다.
/// </summary>
public partial class AugmentSelectUI
{
    // 레이아웃 수치를 한곳에 모아둡니다.
    // 코드 여기저기 박힌 숫자(매직 넘버)는 나중에 "이 40이 뭐였지?" 가 되기 쉬워서,
    // 이름을 붙여 위로 끌어올리면 의미가 드러나고 조정도 한 곳에서 됩니다.
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

    // ─────────────────────────────────────────────────────────
    //  캔버스 뼈대 — 최초 1회
    // ─────────────────────────────────────────────────────────
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

        // ── 어두운 배경 ────────────────────────────────────
        // raycastTarget 을 켜둔 채로 둡니다. 그래야 배경을 눌렀을 때
        // 뒤쪽 게임 버튼이 눌리지 않습니다. (클릭을 흡수하는 역할)
        var dim = AugmentUIFactory.CreateImage("Dim", canvasGo.transform, null, dimColor);
        AugmentUIFactory.Stretch(dim.rectTransform);

        // ── 제목 / 부제 ────────────────────────────────────
        var title = AugmentUIFactory.CreateText("Title", canvasGo.transform,
            titleText, TitleFontSize, TMPro.FontStyles.Bold, nameColor);
        AugmentUIFactory.AnchorHorizontalBand(title.rectTransform, TitleAnchorY, 100f);

        var subtitle = AugmentUIFactory.CreateText("Subtitle", canvasGo.transform,
            subtitleText, SubtitleFontSize, TMPro.FontStyles.Normal, descColor);
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

        // LayoutGroup 을 쓰면 좌표 계산을 직접 하지 않아도 되고,
        // 카드가 2장이든 4장이든 알아서 균등 배치됩니다.
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

        timerLabel = AugmentUIFactory.CreateText("TimerLabel", parent,
            "", TimerFontSize, TMPro.FontStyles.Normal, descColor);
        AugmentUIFactory.AnchorHorizontalBand(timerLabel.rectTransform, TimerTextAnchorY, 44f);
    }

    // ─────────────────────────────────────────────────────────
    //  카드 — 창을 열 때마다
    // ─────────────────────────────────────────────────────────
    private void BuildCards(List<AugmentCard> cards)
    {
        ClearCards();

        for (int i = 0; i < cards.Count; i++)
            spawnedCards.Add(CreateCard(cards[i]));
    }

    /// <summary>
    /// 이전에 만든 카드를 전부 치웁니다.
    ///
    /// ─── 왜 Destroy 만으로는 부족한가 (중요한 학습 포인트) ─────────────────
    ///
    /// Destroy() 는 **즉시 지우지 않습니다.** "이 프레임이 끝나면 지워라" 라고
    /// 예약만 걸어둡니다. 그래서 Destroy 를 부른 직후에도 그 오브젝트는
    /// 여전히 살아 있고, 여전히 cardArea 의 자식이고,
    /// **여전히 LayoutGroup 이 자리를 잡아줍니다.**
    ///
    /// 바로 다음 줄에서 새 카드를 3장 만들면, 그 순간 화면에는 옛 카드까지
    /// 합쳐서 보이게 됩니다. 그래서 SetActive(false) 를 함께 부릅니다.
    /// 이건 예약이 아니라 즉시 반영이라 레이아웃에서 바로 빠집니다.
    ///
    /// (즉시 지우는 DestroyImmediate 도 있지만, 런타임에서 쓰면
    ///  순회 중이던 컬렉션이 깨지는 등 위험해서 에디터 전용으로만 씁니다)
    ///
    /// ─── 왜 리스트가 아니라 자식을 도는가 ────────────────────────────────
    ///
    /// spawnedCards 리스트를 믿으면, 어떤 이유로든 리스트에 안 담긴 오브젝트가
    /// 생겼을 때 그건 영원히 안 지워집니다.
    /// "실제로 화면에 붙어 있는 것"인 cardArea 의 자식을 기준으로 치우면
    /// 경로가 어떻든 확실하게 정리됩니다.
    /// 이렇게 **기록이 아니라 실물을 기준으로 삼는 것**이 훨씬 튼튼합니다.
    /// ────────────────────────────────────────────────────────────────────
    /// </summary>
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

        // 리스트에 담긴 수와 실제 자식 수가 다르면 뭔가 잘못된 겁니다.
        // 대표적으로 씬에 AugmentSelectUI 가 두 개 있는 경우입니다.
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

        // ── 안쪽 배경 ──────────────────────────────────────
        // 테두리보다 BorderWidth 만큼 안쪽에 깔아서 등급 색이 테두리처럼 보이게 합니다.
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

        // ─── 클로저 주의 (학습 포인트) ────────────────────────────────
        // 람다는 바깥 변수를 '값 복사'가 아니라 '참조'로 붙잡습니다.
        // for 루프 안에서 반복 변수 i 를 그대로 쓰면 모든 람다가 같은 i 를 보게 되어
        // 결국 마지막 값만 남는 고전적인 버그가 납니다.
        // 여기처럼 매개변수로 받은 지역 변수(card, cardGo)를 쓰면
        // 호출마다 별도의 변수라서 안전합니다.
        btn.onClick.AddListener(() => OnClickCard(card, cardGo));
    }

    /// <summary>아이콘. 스프라이트가 없으면 등급 색 원으로 대체합니다.</summary>
    private void BuildCardIcon(RectTransform parent, AugmentCard card, Color rarityColor)
    {
        bool hasIcon = card.Icon != null;

        var icon = AugmentUIFactory.CreateImage("Icon", parent,
            hasIcon ? card.Icon : AugmentUIFactory.Circle(),
            hasIcon ? Color.white : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.35f));

        icon.raycastTarget  = false;
        icon.preserveAspect = true;

        AugmentUIFactory.SetFixedSize(icon.gameObject, IconSize, IconSize);
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

        var nameAlign = iconOnTop ? TMPro.TextAlignmentOptions.Top
                                  : TMPro.TextAlignmentOptions.Left;
        var descAlign = iconOnTop ? TMPro.TextAlignmentOptions.Top
                                  : TMPro.TextAlignmentOptions.TopLeft;

        // 이름 — 등급 색으로 칠하면 등급이 한눈에 들어옵니다.
        var nameTmp = AugmentUIFactory.CreateText("Name", texts,
            card.DisplayName, CardNameFontSize, TMPro.FontStyles.Bold, rarityColor);
        nameTmp.alignment = nameAlign;
        AugmentUIFactory.SetTextHeight(nameTmp, 48f);

        // 설명
        var descTmp = AugmentUIFactory.CreateText("Desc", texts,
            card.GetDescription(), CardDescFontSize, TMPro.FontStyles.Normal, descColor);
        descTmp.alignment          = descAlign;
        descTmp.enableWordWrapping = true;
        AugmentUIFactory.SetTextHeight(descTmp, 76f);

        // 보유 스택 — 두 번째로 먹을 때부터 표시
        int stack = AugmentManager.Instance != null ? AugmentManager.Instance.StackOf(card) : 0;
        if (!card.IsPermanent || stack <= 0) return;

        string stackText = card.MaxStack > 0 ? $"보유 {stack} / {card.MaxStack}" : $"보유 {stack}";
        var stackTmp = AugmentUIFactory.CreateText("Stack", texts,
            stackText, StackFontSize, TMPro.FontStyles.Normal,
            new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.85f));
        stackTmp.alignment = nameAlign;
        AugmentUIFactory.SetTextHeight(stackTmp, 32f);
    }
}
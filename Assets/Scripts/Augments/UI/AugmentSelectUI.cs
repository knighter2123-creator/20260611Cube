using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 증강 카드 선택창 — 흐름 담당.
///
/// 이 클래스는 partial 로 세 파일에 나뉘어 있습니다.
///   AugmentSelectUI.cs        ← 지금 이 파일. 상태와 "언제 열고 닫는가"
///   AugmentSelectUI.Build.cs  ← "어떻게 생겼는가" (캔버스·카드 생성)
///   AugmentSelectUI.Anim.cs   ← "어떻게 움직이는가" (등장/선택 연출, 타이머)
///
/// [왜 나눴나]
/// 원래는 한 파일에 600줄이 넘었고, 창을 여는 로직을 고치려는데
/// 스프라이트를 그리는 코드가 눈앞에 계속 지나갔습니다.
/// 파일을 열었을 때 "지금 내가 고치려는 것"만 보이는 게 좋은 구조입니다.
///
/// 프로젝트의 Enemy.cs / Enemy.Debuffs.cs 와 같은 방식이에요.
/// 컴파일하면 완전히 같은 하나의 클래스라 성능 차이는 전혀 없습니다.
///
/// [일시정지 처리]
/// Time.timeScale = 0 으로 게임을 멈춥니다.
/// 그래서 모든 연출은 Time.deltaTime 이 아니라 Time.unscaledDeltaTime 을 씁니다.
/// (deltaTime 을 쓰면 0이 곱해져 연출이 영원히 멈춥니다)
/// </summary>
public partial class AugmentSelectUI : MonoBehaviour
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

    [Header("폰트")]
    [Tooltip("한글 글리프가 포함된 TMP Font Asset. " +
             "비워두면 TMP Settings 의 Default Font Asset 을 사용합니다. " +
             "한글이 □ 로 깨진다면 여기에 한글 폰트를 넣으세요.")]
    [SerializeField] private TMP_FontAsset uiFont;

    [Header("문구")]
    [SerializeField] private string titleText    = "증강 선택";
    [SerializeField] private string subtitleText = "하나를 선택하세요";

    [Header("자동 선택")]
    [Tooltip("이 시간 안에 고르지 않으면 자동으로 하나가 선택됩니다. 0 이하면 자동 선택 없음")]
    [SerializeField] private float autoPickSeconds = 15f;

    [Header("게임 일시정지")]
    [Tooltip("창이 떠 있는 동안 Time.timeScale 을 0으로 만듭니다")]
    [SerializeField] private bool pauseGame = true;

    [Header("색상")]
    [SerializeField] private Color dimColor       = new Color(0f, 0f, 0f, 0.82f);
    [SerializeField] private Color cardBackColor  = new Color(0.10f, 0.12f, 0.16f, 1f);
    [SerializeField] private Color nameColor      = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color descColor      = new Color(0.72f, 0.77f, 0.85f, 1f);
    [SerializeField] private Color timerFillColor = new Color(1f, 0.82f, 0.30f, 1f);

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
    //  런타임 상태
    //  (Build / Anim 파일에서도 같은 클래스이므로 그대로 접근합니다)
    // ─────────────────────────────────────────────────────────
    private Canvas          canvas;
    private CanvasGroup     rootGroup;
    private RectTransform   cardArea;
    private Image           timerFill;
    private TextMeshProUGUI timerLabel;

    private readonly List<GameObject> spawnedCards = new List<GameObject>();

    private Action<AugmentCard> onChosen;
    private bool      isOpen;
    private bool      picked;
    private float     savedTimeScale = 1f;
    private Coroutine timerRoutine;

    // ─────────────────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        // 씬에 이 컴포넌트가 둘 이상이면 각자 자기 캔버스를 만들어
        // 카드가 겹쳐 보이거나 두 배로 보입니다. 조용히 넘어가면 원인 찾기가 아주 어려워서
        // 시작할 때 한 번 경고를 남깁니다.
        //
        // FindObjectsByType 은 비용이 큰 함수라 Awake 에서 딱 한 번만 부릅니다.
        var all = FindObjectsByType<AugmentSelectUI>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Debug.LogError(
                $"[AugmentSelectUI] 씬에 {all.Length}개가 있습니다. 하나만 남기세요.\n" +
                $"  첫 번째: {all[0].name} / 두 번째: {all[1].name}", this);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────────────────────────

    /// <summary>카드 목록을 보여주고, 선택되면 콜백을 부릅니다.</summary>
    public void Show(List<AugmentCard> cards, Action<AugmentCard> callback)
    {
        if (isOpen) return;                            // 중복 오픈 방지
        if (cards == null || cards.Count == 0) return;

        onChosen = callback;
        picked   = false;
        isOpen   = true;

        EnsureBuilt();      // → Build.cs
        BuildCards(cards);  // → Build.cs

        canvas.gameObject.SetActive(true);

        if (pauseGame)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (audioSource != null && openSfx != null) audioSource.PlayOneShot(openSfx);

        StartCoroutine(PlayIntro());   // → Anim.cs

        if (autoPickSeconds > 0f)
            timerRoutine = StartCoroutine(AutoPickCountdown(cards));   // → Anim.cs
        else
            SetTimerVisible(false);
    }

    /// <summary>강제로 닫기 (선택 없이). 보통 쓸 일 없습니다.</summary>
    public void ForceClose() => Close();

    // ─────────────────────────────────────────────────────────
    //  선택 / 닫기
    // ─────────────────────────────────────────────────────────

    /// <summary>카드 버튼 클릭 또는 자동선택 타이머 만료 시 진입점.</summary>
    private void OnClickCard(AugmentCard card, GameObject cardGo)
    {
        if (picked) return;      // 연타로 두 장 먹는 사고 방지
        picked = true;

        if (timerRoutine != null) { StopCoroutine(timerRoutine); timerRoutine = null; }
        if (audioSource != null && pickSfx != null) audioSource.PlayOneShot(pickSfx);

        StartCoroutine(PlayPickAndClose(card, cardGo));   // → Anim.cs
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

    /// <summary>
    /// 창이 열린 상태로 씬이 바뀌거나 오브젝트가 꺼지면
    /// timeScale 이 0인 채로 남아 게임 전체가 멈춥니다. 그걸 막는 안전장치입니다.
    /// </summary>
    private void OnDisable()
    {
        if (isOpen && pauseGame) Time.timeScale = savedTimeScale;
        isOpen = false;
    }

    // ─────────────────────────────────────────────────────────
    //  공용 헬퍼
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
}
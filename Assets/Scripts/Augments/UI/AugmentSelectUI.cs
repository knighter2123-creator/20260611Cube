using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>증강 카드 선택창(흐름): Show 로 카드를 만들고 timeScale 0 으로 멈춘 뒤, 고르거나 자동 선택 타이머가 끝나면 연출 후 닫고 콜백 — 연출은 unscaledDeltaTime 사용.</summary>
// partial 구성: 이 파일(언제 열고 닫나) / .Build.cs(캔버스·카드 생성) / .Anim.cs(등장·선택 연출, 타이머)
public partial class AugmentSelectUI : MonoBehaviour
{
    public enum LayoutMode
    {
        Horizontal,  // 카드 3장을 가로로 (TFT 스타일. 가로 화면·태블릿에 적합)
        Vertical     // 카드 3장을 세로로 (세로 모바일에서 글자가 잘 보임)
    }

    // ── 인스펙터 설정 ──
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

    // ── 런타임 상태 (Build / Anim 파일도 같은 클래스라 그대로 접근) ──
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
    private Coroutine introRoutine;
    private Coroutine timerRoutine;

    // ── 라이프사이클 ──

    private void Awake()
    {
        // 둘 이상이면 캔버스가 겹쳐 카드가 두 배로 보이므로 경고 (FindObjectsByType 은 비싸서 Awake 에서 한 번만)
        var all = FindObjectsByType<AugmentSelectUI>(FindObjectsSortMode.None);
        if (all.Length > 1)
        {
            Debug.LogError(
                $"[AugmentSelectUI] 씬에 {all.Length}개가 있습니다. 하나만 남기세요.\n" +
                $"  첫 번째: {all[0].name} / 두 번째: {all[1].name}", this);
        }
    }

    // ── 공개 API ──

    /// <summary>카드 목록을 보여주고, 선택되면 콜백을 부릅니다.</summary>
    public void Show(List<AugmentCard> cards, Action<AugmentCard> callback)
    {
        // 순서: 거르기 → 화면 만들기 → 열림 확정 → 일시정지 → 연출·타이머 / 선택 → PlayPickAndClose → Close → 콜백

        if (isOpen)
        {
            // 조용히 무시하면 카드가 안 뜨는 원인을 못 찾아 로그
            Debug.LogWarning("[AugmentSelectUI] 이미 창이 열려 있어 새 요청을 무시합니다.", this);
            return;
        }
        if (cards == null || cards.Count == 0) return;

        // 화면 만들기(실패 가능)를 먼저, 성공한 뒤 isOpen — 반대면 예외 때 isOpen 이 남아 창이 다시는 안 열림
        EnsureBuilt();      // → Build.cs
        BuildCards(cards);  // → Build.cs

        onChosen = callback;
        picked   = false;
        isOpen   = true;

        canvas.gameObject.SetActive(true);

        if (pauseGame)
        {
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (audioSource != null && openSfx != null) audioSource.PlayOneShot(openSfx);

        introRoutine = StartCoroutine(PlayIntro());   // → Anim.cs

        if (autoPickSeconds > 0f)
            timerRoutine = StartCoroutine(AutoPickCountdown(cards));   // → Anim.cs
        else
            SetTimerVisible(false);
    }


    // ── 선택 / 닫기 ──

    /// <summary>카드 버튼 클릭 또는 자동선택 타이머 만료 시 진입점.</summary>
    private void OnClickCard(AugmentCard card, GameObject cardGo)
    {
        if (picked) return;      // 연타로 두 장 먹는 사고 방지
        picked = true;

        // 등장 연출 중이면 두 코루틴이 alpha/scale 을 번갈아 덮으므로 등장을 끊고 최종 상태로 확정
        StopIntro();
        StopTimer();
        if (audioSource != null && pickSfx != null) audioSource.PlayOneShot(pickSfx);
        HapticManager.PlayHeavy();   // 증강 카드 획득 진동

        StartCoroutine(PlayPickAndClose(card, cardGo));   // → Anim.cs
    }

    private void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        StopIntro();
        StopTimer();

        if (pauseGame) Time.timeScale = savedTimeScale;

        if (canvas != null)
        {
            rootGroup.alpha = 1f;
            canvas.gameObject.SetActive(false);
        }
    }

    /// <summary>열린 채 씬이 바뀌거나 꺼져도 timeScale 0 이 남아 게임이 멈추지 않게 복구하는 안전장치.</summary>
    private void OnDisable()
    {
        if (isOpen && pauseGame) Time.timeScale = savedTimeScale;
        isOpen = false;

        // 꺼지면 코루틴은 유니티가 조용히 멈춥니다. 핸들만 비워 다음 Show 에서 꼬이지 않게 합니다.
        introRoutine = null;
        timerRoutine = null;
    }

    private void StopTimer()
    {
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = null;
    }

    /// <summary>등장 연출을 끊고, 끝났을 때와 같은 상태(완전 불투명 · 원래 크기)로 확정합니다.</summary>
    private void StopIntro()
    {
        if (introRoutine == null) return;

        StopCoroutine(introRoutine);
        introRoutine = null;

        if (rootGroup != null) rootGroup.alpha = 1f;
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null) continue;
            spawnedCards[i].transform.localScale = Vector3.one;
            var cg = spawnedCards[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
    }

    // ── 공용 헬퍼 ──

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
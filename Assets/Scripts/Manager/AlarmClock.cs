using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>플레이 시간 알림: 게임 씬 진입부터 실제 시간으로 세어 5분마다 "플레이 시간 N분" 팝업을 10초간 표시 — 끌 수 없고 클릭도 막지 않음, 팝업 캔버스는 코드로 만들어 씬과 무관하게 살아남음.</summary>
// [붙이는 위치] 로그인 씬 ManagerRoot 하위 (DontDestroyOnLoad 는 ManagerRoot 담당)
public class AlarmClock : MonoBehaviour
{
    public static AlarmClock Instance { get; private set; }

    [Header("알림 주기")]
    [Tooltip("알림 간격(초). 기본 300초 = 5분.")]
    [SerializeField] private float intervalSeconds = 300f;

    [Tooltip("팝업이 화면에 머무는 전체 시간(초). 페이드 인/아웃 포함.")]
    [SerializeField] private float showSeconds = 10f;

    [SerializeField] private float fadeIn  = 0.25f;
    [SerializeField] private float fadeOut = 0.35f;

    [Header("팝업 UI")]
    [Tooltip("한글이 들어 있는 TMP 폰트를 넣으세요. 비우면 TMP 기본 폰트(한글 없음 → □ 표시)를 씁니다.")]
    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private float fontSize = 40f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.75f);

    [Tooltip("다른 UI 위에 뜨도록 높게 둔다.")]
    [SerializeField] private int sortingOrder = 1000;

    // ── 런타임 ──────────────────────────────
    private float elapsed;          // 게임 시작 후 누적 플레이 시간(초)
    private int   notifiedCount;    // 지금까지 띄운 알림 수

    private CanvasGroup     popupGroup;
    private TextMeshProUGUI popupText;
    private Coroutine       popupRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        BuildPopup();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsGameplayStarted()) return;

        // unscaled 실제 시간 — 백그라운드에서 돌아온 프레임은 delta 가 튀므로 1초로 자름 (백그라운드 시간은 제외)
        elapsed += Mathf.Min(Time.unscaledDeltaTime, 1f);

        if (elapsed >= (notifiedCount + 1) * intervalSeconds)
        {
            notifiedCount++;
            ShowPopup($"게임 플레이 시간 {FormatMinutes(notifiedCount * intervalSeconds)}이 지났습니다.");
        }
    }

    private static bool IsGameplayStarted()
        => SaveManager.Instance != null && SaveManager.Instance.IsGameplayStarted;

    /// <summary>300 → "5분", 3600 → "1시간", 3900 → "1시간 5분"</summary>
    private static string FormatMinutes(float seconds)
    {
        int totalMin = Mathf.RoundToInt(seconds / 60f);
        int h = totalMin / 60;
        int m = totalMin % 60;

        if (h == 0) return $"{m}분";
        if (m == 0) return $"{h}시간";
        return $"{h}시간 {m}분";
    }

    // ── 팝업 표시 ──────────────────────────────

    private void ShowPopup(string message)
    {
        // 이전 팝업이 남아 있으면 새 알림으로 교체 (간격이 10초보다 짧게 설정된 경우 대비)
        if (popupRoutine != null) StopCoroutine(popupRoutine);
        popupRoutine = StartCoroutine(PopupRoutine(message));
    }

    private IEnumerator PopupRoutine(string message)
    {
        popupText.text = message;
        popupGroup.gameObject.SetActive(true);

        yield return Fade(0f, 1f, fadeIn);
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, showSeconds - fadeIn - fadeOut));
        yield return Fade(1f, 0f, fadeOut);

        popupGroup.gameObject.SetActive(false);
        popupRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            popupGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        popupGroup.alpha = to;
    }

    // ── 팝업 UI 생성 ──────────────────────────────

    private void BuildPopup()
    {
        // 캔버스 (프로젝트 다른 씬과 같은 1920x1080 기준)
        var canvasGo = new GameObject("AlarmClockCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // 패널 (화면 상단 중앙)
        var panelGo = new GameObject("AlarmPopup", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);

        var panelRt = (RectTransform)panelGo.transform;
        panelRt.anchorMin        = panelRt.anchorMax = new Vector2(0.5f, 1f);
        panelRt.pivot            = new Vector2(0.5f, 1f);
        panelRt.anchoredPosition = new Vector2(0f, -60f);
        panelRt.sizeDelta        = new Vector2(900f, 110f);

        var panelImage = panelGo.GetComponent<Image>();
        panelImage.color         = panelColor;
        panelImage.raycastTarget = false;

        popupGroup = panelGo.GetComponent<CanvasGroup>();
        popupGroup.alpha          = 0f;
        popupGroup.interactable   = false;
        popupGroup.blocksRaycasts = false;   // 클릭으로 닫을 수 없고, 아래 UI 조작도 막지 않음

        // 텍스트
        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panelGo.transform, false);

        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(24f, 0f);
        textRt.offsetMax = new Vector2(-24f, 0f);

        popupText = textGo.GetComponent<TextMeshProUGUI>();
        if (font != null) popupText.font = font;
        popupText.fontSize      = fontSize;
        popupText.color         = textColor;
        popupText.alignment     = TextAlignmentOptions.Center;
        popupText.raycastTarget = false;

        panelGo.SetActive(false);
    }
}

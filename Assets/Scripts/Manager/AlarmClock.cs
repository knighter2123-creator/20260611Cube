using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이 시간 알림. 게임 시작 후 5분마다 "게임 플레이 시간 N분이 지났습니다." 팝업을 띄우고 10초 뒤 사라진다.
///
/// [붙이는 위치]
///   로그인 씬의 ManagerRoot 하위. 다른 매니저들과 같은 DontDestroyOnLoad 싱글턴이다.
///   (DontDestroyOnLoad 는 ManagerRoot 가 담당 — 여기서 부르지 않는다)
///
/// [언제부터 세는가]
///   SaveManager.IsGameplayStarted 가 true 가 된 순간(로그인/로딩이 아닌 게임 씬에 처음 들어간 순간)부터.
///
/// [플레이어가 끌 수 없음]
///   설정 토글이 없고, 팝업은 클릭을 받지 않는다(blocksRaycasts = false). 그래서 닫을 수도 없고,
///   팝업 아래 버튼 조작도 막지 않는다. 10초가 지나면 저절로 사라진다.
///
/// [팝업 UI]
///   씬마다 배치할 필요가 없도록 자기 자식으로 Overlay 캔버스를 코드에서 만든다.
///   씬이 바뀌어도(가챠/상점 등) 같이 살아남고, sortingOrder 가 높아 다른 UI 위에 뜬다.
/// </summary>
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

        // ★ unscaled — 배속 버튼(timeScale)이나 일시정지에 영향받지 않는 "실제 플레이 시간".
        //   앱이 백그라운드에서 돌아온 첫 프레임은 delta 가 수 분짜리로 튈 수 있어 1초로 자른다.
        //   (백그라운드에 있던 시간은 플레이 시간으로 치지 않는다)
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

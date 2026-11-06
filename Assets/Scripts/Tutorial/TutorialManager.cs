using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>튜토리얼 진행(MainScene 씬 오브젝트 — HUD·타워를 직접 참조해야 해서 DDOL 아님) — 신규 유저면 1프레임 뒤 Begin → timeScale 0 → 단계 진행 → 완료 기록 → 배속 복원 (강조 계산은 .Highlight).</summary>
public partial class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>"지금 튜토리얼 중인가?" — 설정 패널 등 다른 스크립트가 물어볼 때 사용.</summary>
    public static bool IsRunning => Instance != null && Instance.running;

    [Header("UI (튜토리얼 캔버스)")]
    [Tooltip("화면 전체로 늘린 루트. 평소엔 꺼집니다.\n★ 이 TutorialManager 오브젝트는 이 루트 밖에 있어야 합니다 (꺼지면 코루틴이 멈춤).")]
    [SerializeField] private RectTransform overlayRoot;
    [Tooltip("설명 박스. overlayRoot 의 '직계 자식'이어야 합니다.")]
    [SerializeField] private RectTransform messageBox;
    [SerializeField] private TMP_Text messageText;
    [Tooltip("선택: '3 / 10' 같은 진행도")]
    [SerializeField] private TMP_Text pageText;
    [Tooltip("선택: '화면을 터치하면 다음으로'")]
    [SerializeField] private TMP_Text hintText;
    [Tooltip("화면 전체를 덮는 투명 버튼. 어디를 눌러도 다음 단계로.")]
    [SerializeField] private Button nextArea;
    [SerializeField] private Button skipButton;

    [Header("단계 (위에서부터 순서대로)")]
    [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

    [Header("동작")]
    [SerializeField] private bool autoShowOnFirstLogin = true;
    [SerializeField] private float fadeDuration = 0.2f;
    [Tooltip("연타로 여러 단계가 한 번에 넘어가는 것 방지 (초, 실제 시간 기준)")]
    [SerializeField] private float tapCooldown = 0.25f;

    private CanvasGroup group;
    private bool refsOk;
    private bool running;
    private bool escapeRegistered;
    private bool warnedTimeScale;
    private int index;
    private float lastStepTime;
    private Coroutine fadeRoutine;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[Tutorial] 중복 인스턴스 발견 → '{name}' 를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        // 도중에 씬을 떠나면 timeScale 1 고정, 완료 기록은 안 함 (다음에 다시 뜨게)
        if (running)
        {
            running = false;
            UnregisterEscape();
            Time.timeScale = 1f;
        }
    }

    // ─────────────────────────────────────────────
    private void Start()
    {
        refsOk = ValidateReferences();
        if (!refsOk) return;

        // TryGetComponent 사용 (GetComponent 결과의 '가짜 null' 은 ?? 를 통과함)
        if (!overlayRoot.TryGetComponent(out group))
            group = overlayRoot.gameObject.AddComponent<CanvasGroup>();

        EnsureMessageBoxFitsText();     // ★ 추가 — 박스 크기 = 실제 글자 크기

        BuildHighlightLayer();          // Highlight.cs — 어두운 조각 4개 + 테두리 4개를 코드로 생성
        DisableRaycastInMessageBox();   // 설명 박스를 눌러도 '다음'이 되도록

        overlayRoot.gameObject.SetActive(false);

        OnClickListener();

        if (autoShowOnFirstLogin && TutorialProgress.ShouldAutoShow)
            StartCoroutine(BeginNextFrame());
    }

    private void OnClickListener()
    {
        nextArea.onClick.RemoveAllListeners();
        skipButton.onClick.RemoveAllListeners();
        nextArea.onClick.AddListener(OnNextClicked);
        skipButton.onClick.AddListener(Skip);
    }

    /// <summary>한 프레임 뒤 Begin — 다른 Start 의 배속 복원이 timeScale 0 을 덮어쓰지 않고 HUD 레이아웃도 계산된 뒤라서.</summary>
    private IEnumerator BeginNextFrame()
    {
        yield return null;
        Begin();
    }

    /// <summary>Message Box 가 TMP 자체면 ContentSizeFitter(세로 Preferred) 부착 — 글자가 rect 밖으로 넘쳐 위치 계산과 보이는 크기가 달라지는 것 방지.</summary>
    // ⚠ 배경 패널 구조라면 패널에 LayoutGroup + ContentSizeFitter 를 직접 달 것
    private void EnsureMessageBoxFitsText()
    {
        if (!messageBox.TryGetComponent(out TMP_Text _)) return;            // 박스가 텍스트가 아니면 건드리지 않음
        if (messageBox.TryGetComponent(out ContentSizeFitter _)) return;    // 이미 있으면 사용자 설정 존중

        var fitter = messageBox.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;     // 가로 폭은 인스펙터 값 유지 (자동 줄바꿈 기준)
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;     // 세로는 줄 수에 맞춤

        GameLog.Info("[Tutorial] Message Box 가 텍스트라서 ContentSizeFitter(세로 Preferred)를 자동으로 붙였습니다. " +
                  "에디터에서 직접 붙여 두면 이 로그는 사라집니다.", messageBox);
    }

    private bool ValidateReferences(bool ok = true)
    {
        if (overlayRoot == null) { Debug.LogError("[Tutorial] overlayRoot 미할당", this); ok = false; }
        if (messageBox  == null) { Debug.LogError("[Tutorial] messageBox 미할당", this);  ok = false; }
        if (messageText == null) { Debug.LogError("[Tutorial] messageText 미할당", this); ok = false; }
        if (nextArea    == null) { Debug.LogError("[Tutorial] nextArea 미할당", this);    ok = false; }
        if (skipButton  == null) { Debug.LogError("[Tutorial] skipButton 미할당", this);  ok = false; }
        if (!ok) return false;

        if (transform.IsChildOf(overlayRoot))
        {
            Debug.LogError("[Tutorial] TutorialManager 가 overlayRoot 안에 있습니다. " +
                           "overlayRoot 를 끄면 코루틴(페이드)이 멈춥니다. 바깥 오브젝트로 옮기세요.", this);
            ok = false;
        }
        if (messageBox.parent != overlayRoot)
        {
            Debug.LogError("[Tutorial] messageBox 는 overlayRoot 의 직계 자식이어야 위치 계산이 맞습니다.", this);
            ok = false;
        }
        if (steps.Count == 0)
            Debug.LogWarning("[Tutorial] 단계가 비어 있습니다. 컴포넌트 ⋮ 메뉴 → '기본 단계로 초기화'를 눌러 보세요.", this);

        return ok;
    }

    // ── 시작 / 진행 / 종료 ──

    /// <summary>튜토리얼 시작. 자동 팝업과 설정 패널의 '튜토리얼' 버튼이 모두 이걸 부른다.</summary>
    public void Begin()
    {
        if (!refsOk) { Debug.LogWarning("[Tutorial] 참조가 비어 있어 시작하지 않습니다.", this); return; }
        if (running) return;
        if (steps.Count == 0) { Debug.LogWarning("[Tutorial] 단계가 없습니다.", this); return; }

        StopFade();   // 직전 페이드아웃이 아직 도는 중이면 끊는다 (끝나고 SetActive(false) 되는 것 방지)

        running = true;
        warnedTimeScale = false;

        overlayRoot.gameObject.SetActive(true);
        group.alpha = 0f;
        group.blocksRaycasts = true;   // 튜토리얼 중엔 뒤의 HUD 버튼이 눌리지 않게
        group.interactable = true;

        Time.timeScale = 0f;           // ★ 짝이 되는 복원 지점: Finish() 의 RestoreGameSpeed()
        RegisterEscape();

        ShowStep(0, snap: true);
        fadeRoutine = StartCoroutine(Fade(0f, 1f, null));
    }

    private void OnNextClicked()
    {
        if (!running) return;
        if (Time.unscaledTime - lastStepTime < tapCooldown) return;

        if (index + 1 >= steps.Count) Finish();
        else                          ShowStep(index + 1, snap: false);
    }

    public void Skip()
    {
        if (!running) return;
        GameLog.Info($"[Tutorial] 스킵 ({index + 1}/{steps.Count} 단계에서)");
        Finish();
    }

    private void Finish()
    {
        if (!running) return;
        running = false;

        TutorialProgress.MarkDone();   // 끝까지 봤든 스킵했든 "1회 노출"은 끝

        // ESC 핸들러 안에서 불려도 해제 안전 (UpdateEscape 가 역순 for 로 순회)
        UnregisterEscape();

        group.blocksRaycasts = false;  // 페이드아웃 중에도 바로 HUD 를 누를 수 있게
        group.interactable = false;

        RestoreGameSpeed();

        StopFade();
        fadeRoutine = StartCoroutine(Fade(group.alpha, 0f, () => overlayRoot.gameObject.SetActive(false)));
    }

    /// <summary>1f 고정 대신 저장된 배속 복원 (2/3배속 유저가 튜토리얼 재시청 후 1배속으로 떨어지지 않게).</summary>
    private void RestoreGameSpeed()
    {
        if (GameSpeedManager.Instance != null)
            GameSpeedManager.Instance.ReapplySpeed();
        else
            Time.timeScale = 1f;
    }

    private void ShowStep(int i, bool snap)
    {
        index = i;
        lastStepTime = Time.unscaledTime;

        TutorialStep step = steps[i];
        PrepareTarget(step);   // Highlight.cs — 렌더러/카메라를 단계마다 한 번만 찾아 둔다

        messageText.text = step.message;
        if (pageText != null) pageText.text = $"{i + 1} / {steps.Count}";
        if (hintText != null)
            hintText.text = (i == steps.Count - 1) ? "화면을 터치하면 게임을 시작합니다"
                                                   : "화면을 터치하면 다음으로";

        // 글자 수가 바뀌면 박스 크기가 바뀌므로, 위치 계산 전에 레이아웃을 즉시 다시 계산
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageBox);

        if (snap) SnapHighlight();
    }

    // ─────────────────────────────────────────────
    private void LateUpdate()
    {
        // Update/LateUpdate 는 timeScale 0 에서도 매 프레임 호출됩니다. (멈추는 건 deltaTime·물리·WaitForSeconds)
        if (!running) return;

        // 방어 코드: 튜토리얼 중 다른 곳(배속 매니저, 팝업 닫기 등)이 timeScale 을 올리면 다시 멈춘다.
        if (Time.timeScale != 0f)
        {
            if (!warnedTimeScale)
            {
                Debug.LogWarning($"[Tutorial] 튜토리얼 중 timeScale 이 {Time.timeScale} 로 바뀌어 다시 0으로 맞춥니다. " +
                                 "어떤 스크립트가 바꿨는지 확인해 보세요.", this);
                warnedTimeScale = true;
            }
            Time.timeScale = 0f;
        }

        // LateUpdate 에서 하는 이유: 월드 오브젝트 이동(Update)과 UI 레이아웃이 끝난 뒤의 위치를 쓰기 위해
        UpdateHighlight(Time.unscaledDeltaTime);
    }

    private IEnumerator Fade(float from, float to, System.Action onDone)
    {
        // timeScale 이 0 이라 Time.deltaTime 은 0 → 반드시 unscaledDeltaTime 을 써야 움직인다
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        group.alpha = to;
        fadeRoutine = null;
        onDone?.Invoke();
    }

    private void StopFade()
    {
        if (fadeRoutine == null) return;
        StopCoroutine(fadeRoutine);
        fadeRoutine = null;
    }

    // ── 뒤로가기(ESC) — 튜토리얼 중엔 스킵으로 처리 ──

    private void RegisterEscape()
    {
        if (escapeRegistered || GameManager.Instance == null) return;
        GameManager.Instance.RegisterEscapeHandler(OnEscape);
        escapeRegistered = true;
    }

    private void UnregisterEscape()
    {
        if (!escapeRegistered) return;
        GameManager.Instance?.UnregisterEscapeHandler(OnEscape);
        escapeRegistered = false;
    }

    private bool OnEscape()
    {
        if (!running) return false;   // 튜토리얼 중이 아니면 다른 핸들러에게 넘긴다
        Skip();
        return true;
    }

    // ── 기본 단계 채우기 (문구는 인스펙터에서 자유롭게 수정) ──

    // Reset 은 컴포넌트를 처음 붙이거나 ⋮ → Reset 을 눌렀을 때 에디터에서 호출됩니다.
    private void Reset() => FillDefaultSteps();

    [ContextMenu("기본 단계로 초기화 (타겟 연결도 지워짐)")]
    private void FillDefaultSteps()
    {
        steps = new List<TutorialStep>
        {
            new TutorialStep { label = "0. 시작", targetType = TutorialTargetType.None,
                message = "타워 키우기의 세계에 온 것을 환영합니다!\n튜토리얼을 진행하고 게임에 진입하세요." },
            new TutorialStep { label = "1. 플레이어 타워", targetType = TutorialTargetType.World,
                message = "이 타워는 플레이어, 당신입니다." },
            new TutorialStep { label = "2. 공격", targetType = TutorialTargetType.PlayerRange, padding = 0f,
                message = "타워는 사거리 안에 들어온 몬스터를 자동으로 공격합니다." },
            new TutorialStep { label = "3. 몬스터 생성", targetType = TutorialTargetType.World,
                message = "몬스터는 이곳에서 생성되어 타워를 향해 다가옵니다." },
            new TutorialStep { label = "4. 스테이지 / 타이머", targetType = TutorialTargetType.UI,
                message = "현재 스테이지와 남은 시간입니다.\n제한 시간 안에 몬스터를 처치하세요." },
            new TutorialStep { label = "5. 메뉴 - 탭 창", targetType = TutorialTargetType.UI,
                message = "이 버튼으로 강화 / 스탯 / 각성 / 동료 / 도감 창을 열 수 있습니다." },
            new TutorialStep { label = "6. 메뉴 - 미션", targetType = TutorialTargetType.UI,
                message = "가이드 미션과 일일 미션을 완료하고 보상을 받으세요." },
            new TutorialStep { label = "7. 메뉴 - 상점", targetType = TutorialTargetType.UI,
                message = "상점에서 다양한 상품을 구매할 수 있습니다." },
            new TutorialStep { label = "8. 메뉴 - 배속", targetType = TutorialTargetType.UI,
                message = "게임 속도를 바꿀 수 있습니다." },
            new TutorialStep { label = "9. 메뉴 - 설정", targetType = TutorialTargetType.UI,
                message = "설정에서 효과와 진동을 조절하고, 이 튜토리얼을 다시 볼 수 있습니다." },
            new TutorialStep { label = "10. 마무리", targetType = TutorialTargetType.None,
                message = "준비가 끝났습니다!\n몬스터를 물리치고 더 높은 스테이지에 도전하세요." },
        };
    }

    [ContextMenu("테스트: 신규 유저 상태로 되돌리기")]
    private void DebugResetProgress()
    {
        TutorialProgress.ResetForDebug();
        Debug.Log("[Tutorial] SaveData.tutorialDone = false 로 저장했습니다. MainScene 에 다시 들어오면 자동으로 뜹니다.");
    }
}

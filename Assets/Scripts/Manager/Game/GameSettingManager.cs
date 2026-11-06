using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>설정 패널 주인: 열기/닫기·버튼·배속 복원·뒤로가기 처리 (블룸/진동 바인딩은 .Bindings.cs, 계정 삭제 버튼·팝업은 .AccountReset.cs).</summary>
public partial class GameSettingManager : MonoBehaviour
{
    public static GameSettingManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button returnToLoginButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitGameButton;

    [Tooltip("★ 튜토리얼 다시 보기 버튼 (설정 패널 안). 비워 두면 기능만 꺼지고 에러는 나지 않습니다.")]
    [SerializeField] private Button tutorialButton;

    [Header("디버그")]
    [Tooltip("패널이 열리고 닫힐 때 '무엇이 눌렸는지'를 콘솔에 찍습니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logInteractions = true;

    private const string LOGIN_SCENE = "LoginScene";

    public bool IsOpen { get; private set; }

    private int  lastChangeFrame = -1;
    private bool refsOk;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[Setting] 중복 인스턴스 발견 → '{name}' 를 제거합니다.", this);
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 뒤로가기(ESC) ──

    private void OnEnable()
    {
        // ⚠ GameManager 가 없으면 재시도 없이 등록이 빠짐 — Script Execution Order 에서 GameManager 를 -90 정도로 (SaveManager -100 다음)
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterEscapeHandler(OnEscape);
        else
            Debug.LogError("[Setting] GameManager.Instance 가 없어 뒤로가기를 등록하지 못했습니다.");
    }

    private void OnDisable()
    {
        GameManager.Instance?.UnregisterEscapeHandler(OnEscape);

        // 씬을 떠나는 길이라 배속 복원이 아닌 1f 고정 (SceneLoader 도 1f)
        if (IsOpen)
        {
            UnbindAll();
            BloomController.Instance?.Pop();   // Open 의 Push 와 짝. 빼먹으면 refCount 가 남는다
            IsOpen = false;
            Time.timeScale = 1f;
        }
    }

    private bool OnEscape()
    {
        // 튜토리얼 중이면 스킵 처리 (보통은 튜토리얼 핸들러가 먼저 받음 — 등록 순서가 꼬였을 때의 안전망)
        if (TutorialManager.IsRunning)
        {
            TutorialManager.Instance.Skip();
            return true;
        }

        // 계정 초기화 중엔 삼킴 (넘기면 씬을 내리는 도중 엉뚱한 창이 뜸)
        if (AccountReset.IsRunning) return true;

        if (!refsOk) return false;   // 아직 준비 안 됐으면 다른 핸들러에게 넘긴다

        // 삭제 확인 팝업이 떠 있으면 팝업만 닫기 (뒤로가기는 맨 위 하나만 닫는 안드로이드 관례)
        if (IsDeleteConfirmOpen)
        {
            HideDeleteConfirm();
            return true;
        }

        ToggleSettings();
        return true;
    }

    // ─────────────────────────────────────────────
    private void Start()
    {
        refsOk = ValidateReferences();
        if (!refsOk) return;

        IsOpen = false;
        settingsPanel.SetActive(false);
        RestoreGameSpeed();

        // 인스펙터 On Click 에 걸어둔 것은 이걸로 안 지워집니다. 직접 비우세요.
        openSettingsButton.onClick.RemoveAllListeners();
        resumeButton.onClick.RemoveAllListeners();
        returnToLoginButton.onClick.RemoveAllListeners();
        quitGameButton.onClick.RemoveAllListeners();

        openSettingsButton.onClick.AddListener(Open);
        resumeButton.onClick.AddListener(Close);
        returnToLoginButton.onClick.AddListener(ReturnToLogin);
        quitGameButton.onClick.AddListener(QuitGame);

        // ★ 튜토리얼 버튼은 선택 항목: 없어도 설정 패널 자체는 정상 동작해야 하므로 refsOk 에 넣지 않는다
        if (tutorialButton != null)
        {
            tutorialButton.onClick.RemoveAllListeners();
            tutorialButton.onClick.AddListener(OpenTutorial);
        }
        else
        {
            Debug.LogWarning("[Setting] tutorialButton 미할당 — '튜토리얼 다시 보기'가 비활성화됩니다.", this);
        }

        // ★ [계정 삭제] 튜토리얼 버튼과 같은 이유로 선택 항목 — refsOk 에 넣지 않는다 (AccountReset.cs 파일)
        SetupAccountReset();
    }

    private bool ValidateReferences()
    {
        bool ok = true;

        if (settingsPanel == null)       { Debug.LogError("[Setting] settingsPanel 미할당", this);       ok = false; }
        if (openSettingsButton == null)  { Debug.LogError("[Setting] openSettingsButton 미할당", this);  ok = false; }
        if (resumeButton == null)        { Debug.LogError("[Setting] resumeButton 미할당", this);        ok = false; }
        if (returnToLoginButton == null) { Debug.LogError("[Setting] returnToLoginButton 미할당", this); ok = false; }
        if (quitGameButton == null)      { Debug.LogError("[Setting] quitGameButton 미할당", this);      ok = false; }

        return ok;
    }

    // ── 열기 / 닫기 ──

    public void Open()
    {
        if (IsOpen || !refsOk) return;
        if (TutorialManager.IsRunning) return;   // ★ 튜토리얼 위에 설정 패널이 겹쳐 열리지 않게
        if (AccountReset.IsRunning) return;      // ★ [계정 삭제] 씬을 내리는 도중에 다시 열리지 않게
        if (!GuardFrame("열기")) return;

        LogWhoClicked("열기");

        IsOpen = true;
        settingsPanel.SetActive(true);

        BindAll();          // 열 때마다 저장값으로 다시 맞춘다 (Bindings.cs)

        // 열린 동안 블룸 유지(안 그러면 토글 효과가 1.5초 반짝하고 끝남) — Push/Pop 참조 카운트라 다른 연출과 겹쳐도 안전
        BloomController.Instance?.Push();
    }

    public void Close()
    {
        if (!IsOpen || !refsOk) return;
        if (!GuardFrame("닫기")) return;

        LogWhoClicked("닫기");

        UnbindAll();        // 리스너 해제. 안 하면 열 때마다 쌓인다 (Bindings.cs)

        BloomController.Instance?.Pop();   // Open 의 Push 와 짝

        // 삭제 확인 팝업도 닫음 (다음에 열 때 떠 있지 않게)
        HideDeleteConfirm();

        IsOpen = false;
        settingsPanel.SetActive(false);

        RestoreGameSpeed();
    }

    /// <summary>게임 배속 복원(1f 고정 아님) — 1f 로 두면 배속 방치 중이던 플레이어가 설정을 열었다 닫기만 해도 1배속으로 떨어짐.</summary>
    private void RestoreGameSpeed()
    {
        if (GameSpeedManager.Instance != null)
            GameSpeedManager.Instance.ReapplySpeed();
        else
            Time.timeScale = 1f;
    }

    /// <summary>ESC 처럼 "반대로 전환"이 필요한 경우에만 쓰세요. 버튼에는 연결하지 마세요.</summary>
    public void ToggleSettings()
    {
        if (IsOpen) Close();
        else        Open();
    }

    private bool GuardFrame(string action)
    {
        if (Time.frameCount == lastChangeFrame)
        {
            Debug.LogWarning($"[Setting] 같은 프레임에 '{action}' 요청이 두 번 들어와 무시했습니다. " +
                             "버튼의 인스펙터 On Click 에 같은 함수가 중복 등록돼 있는지 확인하세요.", this);
            return false;
        }

        lastChangeFrame = Time.frameCount;
        return true;
    }

    private void LogWhoClicked(string action)
    {
        if (!logInteractions) return;

        GameObject go = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        GameLog.Info($"[Setting] {action} — 클릭된 오브젝트: {(go != null ? go.name : "(키보드 또는 코드 호출)")}", go);
    }

    // ── ★ 튜토리얼 다시 보기 (기존 유저용) ──

    /// <summary>튜토리얼 다시 보기: ① Close(리스너·블룸·배속 정리 — SetActive 만 하면 남음) → ② Begin(timeScale 0, 같은 프레임이라 게임은 흐르지 않음).</summary>
    private void OpenTutorial()
    {
        if (TutorialManager.Instance == null)
        {
            Debug.LogWarning("[Setting] 씬에 TutorialManager 가 없습니다.", this);
            return;
        }

        Close();
        if (IsOpen) return;   // GuardFrame 등으로 닫기가 거부됐다면 튜토리얼도 시작하지 않는다

        TutorialManager.Instance.Begin();
    }

    // ─────────────────────────────────────────────
    private void ReturnToLogin()
    {
        // 씬을 떠나는 길이므로 배속 복원이 아니라 1f 고정이 맞다.
        Time.timeScale = 1f;

        if (IsOpen)
        {
            UnbindAll();
            IsOpen = false;
        }

        HapticManager.Instance?.Cancel();

        // ClearRequests 가 refCount 를 0으로 밀어주므로 여기서는 Pop 이 따로 필요 없다.
        BloomController.Instance?.ClearRequests();

        SaveManager.Instance?.Save();           // ① stat 이 살아 있을 때 저장
        LevelUpManager.Instance?.ResetStat();   // ② 그 다음에 끊는다

        SceneManager.LoadScene(LOGIN_SCENE);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        HapticManager.Instance?.Cancel();

        SaveManager.Instance?.Save();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
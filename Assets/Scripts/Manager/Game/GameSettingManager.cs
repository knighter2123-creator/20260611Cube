using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 설정(OPTION) 패널의 주인.
/// 패널 열기/닫기, 버튼, timeScale, 뒤로가기를 전담한다.
///
/// 블룸/진동 슬라이더·토글 바인딩은 GameSettingManager.Bindings.cs 에 분리되어 있다.
/// </summary>
public partial class GameSettingManager : MonoBehaviour
{
    public static GameSettingManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button returnToLoginButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitGameButton;

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

    // ─────────────────────────────────────────────
    // 뒤로가기(ESC)
    // ─────────────────────────────────────────────

    private void OnEnable()
    {
        // ★ GameManager.Instance 가 null 이면 조용히 등록이 안 되고 다시 시도하지 않습니다.
        //   CompanionFragment 에서 봤던 것과 정확히 같은 함정입니다.
        //   MainScene 에서는 GameManager 가 ManagerRoot 로 이미 살아 있으니 안전하지만,
        //   실행 순서에 기대지 않도록 Script Execution Order 에서
        //   GameManager 를 -90 정도로 지정해 두세요. (SaveManager -100 다음)
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterEscapeHandler(OnEscape);
        else
            Debug.LogError("[Setting] GameManager.Instance 가 없어 뒤로가기를 등록하지 못했습니다.");
    }

    private void OnDisable()
    {
        GameManager.Instance?.UnregisterEscapeHandler(OnEscape);

        // 씬을 떠나는 길이므로 여기서는 배속 복원이 아니라 1f 고정이 맞다.
        // (SceneLoader.LoadScene 도 어차피 1f 로 맞춘다)
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
        if (!refsOk) return false;   // 아직 준비 안 됐으면 다른 핸들러에게 넘긴다

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

    // ─────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open()
    {
        if (IsOpen || !refsOk) return;
        if (!GuardFrame("열기")) return;

        LogWhoClicked("열기");

        IsOpen = true;
        settingsPanel.SetActive(true);

        BindAll();          // 열 때마다 저장값으로 다시 맞춘다 (Bindings.cs)

        // ★ 패널이 열려 있는 동안 블룸을 계속 켜 둔다.
        //   안 그러면 토글을 켜도 PulseFor(1.5초)만 반짝하고 꺼져서
        //   "토글이 아무 영향이 없다"로 보인다.
        //   Push/Pop 은 참조 카운트라 다른 연출과 겹쳐도 안전하다.
        BloomController.Instance?.Push();

        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!IsOpen || !refsOk) return;
        if (!GuardFrame("닫기")) return;

        LogWhoClicked("닫기");

        UnbindAll();        // 리스너 해제. 안 하면 열 때마다 쌓인다 (Bindings.cs)

        BloomController.Instance?.Pop();   // Open 의 Push 와 짝

        IsOpen = false;
        settingsPanel.SetActive(false);

        RestoreGameSpeed();
    }

    /// <summary>
    /// ★ 게임 배속 복원.
    ///
    ///   여기서 Time.timeScale = 1f 로 고정하면, 2배속/3배속으로 방치 중이던 플레이어가
    ///   설정을 한 번 열었다 닫는 것만으로 1배속으로 떨어집니다.
    ///   게임이 멈추지도 않고 에러도 없어서 "왜 갑자기 느려졌지?" 로만 체감됩니다.
    ///
    ///   SceneLoader.GoToStage() 가 가챠 씬을 닫을 때 같은 처리를 하고 있습니다.
    ///   ("가챠 닫을 때 1f로 고정하지 말고, 저장된 배속을 복원")
    ///   패널을 닫는 것도 "일시정지를 푸는 것"이지 "배속을 초기화하는 것"이 아니므로
    ///   같은 규칙을 따라야 합니다.
    ///
    ///   ★ timeScale 을 0으로 만드는 곳이 생기면 항상 짝이 되는 복원 지점을 찾아
    ///     '1f 고정'인지 '원래 값 복원'인지 먼저 판단하세요.
    /// </summary>
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
        Debug.Log($"[Setting] {action} — 클릭된 오브젝트: {(go != null ? go.name : "(키보드 또는 코드 호출)")}", go);
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
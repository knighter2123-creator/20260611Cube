using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 설정(OPTION) 패널의 주인.
/// 패널 열기/닫기, 버튼, timeScale, 뒤로가기를 전담한다.
///
/// 블룸/진동 슬라이더·토글 바인딩은 GameSettingsManager.Bindings.cs 에 분리되어 있다.
/// (이 프로젝트 관례대로 기능별 partial 분리)
///
/// ★ SettingsPanel.cs 는 삭제할 것. 이 스크립트와 패널·timeScale·ESC·버튼이 전부 겹친다.
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
        // 중복 인스턴스가 있으면 서로 다른 IsOpen 을 들고 패널을 번갈아 껐다 켭니다.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[Settings] 중복 인스턴스 발견 → '{name}' 를 제거합니다.", this);
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
    //
    // ★ 기존의 Update() 폴링을 GameManager 핸들러 등록으로 교체했습니다.
    //
    //   폴링 방식의 문제 : ESC를 감시하는 스크립트가 씬에 둘 이상이면
    //   한 번 눌렀는데 각자 반응합니다. SettingsPanel.cs 가 살아 있으면
    //   같은 프레임에 Open 과 Close 가 동시에 일어나 패널이 깜빡이고 맙니다.
    //
    //   핸들러 등록 방식은 "나중에 등록된 것이 먼저 처리하고, 처리했으면 멈춘다"라서
    //   증강 카드 팝업 같은 게 위에 떠 있으면 그게 먼저 뒤로가기를 먹습니다.
    //
    // ※ GameManager.Escape.cs 를 아직 프로젝트에 넣지 않았다면 여기서 컴파일 에러가 납니다.
    //   그 경우 이 두 메서드의 Register/Unregister 줄을 지우고,
    //   아래 주석 처리된 Update() 를 되살려 쓰세요.

    private void OnEnable()
    {
        GameManager.Instance?.RegisterEscapeHandler(OnEscape);
    }

    private void OnDisable()
    {
        GameManager.Instance?.UnregisterEscapeHandler(OnEscape);

        // 패널이 열린 채로 파괴/씬 전환되면 게임이 멈춘 채로 남습니다
        if (IsOpen)
        {
            UnbindAll();          // 리스너 정리 (Bindings.cs)
            Time.timeScale = 1f;
        }
    }

    /// <summary>뒤로가기가 눌렸을 때 GameManager 가 불러줍니다. true = 내가 처리했다.</summary>
    private bool OnEscape()
    {
        if (!refsOk) return false;   // 아직 준비 안 됐으면 다른 핸들러에게 넘긴다

        ToggleSettings();
        return true;
    }

    // GameManager.Escape.cs 가 없을 때 쓰는 폴백.
    // using UnityEngine.InputSystem; 을 상단에 추가해야 합니다.
    //
    // private void Update()
    // {
    //     if (!refsOk) return;
    //     if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
    //         ToggleSettings();
    // }

    // ─────────────────────────────────────────────
    private void Start()
    {
        refsOk = ValidateReferences();
        if (!refsOk) return;

        IsOpen = false;
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        // 코드로 등록하기 전에 런타임 리스너를 비워 중복 등록을 막습니다.
        // ★ 인스펙터 On Click 에 걸어둔 것은 이걸로 안 지워집니다.
        //   SettingsPanel 을 쓰던 때 인스펙터에 연결해둔 게 남아 있으면
        //   버튼 한 번에 두 번 처리되니 반드시 직접 비우세요.
        openSettingsButton.onClick.RemoveAllListeners();
        resumeButton.onClick.RemoveAllListeners();
        returnToLoginButton.onClick.RemoveAllListeners();
        quitGameButton.onClick.RemoveAllListeners();

        // Toggle 을 양쪽에 붙이지 않고 Open / Close 를 명시적으로 연결합니다.
        openSettingsButton.onClick.AddListener(Open);
        resumeButton.onClick.AddListener(Close);
        returnToLoginButton.onClick.AddListener(ReturnToLogin);
        quitGameButton.onClick.AddListener(QuitGame);
    }

    private bool ValidateReferences()
    {
        bool ok = true;

        if (settingsPanel == null)       { Debug.LogError("[Settings] settingsPanel 미할당", this);       ok = false; }
        if (openSettingsButton == null)  { Debug.LogError("[Settings] openSettingsButton 미할당", this);  ok = false; }
        if (resumeButton == null)        { Debug.LogError("[Settings] resumeButton 미할당", this);        ok = false; }
        if (returnToLoginButton == null) { Debug.LogError("[Settings] returnToLoginButton 미할당", this); ok = false; }
        if (quitGameButton == null)      { Debug.LogError("[Settings] quitGameButton 미할당", this);      ok = false; }

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

        BindAll();          // ★ 열 때마다 저장값으로 다시 맞춘다 (Bindings.cs)

        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!IsOpen || !refsOk) return;
        if (!GuardFrame("닫기")) return;

        LogWhoClicked("닫기");

        UnbindAll();        // ★ 리스너 해제. 안 하면 열 때마다 쌓인다 (Bindings.cs)

        IsOpen = false;
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    /// <summary>ESC 처럼 "반대로 전환"이 필요한 경우에만 쓰세요. 버튼에는 연결하지 마세요.</summary>
    public void ToggleSettings()
    {
        if (IsOpen) Close();
        else        Open();
    }

    /// <summary>
    /// 같은 프레임에 상태 변경이 두 번 들어오면 두 번째를 무시합니다.
    /// </summary>
    private bool GuardFrame(string action)
    {
        if (Time.frameCount == lastChangeFrame)
        {
            Debug.LogWarning($"[Settings] 같은 프레임에 '{action}' 요청이 두 번 들어와 무시했습니다. " +
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
        Debug.Log($"[Settings] {action} — 클릭된 오브젝트: {(go != null ? go.name : "(키보드 또는 코드 호출)")}", go);
    }

    // ─────────────────────────────────────────────
    private void ReturnToLogin()
    {
        Time.timeScale = 1f;

        if (IsOpen)
        {
            UnbindAll();
            IsOpen = false;
        }

        // 진동이 울리는 도중 씬이 바뀌면 진동만 남아 계속 울립니다.
        HapticManager.Instance?.Cancel();

        // 블룸 연출 요청 정리. BloomController 는 씬과 함께 파괴되지만 습관으로 맞춰둡니다.
        BloomController.Instance?.ClearRequests();

        // ⚠️ 아래 두 줄은 확인이 필요합니다 (응답 본문 참고)
        if (CurrencyManager.Instance != null)
            Destroy(CurrencyManager.Instance.gameObject);

        LevelUpManager.Instance?.ResetStat();   // Destroy 대신 초기화만

        SceneManager.LoadScene(LOGIN_SCENE);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;
        HapticManager.Instance?.Cancel();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
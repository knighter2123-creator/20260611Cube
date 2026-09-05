using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

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
        // 원래 코드는 중복을 그냥 방치했기 때문에 "클릭할 때마다 제멋대로" 동작할 수 있었습니다.
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

    private void OnDisable()
    {
        // 패널이 열린 채로 파괴/씬 전환되면 게임이 멈춘 채로 남습니다
        if (IsOpen) Time.timeScale = 1f;
    }

    private void Start()
    {
        refsOk = ValidateReferences();
        if (!refsOk) return;

        IsOpen = false;
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        // 코드로 등록하기 전에 런타임 리스너를 비워 중복 등록을 막습니다.
        // (인스펙터 On Click 에 걸어둔 것은 이걸로 안 지워지니 직접 확인하세요)
        openSettingsButton.onClick.RemoveAllListeners();
        resumeButton.onClick.RemoveAllListeners();
        returnToLoginButton.onClick.RemoveAllListeners();
        quitGameButton.onClick.RemoveAllListeners();

        // ★ 핵심 : Toggle 을 양쪽에 붙이지 않고 Open / Close 를 명시적으로 연결합니다.
        //   Toggle 은 누가 한 번 더 부르면 상태가 뒤집혀버려서, 원인 추적이 거의 불가능해집니다.
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

    private void Update()
    {
        if (!refsOk) return;

        // New Input System: ESC 키 감지
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleSettings();
    }

    // ─────────────────────────────────────────────
    // 열기 / 닫기
    // ─────────────────────────────────────────────

    public void Open()
    {
        // 이미 열려 있으면 아무 일도 하지 않습니다.
        // → 패널 뒤의 열기 버튼으로 클릭이 관통해도 패널이 닫히지 않습니다.
        if (IsOpen || !refsOk) return;
        if (!GuardFrame("열기")) return;

        LogWhoClicked("열기");

        IsOpen = true;
        settingsPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!IsOpen || !refsOk) return;
        if (!GuardFrame("닫기")) return;

        LogWhoClicked("닫기");

        IsOpen = false;
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;
    }

    /// <summary>ESC 키처럼 "반대로 전환"이 필요한 경우에만 쓰세요. 버튼에는 연결하지 마세요.</summary>
    public void ToggleSettings()
    {
        if (IsOpen) Close();
        else        Open();
    }

    /// <summary>
    /// 같은 프레임에 상태 변경이 두 번 들어오면 두 번째를 무시합니다.
    /// 리스너가 중복 등록됐거나, 클릭이 여러 UI에 동시에 먹힐 때 상태가 뒤집히는 걸 막습니다.
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
        IsOpen = false;

        if (CurrencyManager.Instance != null)
            Destroy(CurrencyManager.Instance.gameObject);

        LevelUpManager.Instance?.ResetStat();   // Destroy 대신 초기화만

        SceneManager.LoadScene(LOGIN_SCENE);
    }

    private void QuitGame()
    {
        Time.timeScale = 1f;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
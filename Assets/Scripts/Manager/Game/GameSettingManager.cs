using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance;

    
    [Header("UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private Button returnToLoginButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button quitGameButton;

    private const string LOGIN_SCENE = "LoginScene";

    public bool IsOpen { get; private set; } = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
    }

    void Start()
    {
        settingsPanel.SetActive(false);
        Time.timeScale = 1f;

        resumeButton.onClick.AddListener(ToggleSettings);
        openSettingsButton.onClick.AddListener(ToggleSettings);
        returnToLoginButton.onClick.AddListener(ReturnToLogin);
        quitGameButton.onClick.AddListener(QuitGame);
    }

    void Update()
    {
        // New Input System: ESC 키 감지
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ToggleSettings();
    }

    public void ToggleSettings()
    {
        IsOpen = !IsOpen;
        settingsPanel.SetActive(IsOpen);
        Time.timeScale = IsOpen ? 0f : 1f;
    }

    private void ReturnToLogin()
    {
        Time.timeScale = 1f;

        if (CurrencyManager.Instance != null)
            Destroy(CurrencyManager.Instance.gameObject);

        LevelUpManager.Instance?.ResetStat();  // Destroy 대신 초기화만

        SceneManager.LoadScene(LOGIN_SCENE);
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
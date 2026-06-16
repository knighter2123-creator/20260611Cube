using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public const string LOGIN_SCENE = "Login";
    public const string STAGE_SCENE = "StageScene";
    public const string GACHA_SCENE = "GachaScene";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 씬 이동 ───────────────────────────────────
    public void GoToLogin() => LoadScene(LOGIN_SCENE);

    public void GoToStage()
    {
        // ✅ GachaScene이 현재 로드된 경우에만 언로드
        if (IsSceneLoaded(GACHA_SCENE))
        {
            Time.timeScale = 1f;
            SceneManager.UnloadSceneAsync(GACHA_SCENE);
        }
        else
        {
            // LoginScene → StageScene 일반 전환
            LoadScene(STAGE_SCENE);
        }
    }

    public void GoToGacha()
    {
        // ✅ StageScene 위에 GachaScene 추가 로드
        SceneManager.LoadScene(GACHA_SCENE, LoadSceneMode.Additive);
    }

    public void RestartScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ── 내부 유틸 ─────────────────────────────────
    private void LoadScene(string sceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }

    // ✅ 씬이 현재 로드되어 있는지 확인
    private bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
}
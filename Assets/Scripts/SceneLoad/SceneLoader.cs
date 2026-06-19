using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public const string LOGIN_SCENE   = "LoginScene";
    public const string STAGE_SCENE   = "StageScene";
    public const string GACHA_SCENE   = "GachaScene";
    public const string LOADING_SCENE = "LoadingScene";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 씬 이동 ───────────────────────────────────

    public void GoToLogin() => LoadScene(LOGIN_SCENE);

    /// <summary>Login → Loading → Stage 경유 이동</summary>
    public void GoToStageWithLoading() => LoadScene(LOADING_SCENE);

    public void GoToStage()
    {
        if (IsSceneLoaded(GACHA_SCENE))
        {
            Time.timeScale = 1f;
            SceneManager.UnloadSceneAsync(GACHA_SCENE);
        }
        else
        {
            LoadScene(STAGE_SCENE);
        }
    }

    public void GoToGacha()
    {
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

    private bool IsSceneLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
}
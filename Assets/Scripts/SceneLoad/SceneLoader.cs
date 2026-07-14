using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public const string LOGIN_SCENE   = "LoginScene";
    public const string STAGE_SCENE   = "StageScene";
    public const string GACHA_SCENE   = "GachaScene";
    public const string LOADING_SCENE = "LoadingScene";
    public const string EVOLVE_SCENE = "EvolveScene";   
    
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 씬 이동 ───────────────────────────────────

    public void GoToLogin() => LoadScene(LOGIN_SCENE);
    
    // 진화 스테이지로 입장 (StageScene 완전 전환 → 진행 위치는 EvolveStageContext에 저장돼 있음)
    public void GoToEvolveStage() => LoadScene(EVOLVE_SCENE);
    
    // 클리어 후 원래 StageScene으로 복귀
    public void ReturnFromEvolve() => LoadScene(STAGE_SCENE);
    
    /// <summary>Login → Loading → Stage 경유 이동</summary>
    public void GoToStageWithLoading() => LoadScene(LOADING_SCENE);

    public void GoToStage()
    {
        if (IsSceneLoaded(GACHA_SCENE))
        {
            SceneManager.UnloadSceneAsync(GACHA_SCENE);

            // 가챠 닫을 때 1f로 고정하지 말고, 저장된 배속을 복원
            if (GameSpeedManager.Instance != null)
                GameSpeedManager.Instance.ReapplySpeed();
            else
                Time.timeScale = 1f;
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
    
    // ── 상태 조회 (GuideQuest 등 외부 판단용) ─────

    /// <summary>현재 활성 씬 이름</summary>
    public string CurrentScene => SceneManager.GetActiveScene().name;

    /// <summary>가챠가 Additive로 열려 있는지</summary>
    public bool IsGachaOpen => IsSceneLoaded(GACHA_SCENE);

    /// <summary>이미 스테이지면 아무것도 하지 않고, 아니면 스테이지로 복귀한다.</summary>
    public void EnsureStageScene()
    {
        // 가챠가 열려 있으면 GoToStage가 언로드 + 배속 복원까지 처리
        if (IsGachaOpen)
        {
            GoToStage();
            return;
        }

        if (CurrentScene == STAGE_SCENE) return;   // 불필요한 재로드 방지

        GoToStage();
    }
}
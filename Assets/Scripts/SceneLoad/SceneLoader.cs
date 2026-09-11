using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public const string LOGIN_SCENE   = "LoginScene";
    public const string STAGE_SCENE   = "StageScene";
    public const string GACHA_SCENE   = "GachaScene";
    public const string LOADING_SCENE = "LoadingScene";
    public const string EVOLVE_SCENE  = "EvolveScene";

    [Header("디버그")]
    [Tooltip("중복 인스턴스가 자기 자신을 제거할 때 로그를 남깁니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logDuplicate = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // ★ 여기가 "게임 재시작 불가능"의 진원지입니다.
            //
            //   LoginScene을 다시 로드하면 씬에 있는 SceneLoader가 새로 하나 생깁니다.
            //   기존 것(DontDestroyOnLoad로 살아남은 것)이 이미 있으니 새 것은 스스로를 지웁니다.
            //   여기까지는 의도된 동작이고 정상입니다.
            //
            //   문제는 LoginScene의 버튼이 인스펙터에서 "씬에 있던 그 SceneLoader"를
            //   드래그해 연결돼 있을 때입니다. 그 참조는 방금 지워진 새 오브젝트를 가리키므로
            //   두 번째 로그인 화면부터는 버튼을 눌러도 아무 일도 일어나지 않습니다.
            //   에러도 안 납니다. UnityEvent는 대상이 사라지면 조용히 건너뛰기 때문입니다.
            //
            //   해결책은 버튼을 인스펙터가 아니라 코드에서 Instance 경유로 연결하는 것입니다.
            //   → LoginSceneUI.cs 참고
            if (logDuplicate)
                Debug.Log($"[SceneLoader] 중복 인스턴스 제거 — 씬 '{gameObject.scene.name}' 의 '{name}'. " +
                          "이 씬의 버튼이 인스펙터로 이 오브젝트를 참조하고 있다면 그 연결은 이제 끊깁니다.", this);

            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        // ★ static 필드가 파괴된 오브젝트를 계속 붙잡고 있으면
        //   "null은 아닌데 쓸 수는 없는" 좀비 상태가 됩니다.
        //   Instance == this 로 검사하는 이유는, 중복 인스턴스가 스스로 지워질 때
        //   살아 있는 진짜 Instance까지 null로 만들면 안 되기 때문입니다.
        if (Instance == this) Instance = null;
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
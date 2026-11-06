using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬 이동 담당(DontDestroyOnLoad, 전부 Single 전환) — 가챠로 갈 땐 떠나기 전 저장, 돌아올 땐 StageScene 재로드 후 배속 복원 (ShopScene 은 자체 카메라·EventSystem 보유).</summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public const string LOGIN_SCENE   = "LoginScene";
    public const string STAGE_SCENE   = "StageScene";
    public const string GACHA_SCENE   = "ShopScene";
    public const string LOADING_SCENE = "LoadingScene";
    public const string EVOLVE_SCENE  = "EvolveScene";
    public const string DAILY_DUNGEON_SCENE = "DailyDungeonScene";

    [Header("디버그")]
    [Tooltip("중복 인스턴스가 자기 자신을 제거할 때 로그를 남깁니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logDuplicate = true;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 중복 인스턴스는 스스로 파괴
            // ⚠ 버튼을 인스펙터로 씬의 SceneLoader 에 연결하면 재진입 시 파괴된 쪽을 가리켜 조용히 무반응 — 코드에서 SceneLoader.Instance 경유로 연결
            if (logDuplicate)
                GameLog.Info($"[SceneLoader] 중복 인스턴스 제거 — 씬 '{gameObject.scene.name}' 의 '{name}'. " +
                          "이 씬의 버튼이 인스펙터로 이 오브젝트를 참조하고 있다면 그 연결은 이제 끊깁니다.", this);

            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ValidateSceneNames();
    }

    /// <summary>씬 이름 상수가 Build Settings 에 있는지 시작 시 확인해 경고 (틀리면 가챠에서 못 돌아옴).</summary>
    private void ValidateSceneNames()
    {
        string[] names = { LOGIN_SCENE, STAGE_SCENE, GACHA_SCENE, LOADING_SCENE, EVOLVE_SCENE, DAILY_DUNGEON_SCENE };
        foreach (string n in names)
        {
            // Build Settings 에 등록된 씬 이름인지 확인 (SaveManager.preGameScenes 검사와 같은 방법)
            if (!Application.CanStreamedLevelBeLoaded(n))
                Debug.LogWarning($"[SceneLoader] '{n}' 가 Build Settings 에 없습니다. 씬 이름 상수를 확인하세요.", this);
        }
    }

    void OnDestroy()
    {
        // 파괴 시 Instance 비움(좀비 참조 방지) — == this 검사로 중복 인스턴스 파괴가 진짜를 지우지 않게
        if (Instance == this) Instance = null;
    }

    // ── 씬 이동 ───────────────────────────────────

    // 진화 스테이지로 입장 (StageScene 완전 전환 → 진행 위치는 EvolveStageContext에 저장돼 있음)
    public void GoToEvolveStage() => LoadScene(EVOLVE_SCENE);

    // 클리어 후 원래 StageScene으로 복귀
    public void ReturnFromEvolve() => LoadScene(STAGE_SCENE);

    // 일일 던전 입장 (진행 위치는 DailyDungeonContext에 저장돼 있음) / 종료 후 복귀
    public void GoToDailyDungeon()       => LoadScene(DAILY_DUNGEON_SCENE);
    public void ReturnFromDailyDungeon() => LoadScene(STAGE_SCENE);

    /// <summary>Login → Loading → Stage 경유 이동</summary>
    public void GoToStageWithLoading() => LoadScene(LOADING_SCENE);

    public void GoToStage()
    {
        // "가챠에서 돌아오는 중인가" 를 로드 요청 "전에" 기억합니다 — 배속 복원은 가챠 복귀일 때만 (로딩 씬 등 다른 경로는 1배속).
        bool returningFromGacha = IsInGacha;

        LoadScene(STAGE_SCENE);

        // 저장된 배속 복원 (timeScale 은 전역이라 로드 요청 직후 설정해도 새 씬에 적용)
        if (returningFromGacha) RestoreGameSpeed();
    }

    public void GoToGacha()
    {
        // 이미 가챠 씬이면 다시 불러오지 않음 (연타·중복 호출 시 재로드 방지)
        if (IsInGacha) return;

        // 떠나기 전 저장 — StageScene 은 세이브로 재구성되므로 안 하면 가챠 왕복만으로 진행이 되돌아감
        SaveBeforeLeavingStage();

        LoadScene(GACHA_SCENE);
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

    /// <summary>가챠로 떠나기 전 저장 — 씬 이름 조건 없이 호출하고 '저장해도 되는가'는 SaveManager 가 판단 (이름 비교는 어긋나면 조용히 건너뜀).</summary>
    private void SaveBeforeLeavingStage()
    {
        // SaveManager 는 Unity 오브젝트라 ?. 대신 == null 로 검사 (파괴된 인스턴스도 걸러냄)
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
        else
            Debug.LogWarning("[SceneLoader] SaveManager 가 없어 가챠 이동 전 저장을 건너뜁니다. (에디터에서 StageScene 바로 실행?)");
    }

    /// <summary>저장된 배속 복원. GameSpeedManager 가 없으면 1배속.</summary>
    private void RestoreGameSpeed()
    {
        if (GameSpeedManager.Instance != null)
            GameSpeedManager.Instance.ReapplySpeed();
        else
            Time.timeScale = 1f;
    }

    // ── 상태 조회 (GuideQuest 등 외부 판단용) ─────

    /// <summary>현재 활성 씬 이름</summary>
    public string CurrentScene => SceneManager.GetActiveScene().name;

    /// <summary>지금 가챠 씬(ShopScene)이 떠 있는지.</summary>
    public bool IsInGacha => IsSceneLoaded(GACHA_SCENE);

    /// <summary>이미 스테이지면 아무것도 하지 않고, 아니면 스테이지로 복귀한다 (가챠에서면 배속 복원까지).</summary>
    public void EnsureStageScene()
    {
        if (CurrentScene == STAGE_SCENE) return;   // 불필요한 재로드 방지
        GoToStage();
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 이동 담당 (DontDestroyOnLoad).
///
/// ★ [가챠 씬 일반 전환] 이번 변경 (전부 "★ [가챠 씬 일반 전환]" 으로 표시)
///   가챠(ShopScene)를 StageScene 위에 겹쳐 띄우던(Additive) 방식을, 일반 씬 이동(Single)으로 바꿨습니다.
///   가이드 퀘스트 '동료 1회 소환' 클릭 → GoToGacha() 경로가 이제 StageScene 을 내리고 ShopScene 으로 이동합니다.
///
///   1. GoToGacha()  : Additive → LoadScene(Single). 떠나기 전에 SaveManager.Save() (아래 이유)
///   2. GoToStage()  : 가챠에서 돌아올 때 LoadScene(StageScene) + 저장된 배속 복원
///   3. IsGachaOpen  : "ShopScene 이 로드돼 있나" → "StageScene 위에 Additive 로 겹쳐 있나" 로 판정을 정확히 함
///                     (★ 안 고치면 가챠에서 못 나옵니다 — GoToStage 주석 참고)
///   4. IsInGacha    : 신규. "지금 가챠 씬을 보고 있나" (방식과 무관)
///
///   ⚠ GoToGacha() 는 가챠로 가는 유일한 함수라, 이 함수를 부르는 다른 버튼(예: HUD 상점 버튼)도
///     함께 일반 전환이 됩니다. ShopScene 을 두 방식으로 동시에 쓸 수는 없기 때문입니다
///     (Single 로 열려면 ShopScene 에 카메라·EventSystem 이 있어야 하고, Additive 로 겹치면 그게 중복됨).
/// </summary>
public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance;

    public const string LOGIN_SCENE   = "LoginScene";
    public const string STAGE_SCENE   = "StageScene";
    public const string GACHA_SCENE   = "ShopScene";
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

        ValidateSceneNames();
    }

    /// <summary>
    /// ★ [재검토] 씬 이름 상수 오타 방어.
    ///   이제 가챠에서 돌아올 때 LoadScene(STAGE_SCENE) 을 반드시 거치므로,
    ///   이름이 Build Settings 의 실제 씬과 다르면 "가챠에서 못 나오는" 상태가 됩니다.
    ///   시작할 때 한 번 확인해서 경고로 알립니다 (에러가 아니라 경고 — 게임은 계속 진행).
    /// </summary>
    private void ValidateSceneNames()
    {
        string[] names = { LOGIN_SCENE, STAGE_SCENE, GACHA_SCENE, LOADING_SCENE, EVOLVE_SCENE };
        foreach (string n in names)
        {
            // Build Settings 에 등록된 씬 이름인지 확인 (SaveManager.preGameScenes 검사와 같은 방법)
            if (!Application.CanStreamedLevelBeLoaded(n))
                Debug.LogWarning($"[SceneLoader] '{n}' 가 Build Settings 에 없습니다. 씬 이름 상수를 확인하세요.", this);
        }
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
        // ★ [가챠 씬 일반 전환] 이 분기는 "StageScene 위에 Additive 로 겹친 가챠" 만 탑니다.
        //   예전 IsGachaOpen 은 "ShopScene 이 로드돼 있기만 하면" true 였습니다.
        //   가챠를 Single 로 열면 ShopScene 이 '유일하게 로드된 씬' 이라 그것도 true 가 되고,
        //   그러면 여기서 UnloadSceneAsync(ShopScene) 를 부르게 됩니다.
        //   유니티는 마지막 남은 씬을 언로드할 수 없어서(에러 후 아무 일도 안 함) → 가챠에서 영영 못 나옵니다.
        //   IsGachaOpen 을 "StageScene 도 같이 로드돼 있을 때만" 으로 고쳐서 막았습니다.
        //   (이제 Additive 로 여는 곳이 없지만, 다른 코드가 직접 Additive 로 여는 경우를 위해 남겨 둠)
        if (IsGachaOpen)
        {
            SceneManager.UnloadSceneAsync(GACHA_SCENE);

            // 가챠 닫을 때 1f로 고정하지 말고, 저장된 배속을 복원
            RestoreGameSpeed();
        }
        else
        {
            // ★ [재검토] "가챠에서 돌아오는 중인가" 를 로드 요청 "전에" 기억합니다.
            //   이 else 분기는 가챠 말고 다른 곳(로딩 씬 등)에서도 불릴 수 있어서,
            //   배속 복원을 가챠 복귀일 때만 하도록 좁혔습니다 → 다른 경로의 동작은 예전과 완전히 같음.
            bool returningFromGacha = IsInGacha;

            LoadScene(STAGE_SCENE);

            // ★ [가챠 씬 일반 전환] LoadScene 은 timeScale 을 1 로 되돌립니다.
            //   가챠에서 돌아올 때는 예전(Additive 닫기)처럼 저장된 배속으로 돌아가야 하므로 복원합니다.
            //   timeScale 은 씬이 바뀌어도 유지되는 전역 값이라, 로드 요청 직후에 설정해도 새 씬에 그대로 적용됩니다.
            if (returningFromGacha) RestoreGameSpeed();
        }
    }

    public void GoToGacha()
    {
        // ★ [가챠 씬 일반 전환] 이미 가챠 씬이면 다시 불러오지 않음 (연타·중복 호출 시 재로드 방지)
        if (IsInGacha) return;

        // ★ [가챠 씬 일반 전환] StageScene 을 떠나기 "전에" 저장합니다.
        //   Additive 일 때는 StageScene 이 뒤에 살아 있어서 아무것도 잃지 않았지만,
        //   이제는 StageScene 이 내려갔다가 돌아올 때 세이브(SaveManager.Current)에서 다시 만들어집니다.
        //   마지막 저장 이후의 진행(스테이지 킬 수, 경험치·골드 등)을 Current 에 모아 두지 않으면
        //   가챠에 다녀오는 것만으로 그만큼 되돌아갑니다.
        //   (게임 씬이므로 '게임 시작 전 저장 차단' 에 걸리지 않습니다)
        SaveBeforeLeavingStage();

        LoadScene(GACHA_SCENE);   // 예전: SceneManager.LoadScene(GACHA_SCENE, LoadSceneMode.Additive)
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

    /// <summary>
    /// ★ [가챠 씬 일반 전환] 가챠로 떠나기 전 저장.
    /// ★ [재검토] 처음엔 "현재 씬 이름 == STAGE_SCENE 일 때만" 저장했는데, 조건을 뺐습니다.
    ///   씬 이름이 상수와 조금만 달라도(예: 실제 씬이 MainScene) 저장이 조용히 건너뛰어져,
    ///   에러 없이 "가챠 다녀오면 진행이 되돌아가는" 버그가 됩니다.
    ///   Save() 는 원래 어느 씬에서 불러도 안전합니다 — 게임 씬이면 저장하고,
    ///   로그인/로딩 씬이면 SaveManager 의 '게임 시작 전 저장 차단' 이 알아서 무시합니다.
    ///   "지금 저장해도 되는가" 의 판단은 SaveManager 한 곳에 맡깁니다.
    /// </summary>
    private void SaveBeforeLeavingStage()
    {
        // SaveManager 는 Unity 오브젝트라 ?. 대신 == null 로 검사 (파괴된 인스턴스도 걸러냄)
        if (SaveManager.Instance != null)
            SaveManager.Instance.Save();
        else
            Debug.LogWarning("[SceneLoader] SaveManager 가 없어 가챠 이동 전 저장을 건너뜁니다. (에디터에서 StageScene 바로 실행?)");
    }

    /// <summary>저장된 배속 복원. GameSpeedManager 가 없으면 1배속. (예전 GoToStage 의 코드를 함수로 뽑음 — 두 곳에서 씀)</summary>
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

    /// <summary>
    /// 가챠가 StageScene 위에 Additive 로 겹쳐 열려 있는지.
    /// ★ [가챠 씬 일반 전환] "StageScene 도 함께 로드돼 있을 때만" true (위 GoToStage 주석 참고).
    ///   지금은 가챠를 Single 로 열므로 보통 false 입니다. "가챠 화면인가" 를 알고 싶으면 IsInGacha 를 쓰세요.
    /// </summary>
    public bool IsGachaOpen => IsSceneLoaded(GACHA_SCENE) && IsSceneLoaded(STAGE_SCENE);

    /// <summary>★ [가챠 씬 일반 전환] 신규. 지금 가챠 씬이 떠 있는지 (Single / Additive 무관).</summary>
    public bool IsInGacha => IsSceneLoaded(GACHA_SCENE);

    /// <summary>이미 스테이지면 아무것도 하지 않고, 아니면 스테이지로 복귀한다.</summary>
    public void EnsureStageScene()
    {
        // Additive 가챠가 열려 있으면 GoToStage가 언로드 + 배속 복원까지 처리
        if (IsGachaOpen)
        {
            GoToStage();
            return;
        }

        if (CurrentScene == STAGE_SCENE) return;   // 불필요한 재로드 방지

        GoToStage();   // Single 가챠(ShopScene)에 있으면 여기서 StageScene 으로 이동 + 배속 복원
    }
}
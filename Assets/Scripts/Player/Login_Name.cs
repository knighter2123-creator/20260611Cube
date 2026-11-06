using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>LoginScene 게임 시작의 단일 입구(StartGame): (게스트 로그인) → AccountSwitch 로 계정·세이브 맞춤 → CloudSave 확인 → LoadingScene, 재시작이 필요하면 멈추고 새 LoginScene 에서 자동으로 이어감.</summary>
// ⚠ 시작 버튼의 인스펙터 OnClick 은 비울 것 (리스너는 Start 에서 등록) / 닉네임은 여기서 안 정함 (MainScene 의 NicknamePrompt)
public class Login_Name : MonoBehaviour
{
    [Header("시작 버튼")]
    // FormerlySerializedAs: 예전 필드 이름(submitButton)으로 저장된 인스펙터 연결을 그대로 이어받음
    [FormerlySerializedAs("submitButton")]
    [SerializeField] private Button startButton;

    [Header("계정 확인 대기")]
    [Tooltip("시작을 눌렀을 때 로그인 상태(AuthManager)가 아직 확인 전이면 최대 이만큼 기다립니다(초, 실제 시간).\n" +
             "보통 1초 안에 끝납니다. 넘으면(오프라인 등) 마지막으로 쓰던 계정 데이터로 시작합니다.")]
    [SerializeField] private float authWaitSeconds = 3f;

    [Header("게스트 로그인")]
    [Tooltip("로그인하지 않고 시작하면 뒤끝 게스트 계정으로 로그인합니다. 최대 이만큼 기다리고(초), 실패하면 이 기기 게스트로 시작합니다.")]
    [SerializeField] private float guestWaitSeconds = 5f;

    [Header("클라우드 세이브")]
    [Tooltip("클라우드 세이브를 확인할 때 최대 이만큼 기다립니다(초). 넘으면(오프라인 등) 이 기기 데이터로 시작합니다.")]
    [SerializeField] private float cloudWaitSeconds = 5f;

    // 시작 절차에 들어갔는지. 버튼 연타·여러 버튼 동시 입력으로 시작이 두 번 되는 것을 막습니다.
    private bool isLoading;

    private void Start()
    {
        // 옛 PlayerPrefs 이름 → SaveData 1회 이관 (SaveManager.Awake 뒤인 Start 라 안전)
        PlayerProfile.MigrateLegacyIfNeeded();

        if (startButton != null) startButton.onClick.AddListener(StartGame);

        // 재시작으로 다시 로드된 직후면 이미 '시작'을 누른 상태라 자동 시작 (Start 라 SaveManager·SceneLoader 준비 완료)
        if (AccountSwitch.ConsumeAutoStartAfterReload())
        {
            GameLog.Info("[Login_Name] 계정 전환 재시작 완료 — 게임을 이어서 시작합니다.");
            StartGame();
        }
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
    }

    // ───────── 시작 ─────────

    /// <summary>게임 시작의 유일한 입구 — 경로가 하나여야 연타 방지(isLoading)가 한 곳에서 지켜짐.</summary>
    public void StartGame()
    {
        if (isLoading) return;

        // 계정 확인을 기다리는 동안 다른 버튼으로 또 시작되지 않게 바로 잠급니다.
        isLoading = true;
        if (startButton != null) startButton.interactable = false;

        StartCoroutine(StartRoutine());
    }

    /// <summary>로그인 계정의 세이브로 맞춘 뒤 씬 이동 — 로그인 상태를 모르면 최대 authWaitSeconds 대기, Rebooting 이면 이동 안 함, 시간 초과면 마지막 계정으로 시작.</summary>
    private IEnumerator StartRoutine()
    {
        // 로그인하지 않고 시작 → 뒤끝 게스트 계정으로 로그인 (구글·이메일 버튼은 로그인한 뒤에 여기로 오므로 건너뜀)
        yield return EnsureGuestAccount();

        float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, authWaitSeconds);   // 실제 시간 (timeScale 무관)

        AccountSwitch.StartResult result = AccountSwitch.PrepareForStart();
        while (result == AccountSwitch.StartResult.NotReady && Time.realtimeSinceStartup < deadline)
        {
            yield return null;   // 한 프레임 쉬고 다시 물음 (NotReady 는 부작용이 없어 반복해도 안전)
            result = AccountSwitch.PrepareForStart();
        }

        if (result == AccountSwitch.StartResult.Rebooting) yield break;   // 이 오브젝트는 곧 LoginScene 과 함께 사라짐

        if (result == AccountSwitch.StartResult.NotReady)
            Debug.LogWarning($"[Login_Name] {authWaitSeconds}초 동안 로그인 상태를 확인하지 못해 마지막 계정({SaveManager.ActiveAccountLabel}) 데이터로 시작합니다.");

        // 클라우드 세이브 확인 — 게스트·오프라인이면 바로 통과. 선택 팝업이 뜨면 고를 때까지 기다림.
        if (CloudSave.Instance != null)
        {
            bool done = false;
            CloudSave.StartCheck check = CloudSave.StartCheck.Proceed;
            CloudSave.Instance.CheckBeforeStart(cloudWaitSeconds, r => { check = r; done = true; });

            while (!done && this != null) yield return null;
            if (check == CloudSave.StartCheck.Rebooting) yield break;   // 클라우드 세이브 적용 재시작 — 새 LoginScene 에서 자동으로 이어서 시작
        }

        LoadMainScene();
    }

    /// <summary>로그인 없이 시작하면 뒤끝 게스트 로그인 (기기 게스트 진행은 AccountSwitch 가 옮김, 실패하면 기기 게스트로 시작).</summary>
    private IEnumerator EnsureGuestAccount()
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null) yield break;

        float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, guestWaitSeconds);
        while (auth != null && !auth.IsReady && Time.realtimeSinceStartup < deadline) yield return null;   // 자동 로그인 확인이 먼저
        if (auth == null || !auth.IsReady || auth.IsSignedIn || auth.IsSigningIn) yield break;

        auth.SignInAsGuest();
        while (auth != null && auth.IsSigningIn && Time.realtimeSinceStartup < deadline) yield return null;

        if (auth != null && !auth.IsSignedIn)
            Debug.LogWarning($"[Login_Name] 게스트 로그인을 하지 못해 이 기기 게스트로 시작합니다: {auth.LastError}");
    }

    private static void LoadMainScene()
    {
        if (SceneLoader.Instance == null)
            new GameObject("SceneLoader").AddComponent<SceneLoader>();

        SceneLoader.Instance.GoToStageWithLoading();   // LoadingScene → MainScene
    }

#if UNITY_EDITOR
    // 인스펙터에서 컴포넌트 우클릭 → 메뉴로 실행 (플레이 모드에서)
    [ContextMenu("테스트: 저장된 이름 지우기")]
    private void DebugClearName() => PlayerProfile.DebugClear();
#endif
}

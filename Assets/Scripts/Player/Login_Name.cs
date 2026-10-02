using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// LoginScene 의 "게임 시작" 버튼 처리.
///
///   흐름:  [게임 시작] ─→ (계정 확인) ─→ LoadingScene → MainScene
///
/// · 닉네임은 여기서 정하지 않습니다. MainScene 에서 특정 스테이지 도달 시 정합니다
///   (NicknamePrompt → NicknameChangePanel 의 '최초 설정' 모드 → PlayerProfile.TryRegister).
/// · StartGame() 은 public — 다른 버튼(GoogleLoginButtonUI, EmailLoginUI 등)도 같은 입구로 들어옵니다.
/// · 씬 이동 직전에 AccountSwitch.PrepareForStart() 로 로그인 계정과 세이브 주인을 맞춥니다.
///   계정이 바뀌어 매니저 재시작이 필요하면 여기서 멈추고, 재시작된 LoginScene 에서 자동으로 이어서 시작합니다.
///
/// ★ 시작 버튼의 인스펙터 OnClick 목록은 비워 두세요. 리스너는 코드(Start)에서 등록합니다.
/// </summary>
public class Login_Name : MonoBehaviour
{
    [Header("시작 버튼")]
    // FormerlySerializedAs: 예전 필드 이름(submitButton)으로 저장된 인스펙터 연결을 그대로 이어받음
    [FormerlySerializedAs("submitButton")]
    [SerializeField] private Button startButton;

    [Header("계정 확인 대기")]
    [Tooltip("시작을 눌렀을 때 로그인 상태(Firebase)가 아직 준비 전이면 최대 이만큼 기다립니다(초, 실제 시간).\n" +
             "보통 1초 안에 끝납니다. 넘으면(오프라인 등) 마지막으로 쓰던 계정 데이터로 시작합니다.")]
    [SerializeField] private float authWaitSeconds = 3f;

    // 시작 절차에 들어갔는지. 버튼 연타·여러 버튼 동시 입력으로 시작이 두 번 되는 것을 막습니다.
    private bool isLoading;

    private void Start()
    {
        // 예전 PlayerPrefs 에 이름이 있던 유저 → SaveData 로 1회 이관
        // (SaveManager.Awake 가 먼저 끝나 있으므로 Start 에서 호출하면 안전)
        PlayerProfile.MigrateLegacyIfNeeded();

        if (startButton != null) startButton.onClick.AddListener(StartGame);

        // 계정 전환 때문에 LoginScene 이 다시 로드된 직후라면, 유저는 이미 '시작' 을 누른 상태입니다.
        // Start 에서 하는 이유: SaveManager(Awake, 실행 순서 -100)가 새 계정 파일을 이미 읽었고,
        // 이 씬의 SceneLoader 도 Awake 에서 Instance 등록을 끝낸 뒤입니다.
        if (AccountSwitch.ConsumeAutoStartAfterReload())
        {
            Debug.Log("[Login_Name] 계정 전환 재시작 완료 — 게임을 이어서 시작합니다.");
            StartGame();
        }
    }

    private void OnDestroy()
    {
        if (startButton != null) startButton.onClick.RemoveListener(StartGame);
    }

    // ───────── 시작 ─────────

    /// <summary>
    /// 게임 시작의 단 하나의 입구. 시작 경로가 하나여야 연타 방지(isLoading) 같은 규칙이 한 곳에서 지켜집니다.
    /// </summary>
    public void StartGame()
    {
        if (isLoading) return;

        // 계정 확인을 기다리는 동안 다른 버튼으로 또 시작되지 않게 바로 잠급니다.
        isLoading = true;
        if (startButton != null) startButton.interactable = false;

        StartCoroutine(StartRoutine());
    }

    /// <summary>
    /// 지금 로그인한 계정의 세이브로 시작하도록 맞춘 뒤 씬을 옮깁니다.
    /// 로그인 상태를 아직 모르면(Firebase 준비 전) 매 프레임 다시 물으며 최대 authWaitSeconds 만큼 기다립니다.
    ///   Proceed   → 시작
    ///   Rebooting → 계정 전환 재시작 중 — 씬을 옮기지 않음 (재시작 후 새 LoginScene 에서 자동으로 다시 들어옴)
    ///   NotReady  → 시간 초과(오프라인 등) — 마지막으로 쓰던 계정 그대로 시작 (게임이 막히지 않게)
    /// </summary>
    private IEnumerator StartRoutine()
    {
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

        LoadMainScene();
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

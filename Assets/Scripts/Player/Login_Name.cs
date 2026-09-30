using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// LoginScene 의 "게임 시작" 버튼 처리.
///
/// ★ [닉네임 인게임 이동] 이번 변경 (2026-09-30)
///   닉네임은 이제 LoginScene 이 아니라 MainScene 에서 "특정 스테이지 도달" 시 정합니다
///   (NicknamePrompt → NicknameChangePanel 의 '최초 설정' 모드 → PlayerProfile.TryRegister).
///   그래서 이 스크립트는 이름 확인 없이 바로 시작합니다.
///
///   흐름:  [게임 시작] ─→ LoadingScene → MainScene
///
///   바뀐 곳
///   1. OnClickStart: 이름 확인/팝업 없이 바로 PlayToMain
///   2. StartGame() 을 public 으로 추가 — 다른 버튼(GoogleLoginButtonUI.onStartGame 등)이
///      인스펙터에서 "같은 시작 함수" 를 부를 수 있게. (OnClickStart 는 private 이라 인스펙터 목록에 안 보임)
///   3. 이름 입력 팝업 코드 제거. 단, 씬에 남아 있는 팝업 오브젝트가 켜진 채 보이지 않도록
///      legacyNamePopup 으로 참조를 이어받아(FormerlySerializedAs) 자동으로 숨깁니다.
///   4. ClosePopup / IsPopupOpen 은 다른 스크립트(뒤로가기 처리 등)가 부르고 있을 수 있어
///      컴파일이 깨지지 않도록 남겨 두고 [Obsolete] 로 표시했습니다 → 경고가 뜨는 곳이 호출부입니다.
///
/// ★ 시작 버튼의 인스펙터 OnClick 목록은 비워 두세요. 리스너는 코드(Start)에서 등록합니다.
/// </summary>
public class Login_Name : MonoBehaviour
{
    [Header("시작 버튼")]
    // FormerlySerializedAs: 예전 필드 이름(submitButton)으로 저장된 인스펙터 연결을 그대로 이어받음
    [FormerlySerializedAs("submitButton")]
    [SerializeField] private Button startButton;

    [Header("(정리 대상) 예전 이름 입력 팝업")]
    [Tooltip("더 이상 쓰지 않습니다. 예전 namePopup 연결을 이어받아 시작 시 숨기기만 합니다.\n" +
             "씬에서 팝업 오브젝트를 지운 뒤 이 칸이 비어도 괜찮습니다.")]
    // ★ 필드 이름이 namePopup → legacyNamePopup 으로 바뀌었지만 FormerlySerializedAs 덕분에
    //   씬에 연결돼 있던 팝업 참조가 끊기지 않습니다. 이 한 줄이 없으면 팝업이 켜진 채로 저장된 씬에서
    //   작동하지 않는 팝업이 화면을 가리게 됩니다 (예전엔 Awake 가 숨겨 줬으므로).
    [FormerlySerializedAs("namePopup")]
    [SerializeField] private GameObject legacyNamePopup;

    // 로딩을 시작했는지. 버튼 연타로 씬 로딩이 두 번 호출되는 것을 막습니다.
    private bool isLoading;

    private void Awake()
    {
        if (legacyNamePopup != null)
        {
            legacyNamePopup.SetActive(false);
            Debug.Log("[Login_Name] 예전 이름 입력 팝업을 숨겼습니다. 이제 쓰지 않으니 LoginScene 에서 지워도 됩니다.", legacyNamePopup);
        }
    }

    private void Start()
    {
        // 예전 PlayerPrefs 에 이름이 있던 유저 → SaveData 로 1회 이관 (기존 그대로)
        // (SaveManager.Awake 가 먼저 끝나 있으므로 Start 에서 호출하면 안전)
        PlayerProfile.MigrateLegacyIfNeeded();

        if (startButton != null) startButton.onClick.AddListener(OnClickStart);
    }

    private void OnDestroy()
    {
        // 이름 있는 메서드로 구독했기 때문에 RemoveListener 로 정확히 해제할 수 있습니다.
        if (startButton != null) startButton.onClick.RemoveListener(OnClickStart);
    }

    // ───────── 시작 ─────────

    private void OnClickStart() => StartGame();

    /// <summary>
    /// 게임 시작의 단 하나의 입구. 시작 버튼과, 인스펙터로 연결한 다른 버튼(예: 구글 로그인 성공)이
    /// 모두 여기로 들어옵니다. 시작 경로가 하나여야 연타 방지(isLoading) 같은 규칙이 한 곳에서 지켜집니다.
    /// </summary>
    public void StartGame()
    {
        if (isLoading) return;   // 이미 로딩 중이면 무시 (두 버튼을 거의 동시에 눌러도 한 번만)
        PlayToMain();
    }

    // ───────── 예전 팝업 API (호환용) ─────────

    [System.Obsolete("이름 입력은 MainScene 으로 옮겨졌습니다. 이 호출은 지워도 됩니다.")]
    public void ClosePopup()
    {
        if (legacyNamePopup != null) legacyNamePopup.SetActive(false);
    }

    [System.Obsolete("이름 입력은 MainScene 으로 옮겨졌습니다. 항상 false 입니다.")]
    public bool IsPopupOpen => false;

    // ───────── 씬 이동 (기존 코드 유지) ─────────

    private void PlayToMain()
    {
        isLoading = true;
        if (startButton != null) startButton.interactable = false;   // 연타 방지 (시각적으로도)

        if (SceneLoader.Instance == null)
        {
            GameObject obj = new GameObject("SceneLoader");
            obj.AddComponent<SceneLoader>();
        }

        SceneLoader.Instance.GoToStageWithLoading();   // LoadingScene → MainScene
    }

#if UNITY_EDITOR
    // 인스펙터에서 컴포넌트 우클릭 → 메뉴로 실행 (플레이 모드에서)
    [ContextMenu("테스트: 저장된 이름 지우기")]
    private void DebugClearName() => PlayerProfile.DebugClear();
#endif
}
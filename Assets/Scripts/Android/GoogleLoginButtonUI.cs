using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoginScene 의 구글 로그인 버튼 UI — 씬 오브젝트 (ManagerRoot 에 넣지 않는다).
///
/// 버튼을 누르면 AuthManager 에 "로그인 해 줘" 라고 부탁만 하고,
/// 화면 갱신은 AuthManager.StateChanged 이벤트를 받아 AuthManager 의 상태를 읽어서 한다.
///   → 씬을 다시 로드하면 이 UI 도 새로 태어나 새 버튼에 리스너를 건다.
///     (한 스크립트에 UI 와 매니저를 합쳐 ManagerRoot 에 넣으면, 로그인 화면 복귀 시
///      새 버튼에는 아무도 리스너를 걸지 않아 '안 눌리는 버튼' 이 된다.)
///
/// ★ [자동 시작 버그 수정] 이번 변경 (전부 "★ [자동 시작 버그 수정]" 으로 표시)
///   [증상] 로그인된 계정이 있으면
///     - 앱(Play)을 켜자마자 LoginScene 에서 바로 게임이 시작됨
///     - StageScene → '로그인으로 돌아가기' → LoginScene 에 도착하자마자 다시 게임 시작 (= 스테이지 재시작처럼 보임)
///   [원인] 예전 Refresh 의 "로그인돼 있으면 StartGame()" 조건.
///     Refresh 는 버튼을 눌렀을 때만이 아니라 ① OnEnable(씬에 들어올 때마다) ② Firebase 준비 완료(자동 로그인 복원)
///     ③ 다른 UI 의 로그인/로그아웃 때마다 불립니다. 그래서 "로그인 상태" 만 보고 시작하면
///     자동 로그인 = 자동 시작이 되어, 로그인 화면에 머무를 수가 없었습니다 (로그아웃·계정 변경도 불가).
///   [수정]
///   1. startAfterSignIn — "이 버튼을 눌러서 방금 로그인했다" 일 때만 시작 (자동 로그인과 구분)
///   2. 로그인된 상태에서는 이 버튼이 [시작하기] 가 됨 — 로그인 화면에 머문 유저가 시작할 방법
///      (예전에는 로그인되면 버튼이 회색이 되어, 게스트 버튼으로만 시작할 수 있었음)
///   3. buttonLabel(선택) — 버튼 글자를 "Google 로그인" / "시작하기" 로 바꿔 줌
///   loginName 직접 참조 방식은 그대로 둡니다 (둘 다 같은 LoginScene 오브젝트라 안전).
/// </summary>
public class GoogleLoginButtonUI : MonoBehaviour
{
    [SerializeField] private Login_Name loginName;

    [SerializeField] private Button googleLoginButton;
    [SerializeField] private TextMeshProUGUI userIdTMP;     // (선택/테스트용) Firebase UID
    [SerializeField] private TextMeshProUGUI userNameTMP;   // (선택/테스트용) 구글 표시 이름

    [Header("★ [자동 시작 버그 수정] 버튼 글자 (선택)")]
    [Tooltip("(선택) 구글 버튼 안의 TMP. 로그인 전 / 후로 글자를 바꿉니다. 비워 두면 글자는 그대로입니다.")]
    [SerializeField] private TextMeshProUGUI buttonLabel;
    [SerializeField] private string signInLabel = "Google 로그인";
    [SerializeField] private string startLabel  = "시작하기";

    // ★ [자동 시작 버그 수정] 이 버튼으로 로그인을 시작했는가.
    //   true 인 상태에서 로그인 성공이 확인되면 게임을 시작하고 바로 false 로 내립니다.
    //   자동 로그인(앱 재실행, 로그인 화면 복귀)은 이 버튼을 누르지 않았으므로 false → 시작하지 않습니다.
    //   private 이고 [SerializeField] 가 없으므로, 씬이 새로 로드될 때마다 항상 false 로 시작합니다.
    private bool startAfterSignIn;

    private void Awake()
    {
        if (googleLoginButton == null)
        {
            Debug.LogError("[GoogleLoginButtonUI] googleLoginButton 이 연결되지 않았습니다.");
            enabled = false;
            return;
        }
        // 인스펙터의 On Click() 은 비워 둘 것 — 여기서 등록하므로 둘 다 걸면 한 번에 두 번 처리된다
        googleLoginButton.onClick.AddListener(OnClickGoogleLogin);

        // ★ [자동 시작 버그 수정] 연결을 빠뜨리면 "로그인은 되는데 시작이 안 되는" 상태라 미리 알림
        if (loginName == null)
            Debug.LogWarning("[GoogleLoginButtonUI] loginName 이 연결되지 않았습니다. 로그인 후 게임이 시작되지 않습니다.", this);
    }

    private void OnEnable()
    {
        // 구독 + 즉시 한 번 갱신.
        // 이벤트만 기다리면, 구독하기 전에 이미 끝난 일(자동 로그인 등)을 영영 못 보게 된다.
        // "구독할 때 현재 상태를 한 번 읽는다" 가 이벤트 기반 UI 의 기본 짝이다.
        AuthManager.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        // static 이벤트는 파괴된 구독자도 계속 붙잡으므로 반드시 해제
        AuthManager.StateChanged -= Refresh;
    }

    private void OnDestroy()
    {
        if (googleLoginButton != null)
            googleLoginButton.onClick.RemoveListener(OnClickGoogleLogin);
    }

    private void OnClickGoogleLogin()
    {
        // 매번 Instance 를 새로 읽는다 (계정 삭제 후 매니저가 새로 만들어질 수 있음)
        AuthManager auth = AuthManager.Instance;
        if (auth == null)
        {
            Debug.LogWarning("[GoogleLoginButtonUI] AuthManager 가 없습니다. ManagerRoot 하위에 추가했는지 확인하세요.");
            return;
        }

        // ★ [자동 시작 버그 수정] 이미 로그인된 상태 → 이 버튼은 [시작하기]. 유저가 직접 눌렀으니 바로 시작
        if (auth.IsSignedIn)
        {
            StartGame();
            return;
        }

        // 로그인 전 → 로그인 시작. 성공하면 Refresh() 에서 시작
        startAfterSignIn = true;   // ★ [자동 시작 버그 수정]
        auth.SignInWithGoogle();

        // ★ [자동 시작 버그 수정] SignInWithGoogle 이 시작도 못 하고 돌아온 경우(초기화 전, 미지원 플랫폼,
        //   클라이언트 ID 비어 있음) 플래그를 남겨 두면, 나중에 다른 방법(이메일)으로 로그인했을 때
        //   엉뚱하게 여기서도 시작이 걸립니다. 그래서 바로 내립니다.
        if (!auth.IsSigningIn) startAfterSignIn = false;
    }

    // ★ [자동 시작 버그 수정] 시작 호출을 한 곳으로 모음 (버튼 클릭 / 로그인 성공 두 곳에서 씀)
    private void StartGame()
    {
        if (loginName == null)
        {
            Debug.LogWarning("[GoogleLoginButtonUI] loginName 이 비어 있어 게임을 시작할 수 없습니다.", this);
            return;
        }
        loginName.StartGame();   // 연타·중복 시작은 Login_Name 의 isLoading 이 막음
    }

    private void Refresh()
    {
        if (googleLoginButton == null) return;   // Awake 에서 비활성화된 경우 방어

        AuthManager auth = AuthManager.Instance;
        bool ready     = auth != null && auth.IsReady;
        bool signingIn = ready && auth.IsSigningIn;
        bool signedIn  = ready && auth.IsSignedIn;

        // ★ [자동 시작 버그 수정] 누를 수 있는 조건
        //   로그인 후 : [시작하기] — 플랫폼과 무관 (에디터에서 이메일로 로그인한 경우도 여기로 시작 가능)
        //   로그인 전 : [Google 로그인] — 지원 플랫폼(Android)에서만
        //   (예전: 로그인 후에는 항상 회색 → 로그인 화면에 머문 유저는 이 버튼으로 시작할 수 없었음)
        googleLoginButton.interactable = ready && !signingIn
                                         && (signedIn || AuthManager.IsGoogleSignInSupported);

        SetText(buttonLabel, signedIn ? startLabel : signInLabel);   // ★ [자동 시작 버그 수정]
        SetText(userIdTMP,   signedIn ? $"UID: {auth.Uid}" : "");
        SetText(userNameTMP, signedIn ? $"이름: {auth.DisplayName}" : "");

        // ★ [자동 시작 버그 수정] 예전 코드 — 이게 원인이었습니다:
        //     if (signedIn && !auth.IsSigningIn && loginName != null) loginName.StartGame();
        //   "로그인돼 있으면 시작" 이라, 자동 로그인만으로도(앱 실행 / 로그인 화면 복귀) 게임이 시작됐습니다.
        //   이제는 "이 버튼으로 방금 로그인했고, 성공했다" 일 때만 시작합니다.
        if (startAfterSignIn && signedIn)
        {
            startAfterSignIn = false;   // StartGame 보다 먼저 내림 — 시작 중 Refresh 가 다시 불려도 두 번 시작하지 않게
            StartGame();
        }
        // 로그인 창이 닫혔는데 로그인이 안 됐다 = 실패 또는 취소 → 대기 해제
        else if (startAfterSignIn && !signingIn && !signedIn)
        {
            startAfterSignIn = false;
        }
    }

    // TMP 는 선택 연결이라 == null 로 검사 (?. 는 Unity 오브젝트의 '미연결/파괴' 를 못 거른다)
    private static void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }
}
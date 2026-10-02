using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoginScene 의 구글 로그인 버튼 UI — 씬 오브젝트 (ManagerRoot 에 넣지 않는다).
///
/// 버튼을 누르면 AuthManager 에 "로그인 해 줘" 라고 부탁만 하고,
/// 화면 갱신은 AuthManager.StateChanged 를 받아 상태를 읽어서 한다 (AuthStateUI).
///   → 씬을 다시 로드하면 이 UI 도 새로 태어나 새 버튼에 리스너를 건다.
///     (UI 와 매니저를 합쳐 ManagerRoot 에 넣으면, 로그인 화면 복귀 시 새 버튼에 아무도 리스너를 걸지 않는다.)
///
/// [버튼의 세 가지 모습]
///   로그인 전            → [Google 로그인]  누르면 로그인, 성공하면 바로 게임 시작
///   구글로 로그인됨      → [시작하기]       누르면 게임 시작
///   다른 방식으로 로그인됨 → 비활성          (이메일 버튼이 [시작하기]. 구글로 바꾸려면 로그아웃 후 로그인)
///
/// [자동 로그인 ≠ 자동 시작]
///   Refresh 는 버튼을 눌렀을 때만이 아니라 씬에 들어올 때·Firebase 준비 완료(자동 로그인)·다른 UI 의 로그인 때도 불립니다.
///   "로그인돼 있으면 시작" 으로 짜면 로그인 화면에 머무를 수 없으므로,
///   "이 버튼으로 방금 로그인했고 성공했다" (startAfterSignIn) 일 때만 자동으로 시작합니다.
/// </summary>
public class GoogleLoginButtonUI : AuthStateUI
{
    [SerializeField] private Login_Name loginName;

    [SerializeField] private Button googleLoginButton;
    [SerializeField] private TextMeshProUGUI userIdTMP;     // (선택/테스트용) Firebase UID
    [SerializeField] private TextMeshProUGUI userNameTMP;   // (선택/테스트용) 구글 표시 이름

    [Header("버튼 글자 (선택)")]
    [Tooltip("(선택) 구글 버튼 안의 TMP. 로그인 전 / 후로 글자를 바꿉니다.\n" +
             "비워 두면 버튼 안의 TMP 를 자동으로 찾고, 씬에 적힌 글자를 '로그인 전' 글자로 씁니다.")]
    [SerializeField] private TextMeshProUGUI buttonLabel;
    [SerializeField] private string signInLabel = "Google 로그인";
    [SerializeField] private string startLabel  = "시작하기";

    // 이 버튼으로 로그인을 시작했는가. 성공이 확인되면 게임을 시작하고 바로 내립니다.
    // 자동 로그인(앱 재실행, 로그인 화면 복귀)은 이 버튼을 누르지 않았으므로 false → 시작하지 않습니다.
    private bool startAfterSignIn;

    private void Awake()
    {
        if (googleLoginButton == null)
        {
            Debug.LogError("[GoogleLoginButtonUI] googleLoginButton 이 연결되지 않았습니다.", this);
            enabled = false;   // OnEnable(구독)도 돌지 않음
            return;
        }
        // 버튼 글자를 연결하지 않았으면 버튼 안의 TMP 를 찾아 씁니다.
        // 씬에 적어 둔 글자는 그대로 '로그인 전' 글자로 두고, 구글 로그인 상태에서만 [시작하기] 로 바꿉니다.
        if (buttonLabel == null)
        {
            buttonLabel = googleLoginButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (buttonLabel != null) signInLabel = buttonLabel.text;
        }

        // 인스펙터의 On Click() 은 비워 둘 것 — 여기서 등록하므로 둘 다 걸면 한 번에 두 번 처리된다
        googleLoginButton.onClick.AddListener(OnClickGoogleLogin);

        if (loginName == null)
            Debug.LogWarning("[GoogleLoginButtonUI] loginName 이 연결되지 않았습니다. 로그인 후 게임이 시작되지 않습니다.", this);
    }

    private void OnDestroy()
    {
        if (googleLoginButton != null)
            googleLoginButton.onClick.RemoveListener(OnClickGoogleLogin);
    }

    private void OnClickGoogleLogin()
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null)
        {
            Debug.LogWarning("[GoogleLoginButtonUI] AuthManager 가 없습니다. ManagerRoot 하위에 추가했는지 확인하세요.");
            return;
        }

        if (auth.IsSignedIn)
        {
            // 구글로 로그인된 상태 → 이 버튼은 [시작하기]. 다른 방식이면 Refresh 가 버튼을 막아 두지만 한 번 더 확인.
            if (auth.IsSignedInWith(AuthManager.SignInMethod.Google)) StartGame();
            return;
        }

        // 로그인 전 → 로그인 시작. 성공하면 Refresh() 에서 시작
        startAfterSignIn = true;
        auth.SignInWithGoogle();

        // 시작도 못 하고 돌아온 경우(초기화 전, 미지원 플랫폼, 클라이언트 ID 비어 있음) 플래그를 남겨 두면,
        // 나중에 다른 방법(이메일)으로 로그인했을 때 엉뚱하게 여기서도 시작이 걸립니다.
        if (!auth.IsSigningIn) startAfterSignIn = false;
    }

    private void StartGame()
    {
        if (loginName == null)
        {
            Debug.LogWarning("[GoogleLoginButtonUI] loginName 이 비어 있어 게임을 시작할 수 없습니다.", this);
            return;
        }
        loginName.StartGame();   // 연타·중복 시작은 Login_Name 의 isLoading 이 막음
    }

    protected override void Refresh()
    {
        if (googleLoginButton == null) return;

        AuthView a = ReadAuth();
        bool signedInWithGoogle = a.SignedInWith(AuthManager.SignInMethod.Google);

        // 누를 수 있는 조건
        //   구글로 로그인됨 : [시작하기] — 플랫폼과 무관
        //   로그인 전       : [Google 로그인] — 지원 플랫폼(Android)에서만
        //   다른 방식으로 로그인됨 : 비활성
        googleLoginButton.interactable = a.Ready && !a.SigningIn
            && (signedInWithGoogle || (!a.SignedIn && AuthManager.IsGoogleSignInSupported));

        SetText(buttonLabel, signedInWithGoogle ? startLabel : signInLabel);
        SetText(userIdTMP,   a.SignedIn ? $"UID: {a.Auth.Uid}" : "");
        SetText(userNameTMP, a.SignedIn ? $"이름: {a.Auth.DisplayName}" : "");

        if (startAfterSignIn && a.SignedIn)
        {
            startAfterSignIn = false;   // StartGame 보다 먼저 내림 — 시작 중 Refresh 가 다시 불려도 두 번 시작하지 않게
            StartGame();
        }
        else if (startAfterSignIn && !a.SigningIn)
        {
            startAfterSignIn = false;   // 로그인 창이 닫혔는데 로그인이 안 됐다 = 실패 또는 취소 → 대기 해제
        }
    }
}

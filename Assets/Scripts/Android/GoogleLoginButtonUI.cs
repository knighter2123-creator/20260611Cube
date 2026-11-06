using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>LoginScene 구글 버튼 UI(씬 오브젝트라 씬마다 새 버튼에 리스너를 검): 로그인 전=[Google 로그인], 구글=[시작하기], 게스트=[구글 연동], 이메일=비활성.</summary>
// 자동 로그인 ≠ 자동 시작 — '이 버튼으로 방금 로그인 성공'(startAfterSignIn)일 때만 게임 시작 (아니면 로그인 화면에 머물 수 없음)
public class GoogleLoginButtonUI : AuthStateUI
{
    [SerializeField] private Login_Name loginName;

    [SerializeField] private Button googleLoginButton;
    [SerializeField] private TextMeshProUGUI userIdTMP;     // (선택/테스트용) 뒤끝 UID (gamer inDate)
    [SerializeField] private TextMeshProUGUI userNameTMP;   // (선택/테스트용) 구글 표시 이름

    [Header("버튼 글자 (선택)")]
    [Tooltip("(선택) 구글 버튼 안의 TMP. 로그인 전 / 후로 글자를 바꿉니다.\n" +
             "비워 두면 버튼 안의 TMP 를 자동으로 찾고, 씬에 적힌 글자를 '로그인 전' 글자로 씁니다.")]
    [SerializeField] private TextMeshProUGUI buttonLabel;
    [SerializeField] private string signInLabel = "Google 로그인";
    [SerializeField] private string startLabel  = "시작하기";
    [Tooltip("게스트로 로그인돼 있을 때 — 누르면 지금 게스트 계정에 구글을 연결 (진행 유지)")]
    [SerializeField] private string linkLabel   = "구글 연동";

    // 이 버튼으로 로그인을 시작했는가 (자동 로그인은 false → 시작 안 함)
    private bool startAfterSignIn;

    private void Awake()
    {
        if (googleLoginButton == null)
        {
            Debug.LogError("[GoogleLoginButtonUI] googleLoginButton 이 연결되지 않았습니다.", this);
            enabled = false;   // OnEnable(구독)도 돌지 않음
            return;
        }
        // 글자 칸이 비면 버튼 안 TMP 를 찾아 씬 글자를 '로그인 전' 글자로 사용
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

        if (auth.IsGuest)
        {
            // 게스트 → [구글 연동]. 같은 계정에 구글을 붙임 — 연동만 하고 자동으로 시작하지는 않음 (버튼이 [시작하기] 로 바뀜)
            auth.SignInWithGoogle();
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

        // 시작도 못 했으면 플래그를 내림 — 남기면 나중에 이메일 로그인 때 여기서도 시작이 걸림
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
        bool guest              = a.SignedInWith(AuthManager.SignInMethod.Guest);

        // 누를 수 있음: 구글로 로그인됨(플랫폼 무관) 또는 로그인 전·게스트(Android 에서만), 이메일 로그인은 비활성
        googleLoginButton.interactable = a.Ready && !a.SigningIn
            && (signedInWithGoogle || ((!a.SignedIn || guest) && AuthManager.IsGoogleSignInSupported));

        SetText(buttonLabel, signedInWithGoogle ? startLabel : guest ? linkLabel : signInLabel);
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

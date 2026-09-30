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
/// </summary>
public class GoogleLoginButtonUI : MonoBehaviour
{
    [SerializeField] private Button googleLoginButton;
    [SerializeField] private TextMeshProUGUI userIdTMP;     // (선택/테스트용) Firebase UID
    [SerializeField] private TextMeshProUGUI userNameTMP;   // (선택/테스트용) 구글 표시 이름

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
        auth.SignInWithGoogle();
    }

    private void Refresh()
    {
        if (googleLoginButton == null) return;   // Awake 에서 비활성화된 경우 방어

        AuthManager auth = AuthManager.Instance;
        bool ready    = auth != null && auth.IsReady;
        bool signedIn = ready && auth.IsSignedIn;

        // 지원 플랫폼 + 준비됨 + 진행 중 아님 + 아직 로그인 안 됨 일 때만 누를 수 있다
        // (에디터/iOS 에서는 항상 비활성 — AuthManager.IsGoogleSignInSupported 참고)
        googleLoginButton.interactable = AuthManager.IsGoogleSignInSupported
                                         && ready && !auth.IsSigningIn && !signedIn;

        SetText(userIdTMP,   signedIn ? $"UID: {auth.Uid}" : "");
        SetText(userNameTMP, signedIn ? $"이름: {auth.DisplayName}" : "");
    }

    // TMP 는 선택 연결이라 == null 로 검사 (?. 는 Unity 오브젝트의 '미연결/파괴' 를 못 거른다)
    private static void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }
}
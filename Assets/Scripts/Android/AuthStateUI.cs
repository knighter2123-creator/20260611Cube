using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 상태를 그리는 LoginScene UI 들의 공통 부모. (GoogleLoginButtonUI / EmailLoginUI / LogoutButtonUI)
///
/// 세 UI 가 똑같이 하던 일을 한 곳에 모았습니다.
///   · OnEnable 에서 AuthManager.StateChanged 구독 + 즉시 한 번 Refresh
///     (이벤트만 기다리면, 구독 전에 이미 끝난 일 — 자동 로그인 등 — 을 영영 못 봅니다)
///   · OnDisable 에서 해제 (static 이벤트는 파괴된 구독자도 계속 붙잡습니다)
///   · "준비됨 / 처리 중 / 로그인됨" 을 같은 규칙으로 읽기 (ReadAuth)
///
/// 자식은 Refresh() 에서 화면만 그립니다. 상태를 그리는 곳이 Refresh 한 곳이어야 화면과 실제가 어긋나지 않습니다.
/// </summary>
public abstract class AuthStateUI : MonoBehaviour
{
    /// <summary>false 면 구독하지 않습니다 (예: 출시 빌드에서 숨긴 테스트 전용 UI).</summary>
    protected virtual bool ListensToAuth => true;

    protected virtual void OnEnable()
    {
        if (!ListensToAuth) return;

        AuthManager.StateChanged += Refresh;
        Refresh();
    }

    protected virtual void OnDisable()
    {
        AuthManager.StateChanged -= Refresh;   // 구독하지 않았어도 해제는 무해
    }

    /// <summary>AuthManager 의 현재 상태로 화면을 다시 그립니다.</summary>
    protected abstract void Refresh();

    // ─────────────────────────── 상태 읽기 ───────────────────────────

    /// <summary>한 번 읽은 로그인 상태. 준비 전이면 나머지 값은 전부 false 입니다.</summary>
    protected readonly struct AuthView
    {
        public readonly AuthManager Auth;   // 없으면 null
        public readonly bool Ready;
        public readonly bool SigningIn;
        public readonly bool SignedIn;

        public AuthView(AuthManager auth)
        {
            Auth      = auth;
            Ready     = auth != null && auth.IsReady;
            SigningIn = Ready && auth.IsSigningIn;
            SignedIn  = Ready && auth.IsSignedIn;
        }

        /// <summary>지금 이 방식으로 로그인돼 있는가 (준비 전이면 false).</summary>
        public bool SignedInWith(AuthManager.SignInMethod method) => SignedIn && Auth.IsSignedInWith(method);
    }

    /// <summary>매번 Instance 를 새로 읽습니다 (계정 삭제·전환 후 매니저가 새로 만들어질 수 있음).</summary>
    protected static AuthView ReadAuth() => new AuthView(AuthManager.Instance);

    // ─────────────────────────── 선택 연결 도우미 ───────────────────────────
    // Unity 오브젝트는 ?. 대신 == null 로 검사합니다 (?. 는 '미연결/파괴' 를 못 거릅니다).

    protected static void SetText(TMP_Text tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    protected static void SetInteractable(Button button, bool value)
    {
        if (button != null) button.interactable = value;
    }
}

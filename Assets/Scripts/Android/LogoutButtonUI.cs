using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★ 신규. LoginScene 의 로그아웃 버튼 — 씬 오브젝트 (ManagerRoot 에 넣지 않는다).
///
/// [동작]
///   로그인돼 있을 때만 보임 → 누르면 AuthManager.SignOut()
///     → 구글·이메일 어느 방식이든 로그아웃 (Firebase + 구글 계정 선택 상태)
///     → 앱 종료 X, 씬 이동 X — 로그인 화면에 그대로 남아 바로 다시 로그인할 수 있음
///   화면 갱신은 다른 로그인 UI 와 같은 규칙: AuthManager.StateChanged 를 받아 상태를 읽어서 그림.
///   그래서 로그아웃하면 GoogleLoginButtonUI 는 [Google 로그인] 으로, EmailLoginUI 의 [이메일로 로그인] 은 다시 활성으로
///   자동으로 돌아갑니다 (이 스크립트가 그 UI 들을 알 필요 없음).
///
/// ⚠ 이 스크립트는 로그아웃 버튼 "밖" 의 항상 켜진 오브젝트에 붙이세요 (EmailLoginUI 와 같은 이유).
///   버튼 오브젝트 자신에 붙이면, 로그아웃 후 버튼을 숨기는 순간 이 스크립트도 꺼져서(OnDisable → 구독 해제)
///   다시 로그인해도 버튼이 영영 안 나타납니다.
/// </summary>
public class LogoutButtonUI : MonoBehaviour
{
    [SerializeField] private Button logoutButton;

    [Tooltip("(선택) 로그인된 계정 표시. 예: \"Google · user@gmail.com\"")]
    [SerializeField] private TMP_Text accountText;

    [Tooltip("체크: 로그인 안 됐을 때 버튼을 숨김 / 해제: 숨기지 않고 회색(비활성)으로만 둠")]
    [SerializeField] private bool hideWhenSignedOut = true;

    private void Awake()
    {
        if (logoutButton == null)
        {
            Debug.LogError("[LogoutButtonUI] logoutButton 이 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        // 배치 실수 방어 — 위 ⚠ 참고
        if (transform.IsChildOf(logoutButton.transform))
            Debug.LogError("[LogoutButtonUI] 로그아웃 버튼(또는 그 안)에 붙어 있습니다. 버튼을 숨길 때 같이 꺼져서 " +
                           "다시 나타나지 않습니다. 버튼 밖의 항상 켜진 오브젝트로 옮겨 주세요.", this);

        // 인스펙터의 On Click() 은 비워 둘 것 — 여기서 등록하므로 둘 다 걸면 한 번에 두 번 처리됩니다
        logoutButton.onClick.AddListener(OnClickLogout);
    }

    private void OnEnable()
    {
        // 구독 + 즉시 한 번 갱신 (앱 재실행 시 자동 로그인된 상태도 바로 반영)
        AuthManager.StateChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        AuthManager.StateChanged -= Refresh;   // static 이벤트는 반드시 해제
    }

    private void OnDestroy()
    {
        if (logoutButton != null) logoutButton.onClick.RemoveListener(OnClickLogout);
    }

    private void OnClickLogout()
    {
        AuthManager auth = AuthManager.Instance;   // 매번 새로 읽음
        if (auth == null)
        {
            Debug.LogWarning("[LogoutButtonUI] AuthManager 가 없습니다. ManagerRoot 하위에 추가했는지 확인하세요.");
            return;
        }

        // 로그아웃만 부탁합니다. 화면은 SignOut 안의 Notify → StateChanged → Refresh 로 바뀝니다.
        // (여기서 직접 버튼을 숨기지 않는 이유: 상태를 그리는 곳이 Refresh 한 곳이어야 화면과 실제 상태가 어긋나지 않음)
        auth.SignOut();
    }

    private void Refresh()
    {
        if (logoutButton == null) return;

        AuthManager auth = AuthManager.Instance;
        bool ready     = auth != null && auth.IsReady;
        bool signingIn = ready && auth.IsSigningIn;
        bool signedIn  = ready && auth.IsSignedIn;

        // 로그인돼 있고, 로그인 처리 중이 아닐 때만 누를 수 있음 (처리 중 로그아웃은 AuthManager 도 막음)
        if (hideWhenSignedOut)
            logoutButton.gameObject.SetActive(signedIn);
        logoutButton.interactable = signedIn && !signingIn;

        // 어느 계정인지 표시 — 구글/이메일 중 무엇으로 들어왔는지 알아야 '다른 계정으로 바꾸기' 판단이 쉬움
        if (accountText != null)
        {
            if (!signedIn)
            {
                accountText.text = string.Empty;
            }
            else
            {
                string provider = auth.SignInProviderLabel;
                string who      = !string.IsNullOrEmpty(auth.Email) ? auth.Email : auth.DisplayName;
                accountText.text = string.IsNullOrEmpty(provider) ? who : $"{provider} · {who}";
            }
        }
    }
}

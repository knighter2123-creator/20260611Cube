using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>LoginScene 로그아웃 버튼: 로그인돼 있을 때만 보이고 누르면 AuthManager.SignOut (씬 이동 없이 바로 다시 로그인 가능), 화면은 AuthStateUI 규칙으로 갱신.</summary>
// ⚠ 버튼 '밖'의 항상 켜진 오브젝트에 붙일 것 — 버튼 자신에 붙이면 숨길 때 같이 꺼져 구독이 풀리고 다시 안 나타남
public class LogoutButtonUI : AuthStateUI
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

    private void OnDestroy()
    {
        if (logoutButton != null) logoutButton.onClick.RemoveListener(OnClickLogout);
    }

    private void OnClickLogout()
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null)
        {
            Debug.LogWarning("[LogoutButtonUI] AuthManager 가 없습니다. ManagerRoot 하위에 추가했는지 확인하세요.");
            return;
        }

        // 로그아웃만 부탁합니다. 화면은 SignOut 안의 Notify → StateChanged → Refresh 로 바뀝니다.
        auth.SignOut();
    }

    protected override void Refresh()
    {
        if (logoutButton == null) return;

        AuthView a = ReadAuth();

        // 로그인돼 있고, 로그인 처리 중이 아닐 때만 누를 수 있음 (처리 중 로그아웃은 AuthManager 도 막음)
        if (hideWhenSignedOut)
            logoutButton.gameObject.SetActive(a.SignedIn);
        logoutButton.interactable = a.SignedIn && !a.SigningIn;

        // 어느 계정인지 표시 — 구글/이메일 중 무엇으로 들어왔는지 알아야 '다른 계정으로 바꾸기' 판단이 쉬움
        SetText(accountText, a.SignedIn ? AccountLabel(a.Auth) : string.Empty);
    }

    private static string AccountLabel(AuthManager auth)
    {
        string provider = auth.SignInProviderLabel;
        string who      = !string.IsNullOrEmpty(auth.Email) ? auth.Email : auth.DisplayName;
        return string.IsNullOrEmpty(provider) ? who : $"{provider} · {who}";
    }
}

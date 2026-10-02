using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// LoginScene 의 이메일 로그인 / 회원가입 UI — 씬 오브젝트 (ManagerRoot 에 넣지 않는다).
///
/// 구조는 GoogleLoginButtonUI 와 같습니다 (AuthStateUI).
///   - 버튼을 누르면 AuthManager 에 부탁만 하고 (SignInWithEmail / SignUpWithEmail)
///   - 화면 갱신은 AuthManager.StateChanged 를 받아 상태(IsSigningIn, IsSignedIn, LastError)를 읽어서 함
///   - 로그인에 성공하면 onStartGame (인스펙터에서 Login_Name.StartGame 연결) 으로 게임 시작
///
///     로그인 화면  [Google 로그인] [게스트로 시작하기] [이메일로 로그인]
///                                                        └─ 누르면 ▼
///     이메일 패널  ┌ 이메일 / 비밀번호 / [로그인] [회원가입] / 메시지 / [닫기] ┐
///
/// [이메일로 로그인] 버튼의 세 가지 모습
///   로그인 전              → 누르면 이메일 패널이 열림
///   이메일로 로그인됨      → [시작하기] — 패널 없이 바로 게임 시작
///   다른 방식으로 로그인됨 → 비활성 (구글 버튼이 [시작하기]. 이메일로 바꾸려면 로그아웃 후 로그인)
///
///   ⚠ 이 스크립트는 패널 "밖" 의 항상 켜진 오브젝트에 붙이세요 (Login_Name 과 같은 규칙).
///     패널에 붙이면, 패널이 꺼진 채 시작할 때 Awake/OnEnable 이 불리지 않아
///     [이메일로 로그인] 버튼에 리스너가 걸리지 않고 → 눌러도 아무 반응이 없습니다.
/// </summary>
public class EmailLoginUI : AuthStateUI
{
    [Header("패널")]
    [Tooltip("이메일 로그인 패널 루트. 시작할 때 자동으로 닫힙니다 (씬에 켜 둔 채 저장해도 안전).")]
    [FormerlySerializedAs("emailpanel")]
    [SerializeField] private GameObject emailPanel;

    [Tooltip("로그인 화면의 [이메일로 로그인] 버튼 (패널 밖).\n" +
             "로그인 전에는 패널을 열고, 이메일로 로그인된 상태에서는 바로 게임을 시작합니다.")]
    [SerializeField] private Button openButton;

    [Tooltip("(선택) 패널 안의 [닫기] 버튼 또는 반투명 배경(Dim)에 붙인 Button")]
    [SerializeField] private Button closeButton;

    [Header("[이메일로 로그인] 버튼 글자 (선택)")]
    [Tooltip("(선택) Open Button 안의 TMP. 로그인 전 / 후로 글자를 바꿉니다.\n" +
             "비워 두면 버튼 안의 TMP 를 자동으로 찾고, 씬에 적힌 글자를 '로그인 전' 글자로 씁니다.")]
    [SerializeField] private TMP_Text openButtonLabel;
    [SerializeField] private string openLabel  = "이메일로 로그인";
    [SerializeField] private string startLabel = "시작하기";

    [Header("입력")]
    [SerializeField] private TMP_InputField emailInput;
    [SerializeField] private TMP_InputField passwordInput;

    [Header("버튼")]
    [SerializeField] private Button signInButton;   // 로그인
    [SerializeField] private Button signUpButton;   // 회원가입 (선택)

    [Header("표시 (선택)")]
    [SerializeField] private TMP_Text messageText;  // 실패 사유 / 진행 중 / 로그인됨

    [Header("배포")]
    [Tooltip("체크하면 Development Build 와 에디터에서만 보이고, 출시(릴리스) 빌드에서는 [이메일로 로그인] 버튼과 패널이 숨겨집니다.\n" +
             "이메일 로그인을 '테스트용' 으로만 쓴다면 체크하세요. 플레이어용으로 내보내려면 비밀번호 재설정 기능이 먼저 필요합니다.")]
    [SerializeField] private bool developmentBuildOnly = false;

    [Header("게임 시작 연결")]
    [Tooltip("이메일 로그인(또는 가입) 성공 후 / 로그인된 상태에서 [시작하기] 를 눌렀을 때 호출할 함수.\n" +
             "'게스트로 시작하기' / 구글 버튼과 같은 Login_Name.StartGame 을 연결하세요.")]
    [SerializeField] private UnityEvent onStartGame = new UnityEvent();

    // 이 UI 로 방금 로그인을 시작했는가 — 자동 로그인(앱 재실행)과 구분해서, 방금 누른 경우만 게임을 자동 시작
    private bool startAfterSignIn;

    // 패널을 연 뒤 한 번이라도 시도했는가.
    // AuthManager.LastError 는 지난 시도의 실패 문구를 계속 들고 있어서, 패널을 닫았다 다시 열면
    // 아무것도 안 했는데 "비밀번호가 맞지 않습니다" 가 떠 있게 됩니다. 이번에 연 패널에서 시도한 뒤에만 보여 줍니다.
    private bool attemptedSinceOpen;

    // 출시 빌드 + 테스트 전용 설정이라 이 UI 를 쓰지 않는 상태
    private bool disabledForRelease;

    private bool IsPanelOpen => emailPanel != null && emailPanel.activeSelf;

    protected override bool ListensToAuth => !disabledForRelease;

    private void Awake()
    {
        // 패널은 처음에 항상 닫힌 상태로 시작
        if (emailPanel != null) emailPanel.SetActive(false);

        // 테스트 전용이면 출시 빌드에서 [이메일로 로그인] 버튼만 콕 집어 숨깁니다.
        // (이 스크립트가 붙은 오브젝트를 끄면 같은 부모의 구글·게스트 버튼까지 사라질 수 있음)
        // Debug.isDebugBuild: 에디터 / Development Build 에서 true, 출시 빌드에서 false.
        if (developmentBuildOnly && !Debug.isDebugBuild)
        {
            disabledForRelease = true;
            if (openButton != null) openButton.gameObject.SetActive(false);
            return;   // 리스너도 걸지 않음
        }

        if (emailPanel == null)
            Debug.LogError("[EmailLoginUI] emailPanel 이 연결되지 않았습니다.", this);
        else if (transform.IsChildOf(emailPanel.transform))
            Debug.LogError("[EmailLoginUI] 이 스크립트가 이메일 패널 안에 붙어 있습니다. 패널이 꺼지면 같이 꺼져서 " +
                           "[이메일로 로그인] 버튼이 동작하지 않습니다. 패널 밖의 항상 켜진 오브젝트로 옮겨 주세요.", this);

        // 비밀번호 칸은 코드에서 한 번 더 '가림' 으로 고정합니다.
        // 인스펙터에서 Content Type 을 Password 로 바꾸는 걸 깜빡하면 비밀번호가 화면에 그대로 보이기 때문입니다.
        if (passwordInput != null)
        {
            passwordInput.contentType = TMP_InputField.ContentType.Password;
            passwordInput.ForceLabelUpdate();   // 바뀐 Content Type 을 화면 표시에 바로 반영
            passwordInput.onSubmit.AddListener(OnPasswordSubmit);   // 키보드 '완료' → 바로 로그인
        }
        if (emailInput != null)
        {
            emailInput.contentType = TMP_InputField.ContentType.EmailAddress;   // 모바일: @ 가 있는 키보드
            emailInput.onSubmit.AddListener(OnEmailSubmit);         // 키보드 '완료' → 비밀번호 칸으로
        }

        // 버튼 글자를 연결하지 않았으면 버튼 안의 TMP 를 찾아 씁니다.
        // 씬에 적어 둔 글자는 그대로 '로그인 전' 글자로 두고, 이메일 로그인 상태에서만 [시작하기] 로 바꿉니다.
        if (openButtonLabel == null && openButton != null)
        {
            openButtonLabel = openButton.GetComponentInChildren<TMP_Text>(true);
            if (openButtonLabel != null) openLabel = openButtonLabel.text;
        }

        // 인스펙터의 On Click() 은 비워 둘 것 — 여기서 등록하므로 둘 다 걸면 한 번에 두 번 처리됩니다
        if (openButton   != null) openButton.onClick.AddListener(OnClickOpen);
        if (closeButton  != null) closeButton.onClick.AddListener(ClosePanel);
        if (signInButton != null) signInButton.onClick.AddListener(OnClickSignIn);
        if (signUpButton != null) signUpButton.onClick.AddListener(OnClickSignUp);

        if (onStartGame.GetPersistentEventCount() == 0)
            Debug.LogWarning("[EmailLoginUI] onStartGame 이 비어 있습니다. 로그인 후 게임이 시작되지 않습니다.", this);
    }

    private void OnDestroy()
    {
        if (openButton   != null) openButton.onClick.RemoveListener(OnClickOpen);
        if (closeButton  != null) closeButton.onClick.RemoveListener(ClosePanel);
        if (signInButton != null) signInButton.onClick.RemoveListener(OnClickSignIn);
        if (signUpButton != null) signUpButton.onClick.RemoveListener(OnClickSignUp);
        if (passwordInput != null) passwordInput.onSubmit.RemoveListener(OnPasswordSubmit);
        if (emailInput    != null) emailInput.onSubmit.RemoveListener(OnEmailSubmit);
    }

    // ───────────────────────────── [이메일로 로그인] 버튼 ─────────────────────────────

    /// <summary>
    /// 이메일로 로그인된 상태면 [시작하기] — 바로 게임 시작.
    /// 로그인 전이면 패널을 엽니다. (다른 방식으로 로그인된 상태면 Refresh 가 버튼을 막아 둡니다)
    /// </summary>
    private void OnClickOpen()
    {
        AuthManager auth = AuthManager.Instance;
        if (auth != null && auth.IsSignedIn)
        {
            if (auth.IsSignedInWith(AuthManager.SignInMethod.Email)) StartGame();
            return;
        }

        OpenPanel();
    }

    // ───────────────────────────── 패널 열기 / 닫기 ─────────────────────────────

    /// <summary>패널을 열고 이메일 칸에 커서를 둡니다. 이미 로그인된 상태면 열지 않습니다.</summary>
    public void OpenPanel()
    {
        if (emailPanel == null || IsPanelOpen) return;

        AuthManager auth = AuthManager.Instance;
        if (auth != null && auth.IsSignedIn) return;

        attemptedSinceOpen = false;   // 새로 연 패널 — 지난 실패 문구는 숨김
        emailPanel.SetActive(true);
        Refresh();

        // SetActive(true) 직후에 바로 입력칸을 활성화하면 무시될 수 있어 한 프레임 뒤에 처리
        if (emailInput != null) StartCoroutine(FocusNextFrame(emailInput));
    }

    /// <summary>[닫기] / 배경 터치. 로그인 처리 중에는 닫지 않습니다 (Refresh 가 닫기 버튼도 막음).</summary>
    public void ClosePanel()
    {
        if (emailPanel == null) return;

        // 처리 중에 닫으면, 결과가 왔을 때 패널은 닫혀 있는데 게임이 갑자기 시작되거나 실패 문구가 안 보입니다.
        AuthManager auth = AuthManager.Instance;
        if (auth != null && auth.IsSigningIn) return;

        if (passwordInput != null) passwordInput.text = string.Empty;   // 닫으면 비밀번호는 지움 (이메일은 편의상 남김)
        emailPanel.SetActive(false);
    }

    private IEnumerator FocusNextFrame(TMP_InputField field)
    {
        yield return null;   // 한 프레임 대기
        if (field != null && IsPanelOpen && field.interactable)
            field.ActivateInputField();   // 모바일: 가상 키보드가 올라옴
    }

    // 이메일 칸에서 키보드 '완료' → 비밀번호 칸으로 이동
    private void OnEmailSubmit(string _)
    {
        if (passwordInput != null && IsPanelOpen) passwordInput.ActivateInputField();
    }

    // 비밀번호 칸에서 키보드 '완료' → 로그인 (회원가입은 버튼으로만 — 실수로 계정이 만들어지지 않게)
    private void OnPasswordSubmit(string _)
    {
        if (IsPanelOpen) Begin(signUp: false);
    }

    // ───────────────────────────── 로그인 / 가입 ─────────────────────────────

    private void OnClickSignIn() => Begin(signUp: false);
    private void OnClickSignUp() => Begin(signUp: true);

    private void Begin(bool signUp)
    {
        AuthManager auth = AuthManager.Instance;
        if (auth == null)
        {
            SetText(messageText, "로그인 시스템을 찾을 수 없습니다.");
            Debug.LogWarning("[EmailLoginUI] AuthManager 가 없습니다. ManagerRoot 하위에 추가했는지 확인하세요.");
            return;
        }

        // 이미 처리 중/로그인됨이면 무시 — 키보드 '완료' 는 버튼이 막혀 있어도 들어올 수 있음
        if (auth.IsSigningIn || auth.IsSignedIn) return;

        string email    = emailInput    != null ? emailInput.text    : string.Empty;
        string password = passwordInput != null ? passwordInput.text : string.Empty;

        attemptedSinceOpen = true;
        startAfterSignIn = true;
        if (signUp) auth.SignUpWithEmail(email, password);
        else        auth.SignInWithEmail(email, password);

        // 입력 검사에서 바로 막혀 시작도 못 했으면 대기 해제 (나중에 엉뚱한 때 게임이 시작되지 않게)
        if (!auth.IsSigningIn) startAfterSignIn = false;
    }

    private void StartGame()
    {
        // 비밀번호는 더 이상 필요 없으므로 화면에서 지움 (씬이 바뀌기 전 잠깐이라도 남지 않게)
        if (passwordInput != null) passwordInput.text = string.Empty;

        if (onStartGame.GetPersistentEventCount() == 0)
        {
            Debug.LogWarning("[EmailLoginUI] onStartGame 이 비어 있어 게임을 시작할 수 없습니다.", this);
            return;
        }
        onStartGame.Invoke();   // 연타·중복 시작은 Login_Name 의 isLoading 이 막음
    }

    // ───────────────────────────── 화면 갱신 ─────────────────────────────

    protected override void Refresh()
    {
        AuthView a = ReadAuth();
        bool signedInWithEmail = a.SignedInWith(AuthManager.SignInMethod.Email);

        // [이메일로 로그인] 버튼: 로그인 전(패널 열기) 또는 이메일로 로그인됨([시작하기])일 때만
        SetInteractable(openButton, a.Ready && !a.SigningIn && (!a.SignedIn || signedInWithEmail));
        SetText(openButtonLabel, signedInWithEmail ? startLabel : openLabel);

        // 닫기: 처리 중에는 막음 (ClosePanel 의 이유와 같음)
        SetInteractable(closeButton, !a.SigningIn);

        // 준비됨 + 진행 중 아님 + 아직 로그인 안 됨 일 때만 입력·버튼 사용 가능
        bool canUse = a.Ready && !a.SigningIn && !a.SignedIn;
        SetInteractable(signInButton, canUse);
        SetInteractable(signUpButton, canUse);
        if (emailInput    != null) emailInput.interactable    = canUse;
        if (passwordInput != null) passwordInput.interactable = canUse;

        // 문구 우선순위: 진행 중 > 로그인됨 > (이번에 연 패널에서 시도한) 실패 사유 > 없음
        if (a.SigningIn)     SetText(messageText, "처리 중…");
        else if (a.SignedIn) SetText(messageText, "로그인되었습니다.");
        else if (a.Ready && attemptedSinceOpen && !string.IsNullOrEmpty(a.Auth.LastError))
                             SetText(messageText, a.Auth.LastError);
        else                 SetText(messageText, string.Empty);

        // 방금 이 UI 로 로그인했고 성공 → 게임 시작 (한 번만)
        if (startAfterSignIn && a.SignedIn)
        {
            startAfterSignIn = false;   // Invoke 보다 먼저 내림 — 시작 처리 중 Refresh 가 다시 불려도 두 번 시작하지 않게
            StartGame();
        }
        else if (startAfterSignIn && !a.SigningIn)
        {
            startAfterSignIn = false;   // 진행이 끝났는데 로그인이 안 됨 = 실패 → 대기 해제
        }
    }
}

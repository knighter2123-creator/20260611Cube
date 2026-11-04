using System;
using System.Threading.Tasks;
using BackEnd;
using Google;                // GoogleSignIn (google-signin-unity 플러그인)
using UnityEngine;

/// <summary>
/// 로그인(인증) 매니저 — LoginScene 의 ManagerRoot 하위에 둔다. 서버는 뒤끝(BackendManager).
///
/// [하는 일]  뒤끝 초기화 요청, 구글 로그인 → 뒤끝 로그인, 이메일 가입/로그인, 자동 로그인, 로그아웃, 탈퇴, 로그인 상태 보관.
/// [안 하는 일] UI. 버튼/텍스트 참조를 하나도 들고 있지 않다.
///   → 이 매니저는 DontDestroyOnLoad 로 씬을 넘어 살아남지만, 버튼은 씬과 함께 파괴된다.
///     UI 는 GoogleLoginButtonUI / EmailLoginUI 등이 맡고, 이 매니저의 상태를 읽어 갈 뿐이다.
///
/// [예전 Firebase 버전에서 옮겨 오며 지킨 것]  공개 API(IsReady / IsSignedIn / Uid / StateChanged / SignInWith... )는 그대로입니다.
///   로그인 화면 UI, AccountSwitch, 계정별 세이브는 고치지 않아도 됩니다.
///   Uid 는 이제 뒤끝의 유저 inDate 입니다 (다른 유저를 가리키는 뒤끝 API — 친구, 랭킹 등 — 가 쓰는 값).
///
/// [구글 로그인 — 2단계]
///   1) GoogleSignIn : 구글 계정 주인임을 증명하는 ID 토큰을 받는다 (기기 네이티브 창)
///   2) 뒤끝         : AuthorizeFederation(토큰, Google) 로 로그인. 처음이면 자동 가입.
///   ★ 뒤끝 콘솔 > 프로젝트 설정 > 구글 로그인(페더레이션) 에 같은 구글 클라이언트 ID 를 등록해야 합니다.
///
/// [이메일 로그인]  뒤끝 '커스텀 가입/로그인' — 이메일을 아이디로 씁니다.
///   가입하면 비밀번호 찾기용 이메일(UpdateCustomEmail)도 같은 주소로 등록합니다.
///   네이티브 플러그인이 필요 없어서 **에디터에서도 동작**합니다.
///
/// [자동 로그인 / 오프라인]
///   뒤끝은 로그인 토큰을 기기에 보관합니다. 앱을 다시 켜면 LoginWithTheBackendToken 으로 이어서 로그인합니다.
///   서버에 닿지 못하면(오프라인·점검) 마지막 계정을 '오프라인 로그인' 으로 유지합니다 — IsSignedIn = true, IsOnline = false.
///     → 비행기 모드로 켜도 게스트 세이브가 아니라 내 계정 세이브로 이어서 할 수 있습니다 (Firebase 때와 같은 동작).
///     → 서버 기능은 IsOnline 을 보고 기다리면 됩니다. 연결되면 자동으로 다시 로그인하고 StateChanged 를 보냅니다.
///   토큰이 만료·탈퇴·차단으로 거부되면 로그아웃 상태가 됩니다.
///
/// [로그아웃]  뒤끝 + 구글 계정 선택 상태를 지웁니다. 앱 종료·씬 이동·세이브 삭제는 하지 않습니다.
/// [세이브]   CaptureTo/ApplyFrom 이 없다 → save.json 스키마와 무관. 마지막 계정 정보만 PlayerPrefs 에 따로 둔다.
/// </summary>
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    /// <summary>
    /// 로그인 상태가 바뀔 때마다 발행 (준비 완료 / 로그인 시작 / 성공 / 실패·취소 / 로그아웃 / 온라인·오프라인 전환).
    /// 구독하는 쪽은 인자 없이 받고, 필요한 값은 Instance 의 프로퍼티로 직접 읽는다.
    /// ★ static 인 이유: UI 의 OnEnable 이 이 매니저의 Awake 보다 먼저 불려도 구독할 수 있게. 계정 삭제로 매니저가 새로 만들어져도 유지.
    /// ⚠ 구독한 쪽은 반드시 OnDisable 에서 해제할 것.
    /// </summary>
    public static event Action StateChanged;

    [Header("Google Sign-In")]
    [Tooltip("구글 클라우드 콘솔 > API 및 서비스 > 사용자 인증 정보 의 'OAuth 웹 클라이언트 ID'.\n" +
             "뒤끝 콘솔의 구글 로그인 설정에도 같은 값을 넣습니다.\n" +
             "Android 클라이언트 ID 를 넣으면 DEVELOPER_ERROR 가 난다.")]
    [SerializeField] private string webClientId = "";

    [Tooltip("오프라인으로 시작했을 때 다시 로그인을 시도하는 간격(초)")]
    [SerializeField] private float reconnectInterval = 30f;

    // ─────────────── 외부에서 읽는 상태 ───────────────
    /// <summary>시작할 때의 확인(뒤끝 초기화 + 자동 로그인 시도)을 마쳤는가. 실패·오프라인이어도 확인을 마치면 true.</summary>
    public bool IsReady     { get; private set; }
    public bool IsSigningIn { get; private set; }   // 로그인 창·요청이 진행 중
    public bool IsSignedIn  => !string.IsNullOrEmpty(uid);

    /// <summary>뒤끝 서버와 로그인 세션이 살아 있는가. 서버 기능(랭킹, 우편, 세이브 등)은 이것을 보고 요청합니다.</summary>
    public bool IsOnline    => IsSignedIn && online && Backend.IsLogin;

    /// <summary>구글 로그인을 켜 둘 플랫폼. 지금은 Android 만 (에디터·iOS 는 네이티브 설정이 없음).</summary>
    public static bool IsGoogleSignInSupported => Application.platform == RuntimePlatform.Android;

    /// <summary>게임이 계정을 구분하는 값 = 뒤끝 유저 inDate. 로그인 안 됨이면 null.</summary>
    public string Uid         => IsSignedIn ? uid : null;

    /// <summary>뒤끝 닉네임 (아직 없으면 null). 게임 표시 이름은 PlayerProfile.DisplayName 을 쓰세요.</summary>
    public string DisplayName => IsSignedIn && !string.IsNullOrEmpty(nickname) ? nickname : null;

    /// <summary>로그인한 계정의 이메일 (구글/이메일 공통). 로그아웃 버튼 옆 "어느 계정인지" 표시용. 모르면 null.</summary>
    public string Email       => IsSignedIn && !string.IsNullOrEmpty(email) ? email : null;

    /// <summary>로그인 방식. 로그인 화면의 버튼(구글 / 이메일 / 게스트로 시작)과 1:1 로 대응합니다.</summary>
    public enum SignInMethod { Google, Email, Guest }

    /// <summary>지금 이 방식으로 로그인돼 있는가 (로그인 화면의 각 버튼이 [시작하기] 를 판단할 때).</summary>
    public bool IsSignedInWith(SignInMethod m) => IsSignedIn && hasMethod && method == m;

    /// <summary>뒤끝 게스트 계정으로 로그인돼 있는가. 구글 버튼이 [구글 연동] 으로 바뀝니다.</summary>
    public bool IsGuest => IsSignedInWith(SignInMethod.Guest);

    /// <summary>"Google" / "이메일" / "게스트" / "" — 로그아웃 버튼 옆 표시용.</summary>
    public string SignInProviderLabel
    {
        get
        {
            if (IsSignedInWith(SignInMethod.Google)) return "Google";
            if (IsSignedInWith(SignInMethod.Email))  return "이메일";
            if (IsSignedInWith(SignInMethod.Guest))  return "게스트";
            return string.Empty;
        }
    }

    /// <summary>마지막 실패 사유 (UI 표시용, 한국어). 새 시도를 시작하면 비워집니다.</summary>
    public string LastError { get; private set; }

    /// <summary>비밀번호 최소 길이. UI 안내 문구도 이 값을 씁니다. (Firebase 때와 같은 6자 유지)</summary>
    public const int MIN_PASSWORD_LENGTH = 6;

    // ─────────────── 내부 상태 ───────────────
    private string       uid;
    private string       email;
    private string       nickname;
    private SignInMethod method;
    private bool         hasMethod;
    private bool         online;
    private float        nextReconnectTime;
    private bool         reconnecting;

    // 마지막 계정 — 앱을 다시 켰을 때 자동 로그인 · 오프라인 로그인에 씀 (세이브 파일과 별개)
    private const string PREF_UID    = "auth.backend.uid";
    private const string PREF_METHOD = "auth.backend.method";   // "google" / "email"
    private const string PREF_EMAIL  = "auth.backend.email";

    // GoogleSignIn.Configuration 은 DefaultInstance 가 만들어진 뒤 다시 대입하면 예외를 던진다 → static 으로 기억 (ResetStatics 에서 되돌리지 않음)
    private static bool isGoogleConfigured;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        StateChanged = null;
    }

    // ───────────────────────────── 수명 ─────────────────────────────

    private void Awake()
    {
        // 중복 제거 — ManagerRoot 가 1차로 막고, 여기서 한 번 더 (DontDestroyOnLoad 는 루트인 ManagerRoot 담당)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (Instance != this) return;   // Destroy 는 프레임 끝이라 중복 오브젝트도 Start 까지 올 수 있음
        BackendManager.Initialize(OnBackendInitialized);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 오프라인 로그인 상태 → 주기적으로 다시 연결
        if (!IsReady || !IsSignedIn || online || IsSigningIn || reconnecting) return;
        if (Time.unscaledTime < nextReconnectTime) return;

        nextReconnectTime = Time.unscaledTime + reconnectInterval;
        reconnecting = true;
        BackendManager.Initialize(ok =>
        {
            if (this == null) return;
            if (!ok) { reconnecting = false; return; }
            TryTokenLogin(startup: false);
        });
    }

    // ───────────────────────────── 시작: 초기화 + 자동 로그인 ─────────────────────────────

    private void OnBackendInitialized(bool ok)
    {
        if (this == null) return;

        LoadRemembered(out string savedUid, out string savedEmail, out bool savedHasMethod, out SignInMethod savedMethod);

        if (string.IsNullOrEmpty(savedUid))
        {
            // 이전에 로그인한 적 없음 (또는 로그아웃함) → 게스트 상태로 준비 완료
            FinishStartup(ok ? "[Auth] 뒤끝 준비 완료 (로그인 안 됨)" : "[Auth] 뒤끝 초기화 실패 — 로그인 없이 시작");
            return;
        }

        // 마지막 계정을 먼저 채워 둠 — 서버에 못 닿아도 이 계정으로 이어서 할 수 있게 (클래스 주석 [자동 로그인 / 오프라인])
        uid       = savedUid;
        email     = savedEmail;
        hasMethod = savedHasMethod;
        method    = savedMethod;
        online    = false;

        if (!ok)
        {
            nextReconnectTime = Time.unscaledTime + reconnectInterval;
            FinishStartup($"[Auth] 뒤끝 초기화 실패 — 마지막 계정({Short(uid)})으로 오프라인 시작");
            return;
        }

        TryTokenLogin(startup: true);
    }

    private void TryTokenLogin(bool startup)
    {
        BackendManager.Call(cb => Backend.BMember.LoginWithTheBackendToken(cb), bro =>
        {
            if (this == null) return;
            reconnecting = false;

            if (bro != null && bro.IsSuccess())
            {
                string serverUid = CurrentBackendUid();
                if (!string.IsNullOrEmpty(uid) && serverUid != uid)
                {
                    // 다른 코드가 이 기기의 뒤끝 로그인 토큰을 바꿨을 때 (예: 테스트 스크립트가 다른 계정으로 로그인).
                    // 저장해 둔 이메일·로그인 방식은 이전 계정 것이므로 버리고, RefreshUserInfo 가 새 계정 값으로 채웁니다.
                    Debug.LogWarning($"[Auth] 저장된 계정({Short(uid)})과 토큰 계정({Short(serverUid)})이 다릅니다 — 토큰 계정을 씁니다.");
                    email     = null;
                    nickname  = null;
                    hasMethod = false;
                }

                uid    = serverUid;
                online = true;
                Remember();
                RefreshUserInfo();
                if (startup) FinishStartup($"[Auth] 자동 로그인 완료. UID: {Short(uid)}");
                else { GameLog.Info("[Auth] 서버 재연결 — 온라인 로그인 완료"); Notify(); }
                return;
            }

            if (BackendManager.IsNetworkError(bro))
            {
                // 서버에 못 닿음 → 오프라인 로그인 유지, 나중에 다시 시도
                online = false;
                nextReconnectTime = Time.unscaledTime + reconnectInterval;
                if (startup) FinishStartup($"[Auth] 서버에 연결할 수 없어 마지막 계정({Short(uid)})으로 오프라인 시작: {BackendManager.Describe(bro)}");
                return;
            }

            // 토큰 거부 (만료·탈퇴·차단·다른 기기에서 탈퇴 등) → 로그아웃 상태
            Debug.LogWarning($"[Auth] 자동 로그인 거부 — 로그아웃합니다: {BackendManager.Describe(bro)}");
            ClearAccount();
            if (startup) FinishStartup(null);
            else Notify();
        });
    }

    private void FinishStartup(string log)
    {
        if (!string.IsNullOrEmpty(log)) GameLog.Info(log);
        if (!IsGoogleSignInSupported)
            GameLog.Info($"[Auth] 이 플랫폼({Application.platform})에서는 구글 로그인 버튼이 비활성화됩니다.");

        IsReady = true;
        Notify();
    }

    // ───────────────────────────── 구글 로그인 ─────────────────────────────

    // 이번 구글 창이 '게스트 → 구글 연동' 인가 (false = 일반 구글 로그인)
    private bool linkingGuest;

    /// <summary>
    /// 구글 로그인 시작. 결과는 StateChanged 이벤트 + IsSignedIn 으로 확인한다.
    /// 게스트로 로그인돼 있으면 '구글 연동' — 같은 계정(같은 Uid·진행)에 구글을 붙입니다 (LinkGuestToGoogle).
    /// </summary>
    public void SignInWithGoogle()
    {
        if (IsGuest) { LinkGuestToGoogle(); return; }
        if (IsSignedIn) return;
        BeginGoogle(link: false);
    }

    /// <summary>
    /// 게스트 계정을 구글 계정으로 바꿉니다 (ChangeCustomToFederation). Uid 는 그대로라 세이브·랭킹·친구가 이어집니다.
    /// 그 구글 계정이 이미 다른 뒤끝 계정에 쓰이고 있으면 실패합니다 — 그때는 로그아웃 후 구글로 로그인하라고 안내.
    /// </summary>
    public void LinkGuestToGoogle()
    {
        if (!IsGuest) return;
        if (!IsOnline)
        {
            LastError = "구글 연동은 인터넷에 연결된 상태에서 할 수 있습니다.";
            Notify();
            return;
        }
        BeginGoogle(link: true);
    }

    private void BeginGoogle(bool link)
    {
        if (!IsReady)
        {
            Debug.LogWarning("[Auth] 아직 로그인 준비 중입니다.");
            return;
        }
        if (IsSigningIn) return;   // 연타 / 중복 로그인 방지
        linkingGuest = link;

        if (!IsGoogleSignInSupported)
        {
            Debug.LogWarning($"[Auth] 이 플랫폼({Application.platform})에서는 구글 로그인을 쓸 수 없습니다. Android 기기에서 테스트하세요.");
            return;
        }
        if (!BackendManager.IsInitialized)
        {
            LastError = "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.";
            BackendManager.Initialize(null);   // 다음 시도를 위해 다시 초기화해 둠
            Notify();
            return;
        }

        if (!TryConfigureGoogleSignIn()) return;

        LastError = null;
        SetSigningIn(true);

        try
        {
            // GoogleSignIn 의 Task 는 백그라운드에서 끝날 수 있음 → BackendManager.Post 로 메인 스레드에서 처리
            GoogleSignIn.DefaultInstance.SignIn()
                .ContinueWith(task => BackendManager.Post(() => OnGoogleSignInFinished(task)));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] SignIn 호출 실패: {e}");
            SetSigningIn(false);
        }
    }

    private bool TryConfigureGoogleSignIn()
    {
        if (isGoogleConfigured) return true;

        if (string.IsNullOrWhiteSpace(webClientId))
        {
            Debug.LogError("[Auth] webClientId 가 비어 있습니다. AuthManager 인스펙터에 '웹 클라이언트 ID' 를 넣어 주세요.");
            return false;
        }

        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId    = webClientId.Trim(),
            RequestIdToken = true,    // 뒤끝에 넘길 ID 토큰
            RequestEmail   = true,
            UseGameSignIn  = false,
        };

        isGoogleConfigured = true;
        return true;
    }

    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (this == null) return;

        if (task.IsCanceled)
        {
            GameLog.Info("[Auth] 사용자가 구글 로그인을 취소했습니다.");
            SetSigningIn(false);
            return;
        }

        if (task.IsFaulted)
        {
            LogGoogleSignInError(task.Exception);
            SetSigningIn(false);
            return;
        }

        string idToken = task.Result.IdToken;
        if (string.IsNullOrEmpty(idToken))
        {
            Debug.LogError("[Auth] ID 토큰이 비어 있습니다. 웹 클라이언트 ID 설정을 확인하세요.");
            SetSigningIn(false);
            return;
        }

        string googleEmail = task.Result.Email;

        if (linkingGuest)
        {
            LinkWithGoogleToken(idToken, googleEmail);
            return;
        }

        BackendManager.Call(cb => Backend.BMember.AuthorizeFederation(idToken, FederationType.Google, cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                string message = ToKoreanMessage(bro, "로그인");
                Debug.LogError($"[Auth] 뒤끝 구글 로그인 실패: {BackendManager.Describe(bro)}");
                Fail(message);
                return;
            }

            // 비밀번호 찾기·관리용으로 구글 이메일을 뒤끝에도 기록 (실패해도 로그인에는 지장 없음)
            BackendManager.Call(cb => Backend.BMember.UpdateFederationEmail(idToken, FederationType.Google, cb), eb =>
            {
                if (eb == null || !eb.IsSuccess())
                    Debug.LogWarning($"[Auth] 구글 이메일 기록 실패 (무시): {BackendManager.Describe(eb)}");
            });

            OnSignedIn(SignInMethod.Google, googleEmail, bro.GetStatusCode() == "201" ? "가입" : "로그인");
        });
    }

    /// <summary>게스트 → 구글 연동 (구글 토큰을 받은 뒤). 성공하면 같은 Uid 로 로그인 방식만 구글이 됩니다.</summary>
    private void LinkWithGoogleToken(string idToken, string googleEmail)
    {
        string guestUid = uid;
        BackendManager.Call(cb => Backend.BMember.ChangeCustomToFederation(idToken, FederationType.Google, cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogError($"[Auth] 게스트 → 구글 연동 실패: {BackendManager.Describe(bro)}");
                Fail(bro != null && bro.GetStatusCode() == "409"
                    ? "이미 다른 계정에 쓰이는 구글 계정입니다. 로그아웃한 뒤 그 구글 계정으로 로그인해 주세요."
                    : ToKoreanMessage(bro, "구글 연동"));
                return;
            }

            BackendManager.Call(cb => Backend.BMember.UpdateFederationEmail(idToken, FederationType.Google, cb), eb =>
            {
                if (eb == null || !eb.IsSuccess())
                    Debug.LogWarning($"[Auth] 구글 이메일 기록 실패 (무시): {BackendManager.Describe(eb)}");
            });

            string nowUid = CurrentBackendUid();
            if (!string.IsNullOrEmpty(nowUid) && nowUid != guestUid)
                Debug.LogWarning($"[Auth] 연동 후 계정 값이 바뀌었습니다 ({Short(guestUid)} → {Short(nowUid)}). 새 값으로 이어 갑니다.");

            OnSignedIn(SignInMethod.Google, googleEmail, "구글 연동");
        });
    }

    // ───────────────────────────── 게스트 로그인 ─────────────────────────────

    /// <summary>
    /// 뒤끝 게스트 계정으로 로그인합니다 (이 기기에 게스트 ID 가 저장돼 있으면 같은 계정). 결과는 StateChanged + IsSignedIn.
    /// Login_Name 이 "로그인 안 한 채 [시작]" 을 누르면 부릅니다. 실패하면(오프라인 등) 기기 게스트로 시작하게 둡니다.
    /// ⚠ 앱을 지우면 게스트 ID 가 사라져 계정을 잃습니다 — 구글 연동을 권하세요.
    /// </summary>
    public void SignInAsGuest()
    {
        if (!IsReady || IsSigningIn || IsSignedIn) return;
        if (!BackendManager.IsInitialized)
        {
            BackendManager.Initialize(null);   // 다음 시도를 위해 다시 초기화해 둠
            Fail("서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.");
            return;
        }

        LastError = null;
        SetSigningIn(true);
        GuestLogin(retryWithNewId: true);
    }

    private void GuestLogin(bool retryWithNewId)
    {
        BackendManager.Call(cb => Backend.BMember.GuestLogin(cb), bro =>
        {
            if (this == null) return;

            if (bro != null && bro.IsSuccess())
            {
                OnSignedIn(SignInMethod.Guest, null, bro.GetStatusCode() == "201" ? "게스트 가입" : "게스트 로그인");
                return;
            }

            // 이 기기에 저장된 게스트 ID 의 계정이 없어짐(탈퇴·콘솔에서 삭제) → 기기 기록을 지우고 새 게스트로 한 번 더
            if (retryWithNewId && bro != null && (bro.GetStatusCode() == "401" || bro.GetStatusCode() == "410"))
            {
                Debug.LogWarning($"[Auth] 저장된 게스트 계정을 쓸 수 없어 새 게스트 계정을 만듭니다: {BackendManager.Describe(bro)}");
                Backend.BMember.DeleteGuestInfo();
                GuestLogin(retryWithNewId: false);
                return;
            }

            Debug.LogWarning($"[Auth] 게스트 로그인 실패: {BackendManager.Describe(bro)}");
            Fail(ToKoreanMessage(bro, "게스트 로그인"));
        });
    }

    /// <summary>구글 로그인 실패는 대부분 '설정' 문제 — DeveloperError(10) → SHA-1 / 웹 클라이언트 ID / 패키지명.</summary>
    private static void LogGoogleSignInError(AggregateException ex)
    {
        if (ex == null)
        {
            Debug.LogError("[Auth] 구글 로그인 실패 (예외 정보 없음)");
            return;
        }

        foreach (Exception e in ex.Flatten().InnerExceptions)
        {
            if (e is GoogleSignIn.SignInException signInError)
            {
                if (signInError.Status == GoogleSignInStatusCode.Canceled)
                    GameLog.Info("[Auth] 사용자가 구글 로그인을 취소했습니다.");
                else
                    Debug.LogError($"[Auth] 구글 로그인 실패: {signInError.Status} / {signInError.Message}");
            }
            else
            {
                Debug.LogError($"[Auth] 구글 로그인 실패: {e}");
            }
        }
    }

    // ───────────────────────────── 이메일 로그인 ─────────────────────────────
    //
    //  뒤끝 '커스텀 계정' 에 이메일을 아이디로 넣습니다. (뒤끝은 아이디가 이메일 형식인지 검사하지 않아서 여기서 검사)
    //  [먼저 해 둘 것]  뒤끝 콘솔에서 커스텀 회원가입이 허용돼 있는지 확인하세요.

    /// <summary>새 계정 만들기. 성공하면 바로 로그인된 상태가 됩니다.</summary>
    public void SignUpWithEmail(string inputEmail, string password)
    {
        if (!TryBeginEmailAuth(inputEmail, password, out string cleanEmail)) return;

        BackendManager.Call(cb => Backend.BMember.CustomSignUp(cleanEmail, password, cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogError($"[Auth] 이메일 회원가입 실패: {BackendManager.Describe(bro)}");
                Fail(ToKoreanMessage(bro, "회원가입"));
                return;
            }

            // 비밀번호 찾기용 이메일 등록 (실패해도 가입·로그인에는 지장 없음)
            BackendManager.Call(cb => Backend.BMember.UpdateCustomEmail(cleanEmail, cb), eb =>
            {
                if (eb == null || !eb.IsSuccess())
                    Debug.LogWarning($"[Auth] 비밀번호 찾기용 이메일 등록 실패 (무시): {BackendManager.Describe(eb)}");
            });

            if (Backend.IsLogin)
            {
                OnSignedIn(SignInMethod.Email, cleanEmail, "회원가입");
                return;
            }

            // 가입만 되고 로그인 세션이 없으면 바로 로그인
            BackendManager.Call(cb => Backend.BMember.CustomLogin(cleanEmail, password, cb), lb =>
            {
                if (this == null) return;
                if (lb != null && lb.IsSuccess()) OnSignedIn(SignInMethod.Email, cleanEmail, "회원가입");
                else Fail("가입은 됐지만 로그인하지 못했습니다. 다시 로그인해 주세요.");
            });
        });
    }

    /// <summary>기존 계정으로 로그인.</summary>
    public void SignInWithEmail(string inputEmail, string password)
    {
        if (!TryBeginEmailAuth(inputEmail, password, out string cleanEmail)) return;

        BackendManager.Call(cb => Backend.BMember.CustomLogin(cleanEmail, password, cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogError($"[Auth] 이메일 로그인 실패: {BackendManager.Describe(bro)}");
                Fail(ToKoreanMessage(bro, "로그인"));
                return;
            }

            OnSignedIn(SignInMethod.Email, cleanEmail, "로그인");
        });
    }

    /// <summary>이메일 로그인/가입 공통 시작 검사. 통과하면 '진행 중' 상태로 바꿉니다.</summary>
    private bool TryBeginEmailAuth(string inputEmail, string password, out string cleanEmail)
    {
        // 앞뒤 공백 제거 + 소문자 — 뒤끝 아이디는 대소문자를 구분하므로 같은 주소가 두 계정이 되지 않게
        cleanEmail = (inputEmail ?? string.Empty).Trim().ToLowerInvariant();

        if (!IsReady)
        {
            Fail("아직 로그인 준비 중입니다. 잠시 후 다시 시도해 주세요.");
            return false;
        }
        if (IsSigningIn || IsSignedIn) return false;

        if (cleanEmail.Length == 0)
        {
            Fail("이메일을 입력해 주세요.");
            return false;
        }
        if (!LooksLikeEmail(cleanEmail))
        {
            Fail("이메일 형식이 올바르지 않습니다.");
            return false;
        }
        if (string.IsNullOrEmpty(password) || password.Length < MIN_PASSWORD_LENGTH)
        {
            Fail($"비밀번호는 {MIN_PASSWORD_LENGTH}자 이상이어야 합니다.");
            return false;
        }
        if (!BackendManager.IsInitialized)
        {
            BackendManager.Initialize(null);   // 다음 시도를 위해 다시 초기화해 둠
            Fail("서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.");
            return false;
        }

        LastError = null;
        SetSigningIn(true);
        return true;
    }

    /// <summary>a@b.c 꼴인가 (공백 없음, @ 하나, @ 뒤에 점)</summary>
    private static bool LooksLikeEmail(string s)
    {
        if (s.IndexOf(' ') >= 0) return false;
        int at = s.IndexOf('@');
        if (at <= 0 || at != s.LastIndexOf('@')) return false;
        int dot = s.LastIndexOf('.');
        return dot > at + 1 && dot < s.Length - 1;
    }

    /// <summary>
    /// 뒤끝 응답을 유저에게 보여 줄 한국어 문구로 바꿉니다.
    /// 커스텀 로그인은 없는 아이디·틀린 비밀번호가 둘 다 401 이라 같은 문구로 안내합니다 (가입 여부를 알려 주지 않게).
    /// </summary>
    private static string ToKoreanMessage(BackendReturnObject bro, string actionName)
    {
        if (bro == null || bro.IsClientRequestFailError()) return "인터넷 연결을 확인해 주세요.";
        if (bro.IsMaintenanceError())                        return "서버 점검 중입니다. 잠시 후 다시 시도해 주세요.";
        if (bro.IsTooManyRequestError() || bro.IsTooManyRequestByLocalError())
                                                             return "요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.";
        if (bro.IsDeviceBlockError())                        return "이 기기는 사용이 제한되었습니다.";
        if (bro.IsServerError())                             return "서버 오류입니다. 잠시 후 다시 시도해 주세요.";

        switch (bro.GetStatusCode())
        {
            case "409": return "이미 가입된 이메일입니다. 로그인해 주세요.";
            case "401": return "이메일 또는 비밀번호가 맞지 않습니다.";
            case "403": return "사용이 중지된 계정입니다.";
            case "410": return "탈퇴 처리 중인 계정입니다.";
            case "400": return "입력 형식이 올바르지 않습니다.";
            default:    return $"{actionName}에 실패했습니다. ({bro.GetStatusCode()} {bro.GetErrorCode()})";
        }
    }

    // ───────────────────────────── 로그인 성공 공통 ─────────────────────────────

    private void OnSignedIn(SignInMethod how, string knownEmail, string actionName)
    {
        uid       = CurrentBackendUid();
        email     = knownEmail;
        method    = how;
        hasMethod = true;
        online    = true;

        if (string.IsNullOrEmpty(uid))
        {
            Debug.LogError($"[Auth] {actionName}은(는) 성공했는데 뒤끝 유저 inDate 가 없습니다.");
            ClearAccount();
            Fail($"{actionName}에 실패했습니다.");
            return;
        }

        Remember();
        GameLog.Info($"[Auth] {actionName} 완료 ({MethodKey(how)}). UID: {Short(uid)}");
        RefreshUserInfo();
        SetSigningIn(false);   // Notify → UI 갱신
    }

    /// <summary>게임이 쓰는 계정 값 — 뒤끝 유저 inDate (없으면 UID).</summary>
    private static string CurrentBackendUid()
    {
        string inDate = Backend.UserInDate;
        return !string.IsNullOrEmpty(inDate) ? inDate : Backend.UID;
    }

    /// <summary>
    /// 서버의 계정 정보(이메일, 가입 방식, 닉네임)로 표시 값을 맞춥니다. 실패해도 로그인 상태는 그대로.
    /// 자동 로그인은 어떤 방식으로 가입했는지 모를 수 있어서 여기서 채웁니다.
    /// </summary>
    private void RefreshUserInfo()
    {
        string forUid = uid;
        BackendManager.Call(cb => Backend.BMember.GetUserInfo(cb), bro =>
        {
            if (this == null || uid != forUid) return;
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Auth] 계정 정보 조회 실패 (무시): {BackendManager.Describe(bro)}");
                return;
            }

            UserInfoRow row = null;
            try { row = JsonUtility.FromJson<UserInfoResponse>(bro.GetReturnValue())?.row; }
            catch (Exception e) { Debug.LogWarning($"[Auth] 계정 정보 읽기 실패 (무시): {e.Message}"); }
            if (row == null) return;

            if (!string.IsNullOrEmpty(row.emailForFindPassword)) email = row.emailForFindPassword;
            nickname = row.nickname;

            // 가입 방식으로 로그인 방식을 맞춤. 게스트도 뒤끝 안에서는 커스텀 계정이라, 게스트로 기억하고 있으면 이메일로 바꾸지 않음
            string type = (row.subscriptionType ?? string.Empty).ToLowerInvariant();
            if (type.Contains("google"))      { method = SignInMethod.Google; hasMethod = true; }
            else if (type.Contains("guest"))  { method = SignInMethod.Guest;  hasMethod = true; }
            else if (type.Contains("custom") && !(hasMethod && method == SignInMethod.Guest))
                                              { method = SignInMethod.Email;  hasMethod = true; }

            Remember();
            Notify();
        });
    }

    // JsonUtility 가 채우는 필드 — "할당되지 않음" 경고(CS0649)를 끔
#pragma warning disable 0649
    [Serializable] private class UserInfoResponse { public UserInfoRow row; }

    [Serializable]
    private class UserInfoRow
    {
        public string nickname;
        public string subscriptionType;       // "customSignUp" / "google" ...
        public string emailForFindPassword;
    }
#pragma warning restore 0649

    /// <summary>서버 닉네임이 바뀌었을 때 DisplayName 을 맞춥니다. (ProfileSync 가 닉네임을 정하거나 받아 온 뒤)</summary>
    public void SetServerNickname(string value)
    {
        if (!IsSignedIn || nickname == value) return;
        nickname = value;
        Notify();
    }

    // ───────────────────────────── 로그아웃 / 탈퇴 ─────────────────────────────

    /// <summary>뒤끝 + 구글 양쪽 로그아웃. 구글 쪽도 해야 다음 로그인 때 계정 선택 창이 다시 뜬다.</summary>
    public void SignOut()
    {
        // 로그인 처리 중에는 무시 — 잠시 뒤 그 결과가 도착하면 "로그아웃했는데 다시 로그인된" 상태가 됨
        if (IsSigningIn)
        {
            Debug.LogWarning("[Auth] 로그인 처리 중이라 로그아웃을 건너뜁니다.");
            return;
        }

        // 기기에 남은 마지막 계정 기록부터 지움 — 서버 로그아웃이 실패(오프라인)해도 다음 실행에 자동 로그인하지 않게
        //   (자동 로그인은 이 기록이 있을 때만 시도함 — OnBackendInitialized)
        ClearAccount();

        if (BackendManager.IsInitialized)
        {
            BackendManager.Call(cb => Backend.BMember.Logout(cb), bro =>
            {
                if (bro == null || !bro.IsSuccess())
                    Debug.LogWarning($"[Auth] 뒤끝 로그아웃 요청 실패 (기기의 로그인 기록은 이미 지움): {BackendManager.Describe(bro)}");
            });
        }

        SignOutGoogle();

        GameLog.Info("[Auth] 로그아웃");
        LastError = null;
        SetSigningIn(false);   // Notify
    }

    /// <summary>
    /// 뒤끝 계정 탈퇴 (계정 삭제). 온라인이어야 합니다. 결과는 메인 스레드에서 onDone(성공, 실패 문구).
    /// 성공하면 로그아웃 상태가 됩니다. ServerAccountData 가 부릅니다.
    /// </summary>
    public void DeleteAccount(Action<bool, string> onDone)
    {
        if (!IsOnline)
        {
            onDone?.Invoke(false, "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.");
            return;
        }

        BackendManager.Call(cb => Backend.BMember.WithdrawAccount(cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Auth] 뒤끝 탈퇴 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(false, BackendManager.IsNetworkError(bro)
                    ? "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요."
                    : "계정을 삭제하지 못했습니다. 잠시 후 다시 시도해 주세요.");
                return;
            }

            GameLog.Info($"[Auth] 뒤끝 탈퇴 완료: {Short(uid)}");
            // 게스트였으면 기기에 저장된 게스트 ID 도 지움 — 남기면 다음 '게스트로 시작' 이 지운 계정에 로그인하려 함
            if (IsGuest) Backend.BMember.DeleteGuestInfo();
            ClearAccount();
            SignOutGoogle();
            Notify();
            onDone?.Invoke(true, null);
        });
    }

    private void SignOutGoogle()
    {
        // 자동 로그인으로 들어와 이번 실행에 구글 설정을 안 했어도 로그아웃되게 필요하면 설정부터
        if (!IsGoogleSignInSupported || !TryConfigureGoogleSignIn()) return;
        try
        {
            GoogleSignIn.DefaultInstance.SignOut();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Auth] 구글 로그아웃 중 예외 (뒤끝 로그아웃은 진행됨): {e.Message}");
        }
    }

    [ContextMenu("테스트: 로그아웃")]
    private void DebugSignOut() => SignOut();

    // ───────────────────────────── 마지막 계정 기록 ─────────────────────────────

    private void Remember()
    {
        PlayerPrefs.SetString(PREF_UID,    uid ?? string.Empty);
        PlayerPrefs.SetString(PREF_EMAIL,  email ?? string.Empty);
        PlayerPrefs.SetString(PREF_METHOD, !hasMethod ? string.Empty : MethodKey(method));
        PlayerPrefs.Save();
    }

    private static string MethodKey(SignInMethod m)
        => m == SignInMethod.Google ? "google" : m == SignInMethod.Guest ? "guest" : "email";

    private static void LoadRemembered(out string savedUid, out string savedEmail, out bool savedHasMethod, out SignInMethod savedMethod)
    {
        savedUid   = PlayerPrefs.GetString(PREF_UID, string.Empty);
        savedEmail = PlayerPrefs.GetString(PREF_EMAIL, string.Empty);
        string m   = PlayerPrefs.GetString(PREF_METHOD, string.Empty);
        savedHasMethod = m == "google" || m == "email" || m == "guest";
        savedMethod    = m == "google" ? SignInMethod.Google : m == "guest" ? SignInMethod.Guest : SignInMethod.Email;
    }

    private void ClearAccount()
    {
        uid       = null;
        email     = null;
        nickname  = null;
        hasMethod = false;
        online    = false;
        PlayerPrefs.DeleteKey(PREF_UID);
        PlayerPrefs.DeleteKey(PREF_EMAIL);
        PlayerPrefs.DeleteKey(PREF_METHOD);
        PlayerPrefs.Save();
    }

    // ───────────────────────────── 내부 도우미 ─────────────────────────────

    private void Fail(string message)
    {
        LastError = message;
        SetSigningIn(false);
    }

    private void SetSigningIn(bool value)
    {
        IsSigningIn = value;
        Notify();
    }

    private static void Notify() => StateChanged?.Invoke();

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "-" : id.Substring(0, Math.Min(10, id.Length)) + "…";
}

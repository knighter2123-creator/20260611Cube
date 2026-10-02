using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Google;                // GoogleSignIn (google-signin-unity 플러그인)
using UnityEngine;

/// <summary>
/// 로그인(인증) 매니저 — LoginScene 의 ManagerRoot 하위에 둔다.
///
/// [하는 일]  Firebase 초기화, 구글 로그인 → Firebase 로그인, 로그아웃, 로그인 상태 보관.
/// [안 하는 일] UI. 버튼/텍스트 참조를 하나도 들고 있지 않다.
///   → 이 매니저는 DontDestroyOnLoad 로 씬을 넘어 살아남지만, 버튼은 씬과 함께 파괴된다.
///     매니저가 씬 UI 를 붙잡고 있으면 씬이 바뀐 뒤 '죽은 참조'가 된다 (learnings 의 데이터/UI 분리 원칙).
///     UI 는 GoogleLoginButtonUI 가 맡고, 이 매니저의 상태를 읽어 갈 뿐이다.
///
/// [2단계 로그인]
///   1) GoogleSignIn  : 구글 계정 주인임을 증명하는 ID 토큰을 받는다 (기기 네이티브 창)
///   2) Firebase Auth : 그 토큰으로 Firebase 에 로그인 → 게임이 쓸 식별자는 Firebase UID
///
/// [세이브] CaptureTo/ApplyFrom 이 없다 → save.json 스키마·저장 흐름과 무관.
///          로그인 상태는 Firebase 가 기기에 따로 보관한다 (save.json / PlayerPrefs 와 별개).
///
/// [이메일 로그인]  SignUpWithEmail / SignInWithEmail — Firebase 의 이메일·비밀번호 로그인.
///   네이티브 플러그인이 필요 없어서 **에디터(Device Simulator)에서도 동작**합니다.
///   실패 사유(한국어)는 LastError 에 남고, UI 가 StateChanged 를 받아 읽어서 표시합니다.
///
/// [로그아웃]  Firebase + 구글 계정 선택 상태를 지웁니다. 앱 종료·씬 이동·세이브 삭제는 하지 않습니다.
///
/// [로그인 방식 조회]  IsSignedInWith(SignInMethod) — 로그인 화면의 각 버튼이 "내 방식으로 로그인됐는가" 를 판단.
/// </summary>
public class AuthManager : MonoBehaviour
{
    public static AuthManager Instance { get; private set; }

    /// <summary>
    /// 로그인 상태가 바뀔 때마다 발행 (준비 완료 / 로그인 시작 / 성공 / 실패·취소 / 로그아웃).
    /// 구독하는 쪽은 인자 없이 받고, 필요한 값은 Instance 의 프로퍼티로 직접 읽는다.
    ///
    /// ★ static 인 이유
    ///   같은 씬 안에서 Awake/OnEnable 이 불리는 순서는 정해져 있지 않다.
    ///   UI 의 OnEnable 이 이 매니저의 Awake 보다 먼저 불리면 Instance 가 아직 null 이라
    ///   인스턴스 이벤트에는 구독할 수 없다. static 이벤트는 매니저가 태어나기 전에도 구독할 수 있다.
    ///   계정 삭제로 매니저가 새로 만들어져도 구독이 끊기지 않는 장점도 있다.
    /// ⚠ 구독한 쪽은 반드시 OnDisable 에서 해제할 것 (static 은 파괴된 구독자를 계속 붙잡는다).
    /// </summary>
    public static event Action StateChanged;

    [Header("Google Sign-In")]
    [Tooltip("Firebase 콘솔 > Authentication > 로그인 방법 > Google 의 '웹 클라이언트 ID'.\n" +
             "google-services.json 의 oauth_client 중 client_type 이 3 인 client_id.\n" +
             "Android 클라이언트 ID(client_type 1)를 넣으면 DEVELOPER_ERROR 가 난다.")]
    [SerializeField] private string webClientId = "";

    // ─────────────── 외부에서 읽는 상태 ───────────────
    public bool IsReady     { get; private set; }   // Firebase 초기화 완료
    public bool IsSigningIn { get; private set; }   // 로그인 창이 떠 있는 중
    public bool IsSignedIn  => auth != null && auth.CurrentUser != null;

    /// <summary>
    /// 구글 로그인을 켜 둘 플랫폼. 지금은 Android 만.
    ///   - 에디터(Device Simulator 포함): 네이티브 플러그인이 없어 동작하지 않는다.
    ///   - iOS: GoogleService-Info.plist 와 URL 스킴(REVERSED_CLIENT_ID) 설정이 없으면
    ///          네이티브 SDK 가 예외를 던져 앱이 종료될 수 있다. iOS 설정을 끝낸 뒤 여기에 추가할 것.
    /// 원래 코드의 Application.isEditor 검사를 이 한 곳으로 모았다 (조건이 여러 곳에 흩어지면 한쪽만 고치게 된다).
    /// </summary>
    public static bool IsGoogleSignInSupported => Application.platform == RuntimePlatform.Android;
    public string Uid         => IsSignedIn ? auth.CurrentUser.UserId : null;
    public string DisplayName => IsSignedIn ? auth.CurrentUser.DisplayName : null;

    /// <summary>로그인한 계정의 이메일 (구글/이메일 공통). 로그아웃 버튼 옆 "어느 계정인지" 표시용.</summary>
    public string Email => IsSignedIn ? auth.CurrentUser.Email : null;

    /// <summary>로그인 방식. 로그인 화면의 버튼(구글 / 이메일)과 1:1 로 대응합니다.</summary>
    public enum SignInMethod { Google, Email }

    // FirebaseUser.ProviderData 의 ProviderId — 연결된 로그인 방식마다 하나씩 들어 있습니다.
    private const string PROVIDER_GOOGLE = "google.com";
    private const string PROVIDER_EMAIL  = "password";   // 이메일·비밀번호

    /// <summary>
    /// 지금 이 방식으로 로그인돼 있는가.
    /// 로그인 화면의 각 버튼이 "내 방식으로 로그인돼 있으면 [시작하기]" 를 판단할 때 씁니다.
    /// 계정 연동으로 두 방식이 모두 연결돼 있으면 둘 다 true 입니다.
    /// </summary>
    public bool IsSignedInWith(SignInMethod method)
    {
        if (!IsSignedIn) return false;

        string providerId = method == SignInMethod.Google ? PROVIDER_GOOGLE : PROVIDER_EMAIL;
        foreach (IUserInfo info in auth.CurrentUser.ProviderData)
            if (info.ProviderId == providerId) return true;

        return false;
    }

    /// <summary>
    /// 어떤 방식으로 로그인했는가 — "Google" / "이메일" / "" (로그인 안 됨 / 알 수 없음). 로그아웃 버튼 옆 표시용.
    /// (계정 연동으로 둘 다 연결돼 있으면 구글을 먼저 표시합니다)
    /// </summary>
    public string SignInProviderLabel
    {
        get
        {
            if (IsSignedInWith(SignInMethod.Google)) return "Google";
            if (IsSignedInWith(SignInMethod.Email))  return "이메일";
            return string.Empty;
        }
    }

    // 마지막 실패 사유 (UI 표시용, 한국어). 새 시도를 시작하면 비워집니다.
    //   이벤트 인자로 넘기지 않고 프로퍼티로 두는 이유: StateChanged 는 "상태가 바뀌었다" 만 알리고
    //   값은 구독자가 Instance 에서 직접 읽는 규칙이라 (UI 가 늦게 켜져도 같은 값을 읽을 수 있음).
    public string LastError { get; private set; }

    /// <summary>Firebase 비밀번호 최소 길이 (Firebase 규칙: 6자). UI 안내 문구도 이 값을 씁니다.</summary>
    public const int MIN_PASSWORD_LENGTH = 6;

    private FirebaseAuth auth;

    // GoogleSignIn.Configuration 은 DefaultInstance 가 만들어진 뒤 다시 대입하면 예외를 던진다.
    // DefaultInstance 는 static 이라 이 매니저가 파괴·재생성돼도(계정 삭제) 살아 있으므로,
    // "이미 설정했는지" 도 static 으로 기억한다.
    // ResetStatics 에서 일부러 되돌리지 않는다 — GoogleSignIn 쪽 static 과 항상 짝이 맞아야 하기 때문.
    private static bool isGoogleConfigured;

    /// <summary>
    /// 에디터에서 Enter Play Mode Options(도메인 리로드 끔)를 쓰면 static 이 이전 플레이 값을 들고 온다.
    /// 플레이 시작 때마다 비워 준다 (SaveManager 의 ResetStatics 와 같은 패턴).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        StateChanged = null;
    }

    // ───────────────────────────── 수명 ─────────────────────────────

    private void Awake()
    {
        // 중복 제거 (2중 안전장치)
        //   로그인 화면으로 돌아오면 LoginScene 의 ManagerRoot 가 또 로드된다.
        //   1차: ManagerRoot.Awake(실행 순서 -200)가 중복 루트를 SetActive(false) 로 즉시 끈다
        //        → 비활성 오브젝트의 자식은 Awake 가 불리지 않으므로, 보통은 여기까지 오지도 않는다.
        //   2차: 그래도 이 Awake 가 불리는 경우(실행 순서 설정이 빠졌을 때 등)를 위해
        //        다른 자식 매니저들과 같은 패턴으로 한 번 더 막는다.
        // (DontDestroyOnLoad 는 여기서 부르지 않는다 — 루트인 ManagerRoot 가 담당.
        //  자식 오브젝트에 DontDestroyOnLoad 를 부르면 무시되고 경고만 뜬다.)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 2차 안전장치의 짝: Awake 의 Destroy() 는 프레임 끝에 실행되므로
        // 지우기로 한 중복 오브젝트도 Start 까지는 불릴 수 있다. 진짜 Instance 만 초기화한다.
        if (Instance != this) return;

        InitFirebase();
    }

    private void OnDestroy()
    {
        // 계정 삭제(AccountReset)는 ManagerRoot 째로 파괴한 뒤 한 프레임 쉬고 LoginScene 을 다시 올린다.
        // 여기서 Instance 를 비워야 새 AuthManager 가 자신을 '중복' 으로 오해하지 않는다.
        if (Instance == this) Instance = null;
    }

    // ───────────────────────────── Firebase 초기화 ─────────────────────────────

    private void InitFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            // 비동기 콜백은 나중에 도착한다. 그 사이 계정 삭제 등으로 이 매니저가 파괴됐을 수 있다.
            // 파괴된 MonoBehaviour 는 Unity 에서 this == null 이 true.
            if (this == null) return;

            // 실패한 작업의 task.Result 를 읽으면 그 자리에서 예외가 나므로 실패 여부를 먼저 본다.
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogError($"[Auth] Firebase 의존성 확인 실패: {task.Exception}");
                return;
            }

            if (task.Result != DependencyStatus.Available)
            {
                Debug.LogError($"[Auth] Firebase 를 사용할 수 없음: {task.Result}");
                return;
            }

            auth = FirebaseAuth.DefaultInstance;
            IsReady = true;

            // 자동 로그인: 전에 로그인했다면 앱을 다시 켜도 CurrentUser 가 채워져 있다.
            if (IsSignedIn)
                Debug.Log($"[Auth] 이전 로그인 복원. UID: {Uid}");
            else
                Debug.Log("[Auth] Firebase 준비 완료 (로그인 안 됨)");

            if (!IsGoogleSignInSupported)
                Debug.Log($"[Auth] 이 플랫폼({Application.platform})에서는 구글 로그인 버튼이 비활성화됩니다.");

            Notify();
        });
    }

    // ───────────────────────────── 구글 로그인 ─────────────────────────────

    /// <summary>
    /// 구글 로그인 시작. 결과는 StateChanged 이벤트 + IsSignedIn 으로 확인한다.
    /// </summary>
    public void SignInWithGoogle()
    {
        if (!IsReady)
        {
            Debug.LogWarning("[Auth] 아직 Firebase 초기화 전입니다.");
            return;
        }
        if (IsSigningIn || IsSignedIn) return;   // 연타 / 중복 로그인 방지

        // 지원 플랫폼이 아니면 중단 (에디터 / 설정 전 iOS — IsGoogleSignInSupported 주석 참고)
        if (!IsGoogleSignInSupported)
        {
            Debug.LogWarning($"[Auth] 이 플랫폼({Application.platform})에서는 구글 로그인을 쓸 수 없습니다. Android 기기에서 테스트하세요.");
            return;
        }

        if (!TryConfigureGoogleSignIn()) return;

        LastError = null;   // 이전 이메일 로그인 실패 문구가 구글 시도 중에 남아 보이지 않게
        SetSigningIn(true);

        try
        {
            // ContinueWith 가 아니라 ContinueWithOnMainThread:
            // 이어지는 코드에서 Unity API 를 써도 안전하도록 메인 스레드로 돌아온다.
            GoogleSignIn.DefaultInstance.SignIn()
                .ContinueWithOnMainThread(task => OnGoogleSignInFinished(task));
        }
        catch (Exception e)
        {
            // 이 catch 는 SignIn() 을 '호출하는 순간' 의 예외만 잡는다.
            // 로그인 도중 실패는 task.IsFaulted 로 온다.
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
            RequestIdToken = true,    // Firebase 에 넘길 ID 토큰 (false 면 2단계 불가)
            RequestEmail   = true,
            UseGameSignIn  = false,   // 일반 구글 로그인 모드
        };

        isGoogleConfigured = true;
        return true;
    }

    private void OnGoogleSignInFinished(Task<GoogleSignInUser> task)
    {
        if (this == null) return;

        if (task.IsCanceled)
        {
            Debug.Log("[Auth] 사용자가 구글 로그인을 취소했습니다.");
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

        SignInToFirebase(idToken);
    }

    /// <summary>
    /// 구글 로그인 실패는 대부분 코드가 아니라 '설정' 문제라 Status 값이 디버깅의 출발점이다.
    ///   DeveloperError(10) → SHA-1 지문 미등록 / 웹 클라이언트 ID 오류 / 패키지명 불일치
    ///   Canceled           → 사용자가 창을 닫음 (플러그인 버전에 따라 IsFaulted 로 오기도 함)
    /// </summary>
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
                    Debug.Log("[Auth] 사용자가 구글 로그인을 취소했습니다.");
                else
                    Debug.LogError($"[Auth] 구글 로그인 실패: {signInError.Status} / {signInError.Message}");
            }
            else
            {
                Debug.LogError($"[Auth] 구글 로그인 실패: {e}");
            }
        }
    }

    private void SignInToFirebase(string idToken)
    {
        // 두 번째 인자(accessToken)는 Firebase 로그인에 필요 없어서 null
        Credential credential = GoogleAuthProvider.GetCredential(idToken, null);

        auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(signInTask =>
        {
            if (this == null) return;

            if (signInTask.IsCanceled)
            {
                Debug.LogWarning("[Auth] Firebase 로그인이 취소되었습니다.");
                SetSigningIn(false);
                return;
            }

            if (signInTask.IsFaulted)
            {
                Debug.LogError($"[Auth] Firebase 로그인 실패: {signInTask.Exception}");
                SetSigningIn(false);
                return;
            }

            // 반환 타입(FirebaseUser / AuthResult)이 SDK 버전마다 달라서 결과 대신 CurrentUser 를 읽는다.
            if (IsSignedIn)
                Debug.Log($"[Auth] 로그인 완료. UID: {Uid}, 이름: {DisplayName}");
            else
                Debug.LogError("[Auth] 로그인은 끝났는데 CurrentUser 가 없습니다.");

            SetSigningIn(false);   // 내부에서 Notify → UI 갱신
        });
    }

    // ───────────────────────────── 이메일 로그인 ─────────────────────────────
    //
    //  [구글 로그인과 무엇이 다른가]
    //    구글 : 구글 ID 토큰(구글이 발급) → Firebase 에 넘겨 로그인
    //    이메일: 이메일·비밀번호를 Firebase 에 직접 보내 로그인 — 중간에 "구글 토큰" 이 없습니다.
    //    둘 다 결과는 똑같이 Firebase 사용자(UID) 이고, 그 뒤의 게임 흐름(StateChanged, IsSignedIn, Uid)도 같습니다.
    //    서버 검증 등에 쓸 "토큰" 이 필요하면 구글 토큰이 아니라 Firebase ID 토큰(auth.CurrentUser.TokenAsync)을 씁니다.
    //
    //  [먼저 해 둘 것]
    //    Firebase 콘솔 > Authentication > 로그인 방법(Sign-in method) > '이메일/비밀번호' 사용 설정.
    //    안 켜면 OperationNotAllowed 오류가 납니다.
    //
    //  [같은 이메일로 구글 로그인도 하면?]
    //    Firebase 기본 설정(이메일당 계정 1개)에서는, 같은 주소로 구글 로그인을 하면 구글 쪽이 우선되어
    //    이메일·비밀번호 방식이 그 계정에서 빠질 수 있습니다. 테스트용으로는 서로 다른 주소를 쓰세요.

    /// <summary>새 계정 만들기. 성공하면 바로 로그인된 상태가 됩니다.</summary>
    public void SignUpWithEmail(string email, string password)
    {
        if (!TryBeginEmailAuth(email, password, out string cleanEmail)) return;

        // try/catch — Firebase 호출이 '부르는 순간' 예외를 던지면(드묾) 결과 콜백이 영영 오지 않아
        //   IsSigningIn 이 true 로 굳고 모든 로그인 버튼이 잠긴 채 남습니다. 구글 로그인 쪽 SignIn() 과 같은 방어입니다.
        try
        {
            auth.CreateUserWithEmailAndPasswordAsync(cleanEmail, password)
                .ContinueWithOnMainThread(task => OnEmailAuthFinished(task, "회원가입"));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] 이메일 회원가입 호출 실패: {e}");
            Fail("회원가입에 실패했습니다.");
        }
    }

    /// <summary>기존 계정으로 로그인.</summary>
    public void SignInWithEmail(string email, string password)
    {
        if (!TryBeginEmailAuth(email, password, out string cleanEmail)) return;

        try   // 위 SignUpWithEmail 의 try/catch 와 같은 이유
        {
            auth.SignInWithEmailAndPasswordAsync(cleanEmail, password)
                .ContinueWithOnMainThread(task => OnEmailAuthFinished(task, "로그인"));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Auth] 이메일 로그인 호출 실패: {e}");
            Fail("로그인에 실패했습니다.");
        }
    }

    /// <summary>
    /// 이메일 로그인/가입 공통 시작 검사. 통과하면 '진행 중' 상태로 바꿉니다.
    /// 서버에 보내기 전에 걸러낼 수 있는 것(빈 칸, 비밀번호 길이)은 여기서 막습니다 — 네트워크 왕복 없이 바로 안내.
    /// </summary>
    private bool TryBeginEmailAuth(string email, string password, out string cleanEmail)
    {
        cleanEmail = (email ?? string.Empty).Trim();   // 앞뒤 공백은 흔한 입력 실수라 지워 줌 (비밀번호는 공백도 글자라 그대로)

        if (!IsReady)
        {
            Fail("아직 로그인 준비 중입니다. 잠시 후 다시 시도해 주세요.");
            return false;
        }
        if (IsSigningIn || IsSignedIn) return false;   // 연타 / 이미 로그인됨 (구글 로그인과 같은 규칙)

        if (cleanEmail.Length == 0)
        {
            Fail("이메일을 입력해 주세요.");
            return false;
        }
        if (string.IsNullOrEmpty(password) || password.Length < MIN_PASSWORD_LENGTH)
        {
            Fail($"비밀번호는 {MIN_PASSWORD_LENGTH}자 이상이어야 합니다.");
            return false;
        }

        LastError = null;        // 새 시도 → 이전 오류 문구 지움
        SetSigningIn(true);      // 내부에서 Notify → UI 버튼 비활성
        return true;
    }

    /// <summary>
    /// 이메일 로그인/가입 결과 처리.
    /// task 의 타입을 적지 않고(Task) 받는 이유: Firebase SDK 버전에 따라 결과가 FirebaseUser 또는 AuthResult 로 달라서,
    /// 결과값 대신 auth.CurrentUser 를 읽으면 버전을 올려도 이 코드는 그대로 동작합니다 (구글 로그인 쪽과 같은 방식).
    /// </summary>
    private void OnEmailAuthFinished(Task task, string actionName)
    {
        if (this == null) return;   // 기다리는 사이 매니저가 파괴됐을 수 있음 (계정 삭제 등)

        if (task.IsCanceled)
        {
            Fail($"{actionName}이(가) 취소되었습니다.");
            return;
        }

        if (task.IsFaulted)
        {
            string message = ToKoreanMessage(task.Exception);
            Debug.LogError($"[Auth] 이메일 {actionName} 실패: {message}\n{task.Exception}");
            Fail(message);
            return;
        }

        if (IsSignedIn)
            Debug.Log($"[Auth] 이메일 {actionName} 완료. UID: {Uid}");
        else
            Debug.LogError($"[Auth] 이메일 {actionName}은(는) 끝났는데 CurrentUser 가 없습니다.");

        SetSigningIn(false);   // Notify → UI 갱신 (GoogleLoginButtonUI 등도 '로그인됨' 으로 바뀜)
    }

    /// <summary>실패 사유를 남기고 '진행 중' 을 풉니다. (Notify 는 SetSigningIn 이 함)</summary>
    private void Fail(string message)
    {
        LastError = message;
        SetSigningIn(false);
    }

    /// <summary>
    /// Firebase 오류 코드를 유저에게 보여 줄 한국어 문구로 바꿉니다.
    /// FirebaseException.ErrorCode 는 숫자라, AuthError 열거형으로 바꾸면 어떤 오류인지 이름으로 구분할 수 있습니다.
    ///
    /// ⚠ 최근 Firebase 프로젝트는 '이메일 열거 보호' 가 기본으로 켜져 있어서, 없는 계정·틀린 비밀번호를
    ///   구분하지 않고 InvalidCredential 하나로 돌려줍니다 (남이 "이 이메일이 가입돼 있나" 알아내지 못하게).
    ///   그래서 두 경우를 같은 문구로 안내합니다.
    /// </summary>
    private static string ToKoreanMessage(AggregateException ex)
    {
        if (ex != null)
        {
            foreach (Exception e in ex.Flatten().InnerExceptions)
            {
                if (!(e is FirebaseException fe)) continue;

                switch ((AuthError)fe.ErrorCode)
                {
                    case AuthError.InvalidEmail:         return "이메일 형식이 올바르지 않습니다.";
                    case AuthError.EmailAlreadyInUse:    return "이미 가입된 이메일입니다. 로그인해 주세요.";
                    case AuthError.WeakPassword:         return $"비밀번호가 너무 약합니다. ({MIN_PASSWORD_LENGTH}자 이상)";
                    case AuthError.WrongPassword:
                    case AuthError.UserNotFound:
                    case AuthError.InvalidCredential:    return "이메일 또는 비밀번호가 맞지 않습니다.";
                    case AuthError.UserDisabled:         return "사용이 중지된 계정입니다.";
                    case AuthError.NetworkRequestFailed: return "인터넷 연결을 확인해 주세요.";
                    case AuthError.OperationNotAllowed:  return "이메일 로그인이 꺼져 있습니다. (개발자: Firebase 콘솔에서 이메일/비밀번호 사용 설정)";
                    default:                             return $"로그인에 실패했습니다. ({(AuthError)fe.ErrorCode})";
                }
            }
        }
        return "로그인에 실패했습니다.";
    }

    // ───────────────────────────── 로그아웃 ─────────────────────────────

    /// <summary>
    /// Firebase + 구글 양쪽 로그아웃. 구글 쪽도 해야 다음 로그인 때 계정 선택 창이 다시 뜬다.
    /// (계정 전환 / 계정 삭제 흐름에서 호출할 용도)
    /// </summary>
    public void SignOut()
    {
        // 로그인 처리 중에는 무시.
        //   구글 창이나 이메일 요청이 진행 중일 때 로그아웃하면, 잠시 뒤 그 결과가 도착하면서
        //   "로그아웃했는데 다시 로그인돼 있는" 상태가 됩니다. 버튼도 막아 두지만 여기서 한 번 더 막습니다.
        if (IsSigningIn)
        {
            Debug.LogWarning("[Auth] 로그인 처리 중이라 로그아웃을 건너뜁니다.");
            return;
        }

        // 어떤 방식(구글/이메일)으로 로그인했든 Firebase 로그아웃은 같습니다 — 기기에 저장된 로그인 상태가 지워짐
        if (auth != null) auth.SignOut();

        // 구글 쪽 로그아웃 — 이걸 해야 다음 구글 로그인 때 "계정 선택 창" 이 다시 뜹니다.
        //   예전에는 isGoogleConfigured 일 때만 했는데, 앱을 다시 켜서 '자동 로그인' 된 상태에서는
        //   이번 실행에 구글 설정을 한 번도 안 했으므로(false) 구글 로그아웃이 건너뛰어졌습니다.
        //   → 그러면 다음 구글 로그인이 계정 선택 없이 예전 계정으로 바로 들어가 계정을 바꿀 수 없었습니다.
        //   그래서 필요하면 여기서 설정을 먼저 넣고 로그아웃합니다 (이메일로 로그인했던 경우에도 무해).
        if (IsGoogleSignInSupported && TryConfigureGoogleSignIn())
        {
            try
            {
                GoogleSignIn.DefaultInstance.SignOut();
            }
            catch (Exception e)
            {
                // 네이티브 플러그인 쪽 예외. Firebase 로그아웃은 이미 끝났으므로 게임 진행에는 지장 없음 → 로그만
                Debug.LogWarning($"[Auth] 구글 로그아웃 중 예외 (Firebase 로그아웃은 완료): {e.Message}");
            }
        }

        Debug.Log("[Auth] 로그아웃");
        LastError = null;   // 로그아웃하면 이전 실패 문구도 지움
        SetSigningIn(false);   // Notify → 로그인 화면 UI 들이 '로그인 전' 상태로 돌아감
    }

    // 에디터 테스트용: 컴포넌트 우클릭 → 로그아웃 (Firebase 는 로그인 상태를 기억하므로
    //   같은 계정으로 다시 테스트하거나 다른 계정으로 바꿀 때 필요합니다)
    [ContextMenu("테스트: 로그아웃")]
    private void DebugSignOut() => SignOut();

    // ───────────────────────────── 내부 도우미 ─────────────────────────────

    private void SetSigningIn(bool value)
    {
        IsSigningIn = value;
        Notify();
    }

    private static void Notify()
    {
        // C# 이벤트(일반 델리게이트)라 ?.Invoke 가 안전하다 (Unity 오브젝트가 아님)
        StateChanged?.Invoke();
    }
}
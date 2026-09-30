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
    /// ★ [배포 전 검토] 구글 로그인을 켜 둘 플랫폼. 지금은 Android 만.
    ///   - 에디터(Device Simulator 포함): 네이티브 플러그인이 없어 동작하지 않는다.
    ///   - iOS: GoogleService-Info.plist 와 URL 스킴(REVERSED_CLIENT_ID) 설정이 없으면
    ///          네이티브 SDK 가 예외를 던져 앱이 종료될 수 있다. iOS 설정을 끝낸 뒤 여기에 추가할 것.
    /// 원래 코드의 Application.isEditor 검사를 이 한 곳으로 모았다 (조건이 여러 곳에 흩어지면 한쪽만 고치게 된다).
    /// </summary>
    public static bool IsGoogleSignInSupported => Application.platform == RuntimePlatform.Android;
    public string Uid         => IsSignedIn ? auth.CurrentUser.UserId : null;
    public string DisplayName => IsSignedIn ? auth.CurrentUser.DisplayName : null;

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

    // ───────────────────────────── 로그아웃 ─────────────────────────────

    /// <summary>
    /// Firebase + 구글 양쪽 로그아웃. 구글 쪽도 해야 다음 로그인 때 계정 선택 창이 다시 뜬다.
    /// (계정 전환 / 계정 삭제 흐름에서 호출할 용도)
    /// </summary>
    public void SignOut()
    {
        if (auth != null) auth.SignOut();

        // DefaultInstance 는 Configuration 이 들어간 뒤에만 만들 수 있고, 미지원 플랫폼에는 플러그인이 없다
        if (isGoogleConfigured && IsGoogleSignInSupported)
            GoogleSignIn.DefaultInstance.SignOut();

        Debug.Log("[Auth] 로그아웃");
        SetSigningIn(false);
    }

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
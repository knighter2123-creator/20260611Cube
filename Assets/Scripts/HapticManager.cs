using UnityEngine;

/// <summary>
/// 안드로이드 진동(햅틱) 매니저.
///
/// [붙이는 위치]
///   로그인 씬의 ManagerRoot 하위. 다른 매니저들과 같은 DontDestroyOnLoad 싱글턴이다.
///
/// [★ 이 코드는 minSdkVersion 30(Android 11) 이상을 전제로 한다]
///   그래서 API 26(VibrationEffect) 분기를 두지 않는다. 설치되는 모든 기기에 이미 있다.
///   VibrationAttributes(API 30)도 마찬가지로 항상 쓸 수 있다.
///   ※ 나중에 최소 버전을 30 미만으로 낮추면 이 파일을 반드시 다시 봐야 한다.
///     (createOneShot / createWaveform / VibrationAttributes 가 전부 없는 기기가 생긴다)
///
/// [왜 Handheld.Vibrate()를 안 쓰는가]
///   유니티 기본 Handheld.Vibrate()는 안드로이드에서 무조건 약 500ms를 통째로 울린다.
///   레벨업처럼 톡 치고 지나가야 하는 연출에 쓰면 "윙—" 하고 손이 얼얼해진다.
///
/// [★ 가장 중요한 함정 — 권한]
///   AndroidManifest.xml 에 아래 줄이 없으면 진동이 "조용히" 실패한다.
///   에러도 안 나고 로그도 안 찍히고 그냥 아무 일도 안 일어난다.
///
///     &lt;uses-permission android:name="android.permission.VIBRATE" /&gt;
///
///   유니티는 코드에 Handheld.Vibrate()가 있으면 이 권한을 자동으로 넣어주지만,
///   이 스크립트처럼 JNI로만 호출하면 자동 추가가 안 된다.
///   → Player Settings → Publishing Settings → Custom Main Manifest 체크 후
///     Assets/Plugins/Android/AndroidManifest.xml 에 직접 추가할 것.
/// </summary>
public partial class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }

    private const string PrefKey = "haptic_enabled";

    // ★ 에디터 전용 필드는 #if 로 감쌉니다.
    //   감싸지 않으면 실기기 빌드에서 "할당했지만 한 번도 읽지 않는 필드"가 되어
    //   CS0414 경고가 뜹니다. (이 필드는 #elif UNITY_EDITOR 블록에서만 읽힙니다)
#if UNITY_EDITOR
    [Header("에디터 테스트")]
    [Tooltip("에디터에서도 진동 토글을 만질 수 있게 합니다. 실제 진동 대신 로그만 찍힙니다. " +
             "빌드에는 영향이 없습니다.")]
    [SerializeField] private bool editorTreatAsSupported = true;
#endif

    [Header("유저 설정")]
    [Tooltip("설정 패널 토글로 끌 수 있게. PlayerPrefs에 저장된다.")]
    [SerializeField] private bool userEnabled = true;

    [Header("연출 세기")]
    [Tooltip("레벨업 진동 길이(ms). 40~80 사이가 '톡' 하는 느낌.")]
    [SerializeField] private int levelUpDurationMs = 45;

    [Tooltip("진동 세기 1~255. 진폭 제어를 지원하는 기기에서만 반영된다.")]
    [Range(1, 255)]
    [SerializeField] private int levelUpAmplitude = 160;

    [Header("디버그")]
    [Tooltip("진동이 '건너뛰어진' 이유를 콘솔에 찍습니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logBlockedCalls = true;

    [Header("연타 방지")]
    [Tooltip("방치형은 한 번에 여러 레벨이 오르기 때문에 쿨다운이 반드시 필요하다.")]
    [SerializeField] private float minInterval = 0.25f;

    private float lastVibrateTime = -999f;

    public bool UserEnabled => userEnabled;

    /// <summary>이 기기에서 진동이 실제로 가능한지. 진동 모터가 없는 기기가 있다.</summary>
    public bool IsSupported { get; private set; }

    // ───────────────────────── JNI 캐시 ─────────────────────────
    // JNI 객체 생성은 비용이 있어서 매번 만들면 안 된다. 최초 1회만 만들고 들고 있는다.
#if UNITY_ANDROID && !UNITY_EDITOR
    private const int DEFAULT_AMPLITUDE = -1;   // VibrationEffect.DEFAULT_AMPLITUDE
    private const int NO_REPEAT         = -1;   // createWaveform 의 repeat 인자

    private static AndroidJavaObject vibrator;
    private static AndroidJavaClass  vibrationEffectClass;
    private static AndroidJavaObject touchAttributes;   // VibrationAttributes (API 30+)
    private static int  apiLevel;
    private static bool hasAmplitudeControl;
    private static bool hasPermission = true;

    // VibrationAttributes 경로가 한 번이라도 실패하면 내려서 다시 시도하지 않는다.
    private static bool useAttributes = true;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        userEnabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
        InitVibrator();
    }

    private void OnDestroy()
    {
        // static 이 파괴된 오브젝트를 붙잡지 않게 정리. (다른 매니저들과 같은 패턴)
        if (Instance == this) Instance = null;
    }

    private void InitVibrator()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                apiLevel = version.GetStatic<int>("SDK_INT");

            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                // ★ 이 분기는 최소 API 30 에서도 반드시 필요하다.
                //   VibratorManager 는 API 31(안드로이드 12)에 들어왔으므로
                //   API 30 기기에는 존재하지 않는다.
                //   지우면 안드로이드 11 에서만 진동이 죽는, 찾기 고약한 버그가 된다.
                if (apiLevel >= 31)
                {
                    using (var vibratorManager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                    {
                        if (vibratorManager != null)
                            vibrator = vibratorManager.Call<AndroidJavaObject>("getDefaultVibrator");
                    }
                }

                // API 30 경로 (그리고 위가 실패했을 때의 폴백).
                // API 31+ 에서 deprecated 이지만 여전히 동작한다.
                if (vibrator == null)
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                // ★ 권한이 실제로 부여됐는지 런타임에 확인한다.
                //
                //   매니페스트에 줄을 넣었는지 눈으로 확인하는 것과,
                //   그 줄이 실제로 빌드된 APK 에 들어갔는지는 다른 문제다.
                //   Custom Main Manifest 체크를 안 했거나 <application> 안쪽에 잘못 넣으면
                //   파일에는 있는데 APK 에는 없는 상태가 된다.
                //   checkSelfPermission 은 그 최종 결과를 알려주므로 추측이 사라진다.
                //   (0 = PackageManager.PERMISSION_GRANTED)
                try
                {
                    int granted = activity.Call<int>("checkSelfPermission", "android.permission.VIBRATE");
                    hasPermission = (granted == 0);

                    if (!hasPermission)
                        Debug.LogError("[Haptic] VIBRATE 권한이 없습니다. AndroidManifest.xml 의 <manifest> 바로 아래에 " +
                                       "<uses-permission android:name=\"android.permission.VIBRATE\" /> 를 넣고, " +
                                       "Player Settings → Publishing Settings → Custom Main Manifest 가 체크됐는지 확인하세요.");
                }
                catch (System.Exception e)
                {
                    hasPermission = true;   // 확인 자체가 실패하면 판단을 보류하고 진행한다
                    Debug.LogWarning($"[Haptic] 권한 확인 실패(무시하고 진행): {e.Message}");
                }
            }

            if (vibrator == null)
            {
                Debug.LogWarning("[Haptic] Vibrator 서비스를 가져오지 못했습니다.");
                return;
            }

            IsSupported = vibrator.Call<bool>("hasVibrator");

            // minSdk 30 이므로 VibrationEffect 는 항상 존재한다. 버전 분기 불필요.
            vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");

            // 세기(amplitude) 제어 지원 여부. 저가형은 켜기/끄기만 되는 경우가 많다.
            // 지원 안 하면 amplitude 값이 무시되고 최대 세기로 울린다.
            hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");

            InitVibrationAttributes();

            Debug.Log($"[Haptic] 초기화 완료 - API {apiLevel}, 진동 지원 {IsSupported}, " +
                      $"권한 {hasPermission}, 세기 제어 {hasAmplitudeControl}, " +
                      $"용도 표시 {touchAttributes != null}");
        }
        catch (System.Exception e)
        {
            // JNI는 기기/롬에 따라 예상 못한 예외가 난다. 진동은 없어도 게임은 돌아야 하므로
            // 반드시 try-catch로 감싸고 절대 위로 던지지 않는다.
            IsSupported = false;
            Debug.LogWarning($"[Haptic] 초기화 실패: {e.Message}");
        }
#elif UNITY_EDITOR
        // ★ 에디터에서는 실제 진동이 불가능하다.
        //   IsSupported 를 false 로 두면 BindToggle 이 toggle.interactable = false 로 만들어서
        //   설정 화면의 진동 토글이 회색으로 죽어 아예 눌리지 않는다.
        //   그래서 에디터에서는 UI 상으로만 '지원됨'으로 취급한다.
        //   Vibrate() 는 아래 #else 분기를 타서 로그만 찍으므로 안전하다.
        IsSupported = editorTreatAsSupported;

        // ★ 에디터에서도 초기화 사실을 남긴다.
        //   이 줄이 없으면 "HapticManager 가 씬에 있긴 한가?"를 에디터에서 확인할 방법이 없다.
        //   실기기 분기에는 초기화 로그가 있는데 에디터 분기에만 없어서 생긴 사각지대였다.
        Debug.Log($"[Haptic] (에디터) 초기화 — 실제 진동은 불가, UI 상 지원 {IsSupported}", this);
#else
        // iOS / PC — 진동 없음.
        IsSupported = false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// ★ 신규 — 진동의 "용도"를 시스템에 알린다. (VibrationAttributes, API 30+)
    ///
    /// [왜 필요한가]
    ///   용도를 지정하지 않으면 USAGE_UNKNOWN 으로 분류된다.
    ///   안드로이드 12 이후로는 용도 불명 진동을 시스템이 억제하는 경우가 있어,
    ///   기기와 시스템 설정 조합에 따라 진동이 조용히 무시될 수 있다.
    ///   게임 피드백은 USAGE_TOUCH 로 표시하는 것이 맞다.
    ///
    /// [최소 API 30 이라 마침 맞아떨어진다]
    ///   VibrationAttributes 가 정확히 API 30 에 추가되었다.
    ///   즉 이 프로젝트가 설치되는 모든 기기에 존재하므로 버전 분기가 필요 없다.
    ///
    ///   그래도 try-catch 로 감싸는 이유는 제조사 커스텀 롬에서 예상 못한 동작을 하는
    ///   경우가 실제로 있기 때문이다. 실패하면 touchAttributes 가 null 로 남고
    ///   기본 경로로 조용히 되돌아간다 — 진동이 아예 안 되는 것보다 낫다.
    /// </summary>
    private void InitVibrationAttributes()
    {
        try
        {
            using (var attrClass = new AndroidJavaClass("android.os.VibrationAttributes"))
            {
                int usageTouch = attrClass.GetStatic<int>("USAGE_TOUCH");
                touchAttributes = attrClass.CallStatic<AndroidJavaObject>("createForUsage", usageTouch);
            }
        }
        catch (System.Exception e)
        {
            touchAttributes = null;
            Debug.LogWarning($"[Haptic] VibrationAttributes 준비 실패 — 기본 경로를 씁니다: {e.Message}");
        }
    }

    /// <summary>
    /// 실제 vibrate 호출을 한 곳으로 모은다.
    /// 용도 표시가 가능하면 그쪽을, 실패하면 기본 경로를 쓴다.
    ///
    /// useAttributes 플래그를 내리는 이유 — 한 번 실패한 호출은 이 기기에서 계속 실패한다.
    /// 매번 예외를 던지고 잡으면 레벨업마다 비용이 생기므로 첫 실패에 경로를 갈아탄다.
    /// </summary>
    private static void CallVibrate(AndroidJavaObject effect)
    {
        if (useAttributes && touchAttributes != null)
        {
            try
            {
                vibrator.Call("vibrate", effect, touchAttributes);
                return;
            }
            catch (System.Exception e)
            {
                useAttributes = false;
                Debug.LogWarning($"[Haptic] 용도 표시 호출 실패 → 기본 경로로 전환: {e.Message}");
            }
        }

        vibrator.Call("vibrate", effect);
    }
#endif

    // ───────────────────────── 공개 API ─────────────────────────

    /// <summary>레벨업 연출용 진동. LevelUpEffect.Play()와 같은 타이밍에 부른다.</summary>
    public void LevelUp() => Vibrate(levelUpDurationMs, levelUpAmplitude);

    /// <summary>버튼 터치 등 가벼운 피드백.</summary>
    public void Light() => Vibrate(15, 80);

    /// <summary>보스 등장, 증강 카드 획득 등 묵직한 피드백.</summary>
    public void Heavy() => Vibrate(90, 255);

    /// <summary>단발 진동.</summary>
    /// <param name="durationMs">길이(밀리초)</param>
    /// <param name="amplitude">세기 1~255 (지원 기기에서만 반영)</param>
    /// <param name="ignoreCooldown">쿨다운을 무시할지. 결정적인 연출에만 true.</param>
    public void Vibrate(int durationMs, int amplitude = 255, bool ignoreCooldown = false)
    {
        // ★ 조용한 early return 을 전부 걷어냈다.
        //   "진동이 안 온다"의 원인이 설정인지, 기기인지, 쿨다운인지,
        //   애초에 호출이 안 된 건지를 구분할 방법이 없으면 고칠 수가 없다.
        if (!userEnabled)  { LogBlocked("유저 설정이 꺼져 있음 (PlayerPrefs haptic_enabled = 0)"); return; }
        if (!IsSupported)  { LogBlocked("IsSupported = false — 위쪽 '초기화 완료' 로그를 확인하세요"); return; }
        if (durationMs <= 0) { LogBlocked($"durationMs = {durationMs}"); return; }

        // 방치형에서 레벨이 한 번에 5개 오르면 진동도 5번 겹친다.
        // "따다다닥" 하고 손이 떨려서 연출이 아니라 고장처럼 느껴진다.
        if (!ignoreCooldown && Time.unscaledTime - lastVibrateTime < minInterval)
        {
            LogBlocked($"쿨다운 {minInterval}s 안에 재호출됨");
            return;
        }
        lastVibrateTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator == null || vibrationEffectClass == null) return;

        try
        {
            // 세기 제어를 못 하는 기기에는 DEFAULT_AMPLITUDE(-1)를 넘겨야 한다.
            // 지원 안 하는데 임의 값을 넣으면 기기에 따라 예외가 나기도 한다.
            int amp = hasAmplitudeControl ? Mathf.Clamp(amplitude, 1, 255) : DEFAULT_AMPLITUDE;

            using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                       "createOneShot", (long)durationMs, amp))
            {
                CallVibrate(effect);
            }

            if (logBlockedCalls)
                Debug.Log($"[Haptic] 진동 실행 — {durationMs}ms / 세기 {amp}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptic] 진동 실패: {e.Message}");
        }
#else
        Debug.Log($"[Haptic] (에디터) 진동 {durationMs}ms / 세기 {amplitude}");
#endif
    }

    /// <summary>
    /// 패턴 진동. 예: 톡-쉬고-톡 하는 2단 타격감.
    /// timingsMs는 [대기, 진동, 대기, 진동, ...] 순서다. 첫 값이 대기라는 점이 헷갈리기 쉽다.
    /// amplitudes를 주려면 timingsMs와 길이가 같아야 한다.
    /// </summary>
    public void VibratePattern(long[] timingsMs, int[] amplitudes = null, bool ignoreCooldown = false)
    {
        if (!userEnabled || !IsSupported) return;
        if (timingsMs == null || timingsMs.Length == 0) return;

        // ★ 추가된 검사 — createWaveform 은 두 배열의 길이가 다르면
        //   IllegalArgumentException 을 던진다. 이전 코드는 확인하지 않아서
        //   호출부가 길이를 잘못 맞추면 진동이 통째로 실패했다.
        if (amplitudes != null && amplitudes.Length != timingsMs.Length)
        {
            Debug.LogWarning($"[Haptic] timings({timingsMs.Length})와 amplitudes({amplitudes.Length})의 " +
                             "길이가 다릅니다. 세기를 무시하고 켜기/끄기 패턴으로 재생합니다.");
            amplitudes = null;
        }

        if (!ignoreCooldown && Time.unscaledTime - lastVibrateTime < minInterval) return;
        lastVibrateTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator == null || vibrationEffectClass == null) return;

        try
        {
            AndroidJavaObject effect;

            if (hasAmplitudeControl && amplitudes != null)
            {
                int[] safe = new int[amplitudes.Length];
                for (int i = 0; i < amplitudes.Length; i++)
                    safe[i] = Mathf.Clamp(amplitudes[i], 0, 255);   // 0 = 쉬는 구간이라 허용

                effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createWaveform", timingsMs, safe, NO_REPEAT);
            }
            else
            {
                // ★ 이전에는 여기서 deprecated 된 vibrator.vibrate(long[], int) 를 썼다.
                //   minSdk 30 이므로 createWaveform 의 타이밍 전용 오버로드를 쓰는 게 맞다.
                //   (세기 없이 켜기/끄기만 반복하는 패턴)
                effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                    "createWaveform", timingsMs, NO_REPEAT);
            }

            using (effect)
            {
                CallVibrate(effect);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Haptic] 패턴 진동 실패: {e.Message}");
        }
#else
        Debug.Log($"[Haptic] (에디터) 패턴 진동 {timingsMs.Length}단계");
#endif
    }

    private void LogBlocked(string reason)
    {
        if (!logBlockedCalls) return;
        Debug.LogWarning($"[Haptic] 진동을 건너뛰었습니다 — {reason}", this);
    }

    /// <summary>진행 중인 진동 중단. 씬 전환이나 앱 일시정지 시 호출하면 깔끔하다.</summary>
    public void Cancel()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try { vibrator?.Call("cancel"); }
        catch (System.Exception e) { Debug.LogWarning($"[Haptic] cancel 실패: {e.Message}"); }
#endif
    }

    // ───────────────────────── 유저 설정 ─────────────────────────

    /// <summary>설정 패널의 Toggle → On Value Changed 에 연결. (Dynamic bool 쪽을 고를 것)</summary>
    public void OnToggleChanged(bool on) => SetUserEnabled(on);

    public void SetUserEnabled(bool on)
    {
        userEnabled = on;
        PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
        PlayerPrefs.Save();

        // 켜는 순간 한 번 울려주면 "적용됐다"는 걸 손으로 알 수 있다.
        if (on) Vibrate(20, 120, ignoreCooldown: true);

        // 코드에서 직접 호출됐을 때도 화면의 토글이 따라오게 한다.
        // SetIsOnWithoutNotify 를 쓰므로 무한 루프가 생기지 않는다. (Binding.cs 참고)
        RefreshBoundToggle();
    }

    private void OnApplicationPause(bool pause)
    {
        // 앱이 백그라운드로 갈 때 진동이 계속 울리면 사용자가 당황한다.
        if (pause) Cancel();
    }
}
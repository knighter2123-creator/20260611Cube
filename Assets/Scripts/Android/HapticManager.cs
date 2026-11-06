using UnityEngine;

/// <summary>안드로이드 진동 매니저: JNI VibrationEffect 로 길이·세기를 정해 짧게 울림 (Handheld.Vibrate 는 항상 ~500ms 라 실패 시 대체용으로만).</summary>
// [붙이는 위치] 로그인 씬 ManagerRoot 하위 DontDestroyOnLoad 싱글턴
// ⚠ minSdk 30 전제(VibrationEffect·VibrationAttributes 분기 없음) — 30 미만으로 낮추면 이 파일을 다시 볼 것
// ⚠ VIBRATE 권한이 없으면 조용히 실패 — FallbackVibrate 의 Handheld.Vibrate 호출이 권한을 자동으로 넣어 주므로 지우면 Manifest 에 직접 추가
public partial class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }

    private const string PrefKey = "haptic_enabled";

    // 에디터에서만 읽는 필드라 #if 로 감쌈 (안 감싸면 실기기 빌드에서 CS0414 경고)
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

    [Tooltip("묵직한 진동(보스 등장 · 증강 카드 획득) 길이(ms).")]
    [SerializeField] private int heavyDurationMs = 90;

    [Tooltip("묵직한 진동 세기 1~255.")]
    [Range(1, 255)]
    [SerializeField] private int heavyAmplitude = 255;

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
#if UNITY_ANDROID && !UNITY_EDITOR
    private const int DEFAULT_AMPLITUDE = -1;   // VibrationEffect.DEFAULT_AMPLITUDE

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

        userEnabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
        InitVibrator();
    }

    private void OnDestroy()
    {
        // 설정 패널이 닫히기 전에 매니저가 파괴되면(계정 삭제 등) 토글에 리스너가 남습니다. 짝을 맞춰 끊습니다.
        UnbindToggle();

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
                // ★ VibratorManager 는 API 31+ — 이 분기를 지우면 안드로이드 11 에서만 진동이 죽음
                if (apiLevel >= 31)
                {
                    using (var vibratorManager = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                    {
                        if (vibratorManager != null)
                            vibrator = vibratorManager.Call<AndroidJavaObject>("getDefaultVibrator");
                    }
                }

                // API 30 경로 + 위 실패 시 폴백 (31+ 에선 deprecated 지만 동작)
                if (vibrator == null)
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");

                // 빌드된 APK 에 VIBRATE 권한이 실제로 들어갔는지 런타임 확인 (0 = GRANTED) — 매니페스트 파일에 있어도 빠질 수 있어서
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

            // 세기 제어 지원 여부 (저가형은 켜기/끄기만 — 미지원이면 최대 세기)
            hasAmplitudeControl = vibrator.Call<bool>("hasAmplitudeControl");

            InitVibrationAttributes();

            GameLog.Info($"[Haptic] 초기화 완료 - API {apiLevel}, 진동 지원 {IsSupported}, " +
                      $"권한 {hasPermission}, 세기 제어 {hasAmplitudeControl}, " +
                      $"용도 표시 {touchAttributes != null}");
        }
        catch (System.Exception e)
        {
            // JNI 는 기기·롬마다 예외가 날 수 있음 — 진동이 없어도 게임은 돌아야 하므로 위로 던지지 않음
            IsSupported = false;
            Debug.LogWarning($"[Haptic] 초기화 실패: {e.Message}");
        }
#elif UNITY_EDITOR
        // 에디터는 진동 불가지만 토글이 회색으로 죽지 않게 UI 상으로만 '지원됨' 처리 (Vibrate 는 아무것도 안 함)
        IsSupported = editorTreatAsSupported;

        // 에디터에서도 매니저가 있는지 확인할 수 있게 초기화 로그
        GameLog.Info($"[Haptic] (에디터) 초기화 — 실제 진동은 불가, UI 상 지원 {IsSupported}", this);
#else
        // iOS / PC — 진동 없음.
        IsSupported = false;
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>진동 용도를 USAGE_TOUCH 로 표시 — 용도 불명 진동은 안드로이드 12+ 에서 억제될 수 있어서 (커스텀 롬 대비 실패하면 기본 경로).</summary>
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

    /// <summary>실제 vibrate 호출 — 용도 표시 경로가 한 번 실패하면 이 기기에선 계속 실패하므로 기본 경로로 영구 전환(매번 예외 비용 방지).</summary>
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

    /// <summary>보스 등장·증강 획득 같은 묵직한 진동 — 매니저가 없거나 진동을 껐으면 아무것도 안 함.</summary>
    public static void PlayHeavy()
    {
        HapticManager h = Instance;
        if (h != null) h.Vibrate(h.heavyDurationMs, h.heavyAmplitude);   // Unity 오브젝트라 ?. 대신 != null
    }

    /// <summary>단발 진동.</summary>
    /// <param name="durationMs">길이(밀리초)</param>
    /// <param name="amplitude">세기 1~255 (지원 기기에서만 반영)</param>
    /// <param name="ignoreCooldown">쿨다운을 무시할지. 결정적인 연출에만 true.</param>
    public void Vibrate(int durationMs, int amplitude = 255, bool ignoreCooldown = false)
    {
        if (durationMs <= 0) { LogBlocked($"durationMs = {durationMs}"); return; }
        if (!TryPassGate(ignoreCooldown)) return;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (vibrator == null || vibrationEffectClass == null) { FallbackVibrate("Vibrator 없음"); return; }

        try
        {
            // 세기 제어 미지원 기기엔 DEFAULT_AMPLITUDE(-1) — 임의 값은 기기에 따라 예외
            int amp = hasAmplitudeControl ? Mathf.Clamp(amplitude, 1, 255) : DEFAULT_AMPLITUDE;

            using (var effect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                       "createOneShot", (long)durationMs, amp))
            {
                CallVibrate(effect);
            }
        }
        catch (System.Exception e)
        {
            FallbackVibrate($"진동 실패: {e.Message}");
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>JNI 실패 시 기본 진동(~500ms)으로 대체 — ★ 이 Handheld.Vibrate 호출이 빌드에 VIBRATE 권한을 넣어 줌.</summary>
    private static void FallbackVibrate(string reason)
    {
        Debug.LogWarning($"[Haptic] {reason} — 기본 진동으로 대신합니다.");
        try   { Handheld.Vibrate(); }
        catch (System.Exception e) { Debug.LogWarning($"[Haptic] 기본 진동도 실패: {e.Message}"); }
    }
#endif

    /// <summary>진동 관문(유저 설정·기기 지원·쿨다운) — 기기 미지원만 로그, 꺼짐·쿨다운은 정상이라 조용히 막음.</summary>
    private bool TryPassGate(bool ignoreCooldown)
    {
        if (!userEnabled) return false;
        if (!IsSupported) { LogBlocked("IsSupported = false — 위쪽 '초기화 완료' 로그를 확인하세요"); return false; }

        // 한 번에 여러 레벨이 오를 때 진동이 겹쳐 고장처럼 느껴지지 않게 쿨다운
        if (!ignoreCooldown && Time.unscaledTime - lastVibrateTime < minInterval) return false;

        lastVibrateTime = Time.unscaledTime;
        return true;
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

        // 코드에서 바꿔도 토글이 따라오게 (SetIsOnWithoutNotify 라 무한 루프 없음)
        RefreshBoundToggle();
    }

    private void OnApplicationPause(bool pause)
    {
        // 앱이 백그라운드로 갈 때 진동이 계속 울리면 사용자가 당황한다.
        if (pause) Cancel();
    }
}
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Volume 의 Bloom 을 코드로 켜고 끕니다.
///
/// 두 가지 축으로 동작합니다.
///  1) 유저 설정      : SetUserEnabled(false) 면 무슨 일이 있어도 블룸이 켜지지 않음 (PlayerPrefs 저장)
///  2) 연출 요청      : PulseFor(초) / Push()-Pop() 으로 "지금 블룸이 필요하다"고 요청
///
/// 방치형 게임은 오래 켜두므로, 평소엔 꺼두고 레벨업·가챠 같은 순간에만 켜는 걸 권장합니다.
///
/// 사용법 :
///   BloomController.Instance?.PulseFor(2f);          // 2초간 켜기
///   BloomController.Instance?.Push();  ... Pop();    // 구간 동안 켜기
///   BloomController.Instance?.SetUserEnabled(false); // 설정 메뉴 토글
/// </summary>
[DisallowMultipleComponent]
public class BloomController : MonoBehaviour
{
    public static BloomController Instance { get; private set; }

    private const string PrefsKey = "Settings_Bloom";

    [Header("대상")]
    [Tooltip("비우면 씬에서 Bloom 오버라이드를 가진 Volume 을 자동으로 찾습니다")]
    [SerializeField] private Volume targetVolume;
    [Tooltip("비우면 Camera.main 을 사용합니다")]
    [SerializeField] private Camera targetCamera;

    [Header("동작")]
    [Tooltip("항상 켜둠. 연출 요청과 무관하게 유지 (유저 설정은 여전히 적용됨)")]
    [SerializeField] private bool alwaysOn = false;
    [Tooltip("프로파일에 설정된 Intensity 대신 이 값을 사용. 0 이하면 프로파일 값 사용")]
    [SerializeField] private float onIntensity = 0f;
    [SerializeField] private float fadeInTime  = 0.10f;
    [SerializeField] private float fadeOutTime = 0.40f;

    [Header("성능")]
    [Tooltip("블룸이 꺼질 때 카메라의 포스트 프로세싱 패스 자체를 끕니다. " +
             "★컬러 그레이딩·비네트 등 다른 포스트 효과를 쓰고 있다면 반드시 체크 해제하세요.")]
    [SerializeField] private bool togglePostProcessing = true;

    [Header("Time.timeScale 무시")]
    [SerializeField] private bool useUnscaledTime = true;

    // ─────────────────────────────────────────────
    private Bloom bloom;
    private UniversalAdditionalCameraData cameraData;

    private float profileIntensity = 1f;   // 프로파일 원본 Intensity
    private float current;                 // 현재 적용 중인 Intensity
    private int   refCount;                // Push/Pop 카운트
    private float holdUntil;               // PulseFor 마감 시각
    private bool  userEnabled = true;
    private bool  ready;

    /// <summary>유저 설정상 블룸이 허용되는가</summary>
    public bool UserEnabled => userEnabled;

    /// <summary>지금 실제로 블룸이 그려지고 있는가</summary>
    public bool IsActive => current > 0.001f;

    private float Now => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float Delta => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[BloomController] 씬에 인스턴스가 둘 이상입니다. 나중 것을 사용합니다.", this);
        Instance = this;

        userEnabled = PlayerPrefs.GetInt(PrefsKey, 1) == 1;

        Resolve();
        ApplyImmediate(alwaysOn && userEnabled ? TargetIntensity : 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private float TargetIntensity => onIntensity > 0f ? onIntensity : profileIntensity;

    private void Resolve()
    {
        ready = false;

        // ── Volume 찾기 ──
        if (targetVolume == null)
        {
#if UNITY_2023_1_OR_NEWER
            Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
#else
            Volume[] volumes = FindObjectsOfType<Volume>();
#endif
            foreach (Volume v in volumes)
            {
                // sharedProfile 로 조회만 하고, 실제 수정은 profile(런타임 사본)에 함
                if (v != null && v.sharedProfile != null && v.sharedProfile.Has<Bloom>())
                {
                    targetVolume = v;
                    break;
                }
            }
        }

        if (targetVolume == null)
        {
            Debug.LogWarning("[BloomController] Bloom 오버라이드를 가진 Volume 을 찾지 못했습니다. " +
                             "Global Volume 에 Bloom 을 추가하세요.", this);
            return;
        }

        // profile 은 런타임 사본이라 여기서 값을 바꿔도 에셋이 더러워지지 않습니다
        if (!targetVolume.profile.TryGet(out bloom))
        {
            Debug.LogWarning("[BloomController] Volume 프로파일에 Bloom 이 없습니다.", this);
            return;
        }

        bloom.intensity.overrideState = true;
        profileIntensity = Mathf.Max(0.01f, bloom.intensity.value);

        // ── 카메라 ──
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null) cameraData = cam.GetUniversalAdditionalCameraData();

        ready = true;
    }

    // ─────────────────────────────────────────────
    // 외부 API
    // ─────────────────────────────────────────────

    /// <summary>지정한 시간(초) 동안 블룸을 켭니다. 중첩 호출 시 더 늦은 마감 시각으로 연장됩니다.</summary>
    public void PulseFor(float seconds)
    {
        if (seconds <= 0f) return;
        holdUntil = Mathf.Max(holdUntil, Now + seconds);
    }

    /// <summary>블룸 켜기 요청. 반드시 Pop() 과 짝을 맞추세요.</summary>
    public void Push() => refCount++;

    /// <summary>블룸 켜기 요청 해제.</summary>
    public void Pop() => refCount = Mathf.Max(0, refCount - 1);

    /// <summary>모든 요청을 강제로 비웁니다. (씬 전환 등)</summary>
    public void ClearRequests()
    {
        refCount = 0;
        holdUntil = 0f;
    }

    /// <summary>설정 메뉴용. 끄면 어떤 요청이 와도 블룸이 켜지지 않습니다.</summary>
    public void SetUserEnabled(bool on, bool save = true)
    {
        userEnabled = on;
        if (save)
        {
            PlayerPrefs.SetInt(PrefsKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (!on) ApplyImmediate(0f);   // 끌 때는 즉시 반영
    }

    /// <summary>UI Toggle 의 OnValueChanged 에 그대로 연결할 수 있는 진입점.</summary>
    public void OnToggleChanged(bool on) => SetUserEnabled(on);

    /// <summary>연출 요청과 무관하게 상시 켜둘지 여부.</summary>
    public void SetAlwaysOn(bool on) => alwaysOn = on;

    // ─────────────────────────────────────────────
    private void Update()
    {
        if (!ready) return;

        bool want = userEnabled && (alwaysOn || refCount > 0 || Now < holdUntil);

        float goal = want ? TargetIntensity : 0f;
        if (Mathf.Approximately(current, goal)) return;

        float time  = want ? fadeInTime : fadeOutTime;
        float speed = TargetIntensity / Mathf.Max(0.01f, time);

        ApplyImmediate(Mathf.MoveTowards(current, goal, speed * Delta));
    }

    private void ApplyImmediate(float intensity)
    {
        current = intensity;
        if (bloom == null) return;

        bool on = current > 0.001f;

        // active = false 면 URP 가 블룸 패스를 아예 건너뜁니다
        bloom.active = on;
        bloom.intensity.value = current;

        if (togglePostProcessing && cameraData != null)
            cameraData.renderPostProcessing = on;
    }
}

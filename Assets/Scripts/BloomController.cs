using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP Volume 의 Bloom 을 코드로 제어합니다.
///
/// 세 가지 축으로 동작합니다.
///  1) 유저 강도(슬라이더) : 0~1 값. 0이면 꺼짐. PlayerPrefs 저장
///  2) 유저 on/off(토글)   : 끄면 무슨 일이 있어도 안 켜짐. PlayerPrefs 저장
///  3) 연출 요청           : PulseFor(초) / Push()-Pop() 으로 "지금 블룸이 필요하다"고 요청
///
/// UI 연결 :
///   Slider.OnValueChanged  → BloomController.OnSliderChanged  (Min 0 / Max 1)
///   Toggle.OnValueChanged  → BloomController.OnToggleChanged
///
/// 코드 :
///   BloomController.Instance?.PulseFor(2f);
///   BloomController.Instance?.SetUserIntensity01(0.6f);
/// </summary>
[DisallowMultipleComponent]
public class BloomController : MonoBehaviour
{
    public static BloomController Instance { get; private set; }

    private const string PrefsEnabledKey   = "Settings_Bloom";
    private const string PrefsIntensityKey = "Settings_BloomIntensity";

    [Header("대상")]
    [Tooltip("비우면 씬에서 Bloom 오버라이드를 가진 Volume 을 자동으로 찾습니다")]
    [SerializeField] private Volume targetVolume;
    [Tooltip("비우면 Camera.main 을 사용합니다")]
    [SerializeField] private Camera targetCamera;

    [Header("강도 범위")]
    [Tooltip("슬라이더가 최대(1)일 때의 Bloom Intensity")]
    [SerializeField] private float maxIntensity = 2.0f;
    [Tooltip("저장된 설정이 없을 때 쓸 기본 슬라이더 값. 0 이하면 Volume 프로파일 값에서 자동 계산")]
    [Range(0f, 1f)]
    [SerializeField] private float defaultIntensity01 = 0f;

    [Header("동작")]
    [Tooltip("항상 켜둠. 연출 요청과 무관하게 유지 (유저 설정은 여전히 적용됨)")]
    [SerializeField] private bool alwaysOn = false;
    [SerializeField] private float fadeInTime  = 0.10f;
    [SerializeField] private float fadeOutTime = 0.40f;

    [Header("슬라이더 미리보기")]
    [Tooltip("슬라이더를 움직이는 동안 블룸을 강제로 켜서 결과를 바로 보여줍니다")]
    [SerializeField] private bool previewOnChange = true;
    [SerializeField] private float previewSeconds = 1.5f;

    [Header("성능")]
    [Tooltip("블룸이 꺼질 때 카메라의 포스트 프로세싱 패스 자체를 끕니다. " +
             "★컬러 그레이딩·비네트 등 다른 포스트 효과를 쓰고 있다면 반드시 체크 해제하세요.")]
    [SerializeField] private bool togglePostProcessing = true;

    [Header("Time.timeScale 무시")]
    [SerializeField] private bool useUnscaledTime = true;

    // ─────────────────────────────────────────────
    private Bloom bloom;
    private UniversalAdditionalCameraData cameraData;

    private float userScale = 1f;    // 0~1 슬라이더 값
    private bool  userEnabled = true;
    private float current;           // 현재 적용 중인 Intensity
    private int   refCount;
    private float holdUntil;
    private bool  ready;

    /// <summary>슬라이더에 표시할 0~1 값</summary>
    public float UserIntensity01 => userScale;

    /// <summary>유저 설정상 블룸이 허용되는가 (토글 + 슬라이더 0 여부)</summary>
    public bool UserEnabled => userEnabled && userScale > 0.001f;

    /// <summary>지금 실제로 블룸이 그려지고 있는가</summary>
    public bool IsActive => current > 0.001f;

    /// <summary>현재 설정에서 블룸이 켜졌을 때 적용될 Intensity</summary>
    public float TargetIntensity => UserEnabled ? maxIntensity * userScale : 0f;

    private float Now   => useUnscaledTime ? Time.unscaledTime : Time.time;
    private float Delta => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[BloomController] 씬에 인스턴스가 둘 이상입니다. 나중 것을 사용합니다.", this);
        Instance = this;

        Resolve();
        LoadSettings();

        ApplyImmediate(alwaysOn ? TargetIntensity : 0f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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
                if (v == null) continue;

                // sharedProfile 이 비어 있고 profile 만 설정된 Volume 도 있으므로 둘 다 확인
                VolumeProfile p = v.sharedProfile != null ? v.sharedProfile : v.profile;
                if (p != null && p.Has<Bloom>())
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

        // ★ Volume 에 Profile 자체가 비어 있으면 profile 이 null 이라 여기서 NullReference 가 납니다
        VolumeProfile profile = targetVolume.profile;
        if (profile == null)
        {
            Debug.LogError($"[BloomController] Volume '{targetVolume.name}' 에 Profile 이 없습니다. " +
                           "인스펙터의 Profile 칸에 프로파일을 넣거나 New 로 만들어 주세요.", targetVolume);
            return;
        }

        // profile 은 런타임 사본이라 여기서 값을 바꿔도 에셋이 더러워지지 않습니다
        if (!profile.TryGet(out bloom) || bloom == null)
        {
            Debug.LogError($"[BloomController] Volume '{targetVolume.name}' 의 프로파일에 Bloom 오버라이드가 없습니다. " +
                           "Add Override → Post-processing → Bloom 을 추가하세요.", targetVolume);
            bloom = null;
            return;
        }

        // ★ URP 버전이 바뀌었거나 프로파일 에셋이 깨지면 파라미터가 null 로 역직렬화될 수 있습니다.
        //   이 경우 Bloom 오버라이드를 지웠다가 다시 추가하면 복구됩니다.
        if (bloom.intensity == null)
        {
            Debug.LogError($"[BloomController] Bloom 의 intensity 파라미터가 비어 있습니다. " +
                           $"Volume '{targetVolume.name}' 의 프로파일에서 Bloom 오버라이드를 제거한 뒤 다시 추가해 주세요. " +
                           "(URP 버전 변경이나 프로파일 에셋 손상 시 발생합니다)", targetVolume);
            bloom = null;
            return;
        }

        bloom.intensity.overrideState = true;

        // 기본 슬라이더 값을 정하지 않았으면 프로파일에 설정된 Intensity 로부터 역산
        if (defaultIntensity01 <= 0f)
            defaultIntensity01 = Mathf.Clamp01(bloom.intensity.value / Mathf.Max(0.01f, maxIntensity));

        // ── 카메라 ──
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam != null) cameraData = cam.GetUniversalAdditionalCameraData();

        ready = true;
    }

    private void LoadSettings()
    {
        userEnabled = PlayerPrefs.GetInt(PrefsEnabledKey, 1) == 1;
        userScale   = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsIntensityKey, defaultIntensity01));
    }

    // ─────────────────────────────────────────────
    // 슬라이더 / 토글
    // ─────────────────────────────────────────────

    /// <summary>UI Slider 의 On Value Changed 에 그대로 연결하세요. (Min 0 / Max 1)</summary>
    public void OnSliderChanged(float value01) => SetUserIntensity01(value01);

    /// <summary>UI Toggle 의 On Value Changed 에 그대로 연결하세요.</summary>
    public void OnToggleChanged(bool on) => SetUserEnabled(on);

    /// <summary>블룸 강도를 0~1 로 설정합니다. 0이면 꺼진 것과 같습니다.</summary>
    public void SetUserIntensity01(float value01, bool save = true)
    {
        userScale = Mathf.Clamp01(value01);

        if (save)
        {
            PlayerPrefs.SetFloat(PrefsIntensityKey, userScale);
            PlayerPrefs.Save();
        }

        // 슬라이더를 움직이는 동안 결과가 바로 보이도록 강제로 켬
        if (previewOnChange && userEnabled && userScale > 0.001f)
        {
            PulseFor(previewSeconds);
            ApplyImmediate(TargetIntensity);   // 드래그 반응이 즉각적이도록 스냅
        }
        else if (userScale <= 0.001f)
        {
            ApplyImmediate(0f);
        }
        else if (IsActive)
        {
            ApplyImmediate(TargetIntensity);
        }
    }

    /// <summary>블룸 사용 여부. 끄면 어떤 요청이 와도 켜지지 않습니다.</summary>
    public void SetUserEnabled(bool on, bool save = true)
    {
        userEnabled = on;

        if (save)
        {
            PlayerPrefs.SetInt(PrefsEnabledKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        if (!on) ApplyImmediate(0f);
        else if (previewOnChange) { PulseFor(previewSeconds); ApplyImmediate(TargetIntensity); }
    }

    /// <summary>
    /// 토글 초기 상태를 저장된 설정으로 맞추고 리스너를 겁니다.
    /// ★ 인스펙터에서 On Value Changed 를 수동으로 연결하면 'Static Parameters' 를 잘못 고르기 쉽고,
    ///   시작 시 토글의 체크 상태가 저장값과 어긋납니다. 이 메서드를 쓰면 둘 다 해결됩니다.
    ///   (이 메서드로 연결했다면 인스펙터의 On Value Changed 항목은 비워두세요)
    /// </summary>
    public void BindToggle(UnityEngine.UI.Toggle toggle)
    {
        if (toggle == null) return;

        toggle.SetIsOnWithoutNotify(userEnabled);
        toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    /// <summary>슬라이더 초기값을 세팅할 때 쓰세요. (이벤트를 되쏘지 않음)</summary>
    public void BindSlider(UnityEngine.UI.Slider slider)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(userScale);
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    // ─────────────────────────────────────────────
    // 연출 요청
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

    /// <summary>연출 요청과 무관하게 상시 켜둘지 여부.</summary>
    public void SetAlwaysOn(bool on) => alwaysOn = on;

    // ─────────────────────────────────────────────
    private void Update()
    {
        if (!ready) return;

        bool want = UserEnabled && (alwaysOn || refCount > 0 || Now < holdUntil);

        float goal = want ? TargetIntensity : 0f;
        if (Mathf.Approximately(current, goal)) return;

        float time  = want ? fadeInTime : fadeOutTime;
        float speed = Mathf.Max(0.01f, maxIntensity) / Mathf.Max(0.01f, time);

        ApplyImmediate(Mathf.MoveTowards(current, goal, speed * Delta));
    }

    private void ApplyImmediate(float intensity)
    {
        current = intensity;
        if (bloom == null || bloom.intensity == null) return;

        bool on = current > 0.001f;

        // active = false 면 URP 가 블룸 패스를 아예 건너뜁니다
        bloom.active = on;
        bloom.intensity.value = current;

        if (togglePostProcessing && cameraData != null)
            cameraData.renderPostProcessing = on;
    }

    // ─────────────────────────────────────────────
    // 진단
    // ─────────────────────────────────────────────

    /// <summary>
    /// 블룸이 안 보일 때 원인을 한 번에 확인합니다.
    /// 인스펙터에서 컴포넌트 우클릭 → "블룸 진단" 으로도 실행할 수 있습니다.
    /// </summary>
    [ContextMenu("블룸 진단")]
    public void Diagnose()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═════ Bloom 진단 ═════");

        // ① 렌더 파이프라인 + HDR
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            sb.AppendLine("❌ 현재 활성 렌더 파이프라인이 URP 가 아닙니다. (Graphics / Quality 설정 확인)");
        }
        else
        {
            sb.AppendLine($"{Mark(urp.supportsHDR)} URP Asset 의 HDR : {urp.supportsHDR}");
            if (!urp.supportsHDR)
                sb.AppendLine("   → URP Asset 인스펙터에서 HDR 을 켜세요. 꺼져 있으면 밝기가 1.0 으로 잘려 블룸이 절대 안 나옵니다.");
        }

        // ② 카메라
        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
        {
            sb.AppendLine("❌ 카메라를 찾지 못했습니다. (Camera.main = MainCamera 태그 확인)");
        }
        else
        {
            var data = cam.GetUniversalAdditionalCameraData();
            sb.AppendLine($"{Mark(data != null && data.renderPostProcessing)} 카메라 '{cam.name}' 의 Post Processing : {(data != null && data.renderPostProcessing)}");
            if (data != null && !data.renderPostProcessing)
                sb.AppendLine("   → 지금 블룸이 꺼진 상태라면 정상입니다(이 스크립트가 껐음). 연출 중에도 false 면 카메라 체크박스를 확인하세요.");
        }

        // ③ Volume
        if (targetVolume == null)
        {
            sb.AppendLine("❌ Bloom 오버라이드를 가진 Volume 을 찾지 못했습니다.");
            sb.AppendLine("   → Hierarchy 우클릭 → Volume → Global Volume, Profile 에 Bloom 오버라이드를 추가하세요.");
        }
        else
        {
            sb.AppendLine($"✅ Volume : '{targetVolume.name}'");
            sb.AppendLine($"{Mark(targetVolume.isGlobal)} isGlobal : {targetVolume.isGlobal}");
            if (!targetVolume.isGlobal)
                sb.AppendLine("   → Local Volume 이면 카메라가 콜라이더 안에 들어와야 적용됩니다. Global 로 바꾸는 걸 권장합니다.");

            sb.AppendLine($"{Mark(targetVolume.weight > 0.99f)} weight : {targetVolume.weight} (1이어야 온전히 적용)");
            sb.AppendLine($"{Mark(targetVolume.enabled && targetVolume.gameObject.activeInHierarchy)} 활성 상태 : {targetVolume.enabled && targetVolume.gameObject.activeInHierarchy}");
            sb.AppendLine($"{Mark(targetVolume.sharedProfile != null)} Profile 할당 : " +
                          $"{(targetVolume.sharedProfile != null ? targetVolume.sharedProfile.name : "없음 ← Profile 칸이 비어 있습니다")}");
        }

        // ④ Bloom 오버라이드
        if (bloom == null)
        {
            sb.AppendLine("❌ Bloom 오버라이드를 잡지 못했습니다. (위의 Profile / Bloom 항목을 확인하세요)");
        }
        else if (bloom.intensity == null)
        {
            sb.AppendLine("❌ Bloom 은 있지만 intensity 파라미터가 null 입니다. " +
                          "프로파일에서 Bloom 오버라이드를 제거 후 다시 추가하세요.");
        }
        else
        {
            sb.AppendLine($"✅ Bloom 오버라이드 확보");
            sb.AppendLine($"   active            : {bloom.active}");
            sb.AppendLine($"   intensity(현재)   : {bloom.intensity.value:0.###}");
            sb.AppendLine($"   threshold         : {bloom.threshold.value:0.###}  (1.0 근처 권장)");
            sb.AppendLine($"   {Mark(bloom.threshold.overrideState)} threshold override : {bloom.threshold.overrideState}");
            if (!bloom.threshold.overrideState)
                sb.AppendLine("   → Bloom 오버라이드에서 Threshold 왼쪽 체크박스가 꺼져 있습니다. 켜고 값을 지정하세요.");
        }

        // ⑤ 이 스크립트의 상태
        sb.AppendLine("───── 컨트롤러 상태 ─────");
        sb.AppendLine($"   유저 토글 (userEnabled) : {userEnabled}");
        sb.AppendLine($"   유저 강도 (slider 0~1)  : {userScale:0.###}");
        sb.AppendLine($"   켜졌을 때 Intensity     : {TargetIntensity:0.###}  (maxIntensity {maxIntensity})");
        sb.AppendLine($"   alwaysOn                : {alwaysOn}");
        sb.AppendLine($"   요청 refCount           : {refCount}");
        sb.AppendLine($"   PulseFor 남은 시간      : {Mathf.Max(0f, holdUntil - Now):0.##}s");
        sb.AppendLine($"   현재 적용 Intensity     : {current:0.###}");

        if (!alwaysOn && refCount == 0 && Now >= holdUntil)
        {
            sb.AppendLine();
            sb.AppendLine("ℹ️ 지금은 아무 연출도 블룸을 요청하지 않아 '의도적으로 꺼진' 상태입니다.");
            sb.AppendLine("   테스트하려면 Always On 을 체크하거나, 우클릭 → '3초간 켜보기' 를 실행하세요.");
        }

        Debug.Log(sb.ToString(), this);
    }

    [ContextMenu("3초간 켜보기")]
    private void TestPulse()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BloomController] 플레이 모드에서만 테스트할 수 있습니다.");
            return;
        }
        PulseFor(3f);
        Debug.Log("[BloomController] 3초간 블룸을 켭니다.", this);
    }

    private static string Mark(bool ok) => ok ? "✅" : "⚠️";
}
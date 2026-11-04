using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM / 효과음 재생과 볼륨 설정을 담당합니다.
/// ManagerRoot 하위에 배치하세요 (DontDestroyOnLoad는 ManagerRoot가 담당).
///
/// 사용법 :
///   효과음  — SoundManager.Instance.PlaySfx(clip)            (Unity 오브젝트라 ?. 대신 != null 로 확인)
///   배경음  — 씬에 SceneBgm 컴포넌트를 두고 클립을 넣으면 그 씬에 들어갈 때 바뀝니다 (비우면 멈춤).
///   볼륨    — 설정 창의 효과음 / 배경음 슬라이더 (GameSettingManager.Bindings)
///
/// 소리가 안 나면 컴포넌트 우클릭 → "사운드 진단" 을 실행하세요.
/// </summary>
[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private const string PrefsMaster = "Settings_VolMaster";
    private const string PrefsBgm    = "Settings_VolBgm";
    private const string PrefsSfx    = "Settings_VolSfx";

    [Header("오디오 소스 (비우면 자동 생성)")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("기본 볼륨 (저장된 설정이 없을 때)")]
    [Range(0f, 1f)] [SerializeField] private float defaultMaster = 1f;
    [Range(0f, 1f)] [SerializeField] private float defaultBgm    = 0.6f;
    [Range(0f, 1f)] [SerializeField] private float defaultSfx    = 1f;

    [Header("중복 재생 억제")]
    [Tooltip("같은 클립이 이 간격(초) 안에 다시 요청되면 무시합니다.\n" +
             "적이 한꺼번에 죽거나 100연차를 돌릴 때 같은 소리가 겹쳐 뭉개지는 걸 막습니다. 0이면 억제 안 함")]
    [SerializeField] private float sameClipMinInterval = 0.04f;

    // ─────────────────────────────────────────────
    private float master, bgmVol, sfxVol;
    private readonly Dictionary<int, float> lastPlayed = new Dictionary<int, float>();

    public float MasterVolume01 => master;
    public float BgmVolume01    => bgmVol;
    public float SfxVolume01    => sfxVol;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        EnsureSources();
        LoadSettings();
        ApplyVolumes();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void EnsureSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
        }
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        foreach (AudioSource s in new[] { bgmSource, sfxSource })
        {
            s.playOnAwake = false;

            // ★ 2D 게임에서 소리가 안 들리는 가장 흔한 원인입니다.
            //   spatialBlend 가 1(3D)이면 리스너와의 거리에 따라 감쇠돼 거의 안 들립니다.
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
        }
    }

    private void LoadSettings()
    {
        master = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsMaster, defaultMaster));
        bgmVol = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsBgm,    defaultBgm));
        sfxVol = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefsSfx,    defaultSfx));
    }

    private void ApplyVolumes()
    {
        if (bgmSource != null) bgmSource.volume = master * bgmVol;
        // SFX 는 PlayOneShot 의 volumeScale 로 그때그때 적용합니다
    }

    // ══════════════════════════════════════════════
    //  재생
    // ══════════════════════════════════════════════

    /// <summary>효과음 1회 재생. 여러 개가 동시에 겹쳐도 됩니다.</summary>
    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null) return;
        if (sfxSource == null) EnsureSources();

        float vol = master * sfxVol * Mathf.Max(0f, volumeScale);
        if (vol <= 0.001f) return;   // 볼륨 0이면 재생 자체를 생략

        // 같은 클립이 순식간에 여러 번 요청되면 위상이 겹쳐 소리가 지저분해집니다
        if (sameClipMinInterval > 0f)
        {
            int id = clip.GetInstanceID();
            float now = Time.unscaledTime;

            if (lastPlayed.TryGetValue(id, out float last) && now - last < sameClipMinInterval)
                return;

            lastPlayed[id] = now;
        }

        sfxSource.PlayOneShot(clip, Mathf.Clamp01(vol));
    }

    /// <summary>BGM 재생. 같은 클립이 이미 재생 중이면 아무 것도 하지 않습니다.</summary>
    public void PlayBgm(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        if (bgmSource == null) EnsureSources();

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip   = clip;
        bgmSource.loop   = loop;
        bgmSource.volume = master * bgmVol;
        bgmSource.Play();
    }

    public void StopBgm()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    // ══════════════════════════════════════════════
    //  볼륨 설정 (설정 패널 — GameSettingManager.Bindings 가 열 때 Bind, 닫을 때 Unbind)
    //  마스터 볼륨은 슬라이더가 없어 저장된 값(기본 defaultMaster)을 그대로 씁니다.
    // ══════════════════════════════════════════════

    public void SetBgmVolume01(float v) => SetVolume(PrefsBgm, ref bgmVol, v);
    public void SetSfxVolume01(float v) => SetVolume(PrefsSfx, ref sfxVol, v);

    /// <summary>
    /// 값만 바꾸고 바로 적용합니다. 디스크 저장(PlayerPrefs.Save)은 하지 않습니다 —
    /// 슬라이더를 끄는 동안 매 프레임 불리므로, 저장은 패널을 닫을 때 한 번 (UnbindSliders → SaveVolumes).
    /// </summary>
    private void SetVolume(string key, ref float field, float value)
    {
        field = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(key, field);
        ApplyVolumes();
    }

    /// <summary>볼륨 설정을 디스크에 저장합니다 (앱이 꺼질 때도 유니티가 저장하지만, 강제 종료에 대비).</summary>
    public void SaveVolumes() => PlayerPrefs.Save();

    /// <summary>UI Slider 연결 — 저장값으로 위치를 맞추고 리스너까지 겁니다. (Min 0 / Max 1) 닫을 때 UnbindSliders 로 짝을 맞추세요.</summary>
    public void BindBgmSlider(UnityEngine.UI.Slider s) => Bind(s, bgmVol, SetBgmVolume01);
    public void BindSfxSlider(UnityEngine.UI.Slider s) => Bind(s, sfxVol, SetSfxVolume01);

    /// <summary>
    /// Bind*Slider 의 짝 — 건 리스너를 떼고 저장합니다. 안 떼면 패널을 열 때마다 리스너가 하나씩 쌓입니다.
    /// (UnityEvent 는 대상 오브젝트 + 메서드로 비교하므로 등록 때와 다른 델리게이트로도 정확히 제거됨)
    /// </summary>
    public void UnbindSliders(UnityEngine.UI.Slider sfx, UnityEngine.UI.Slider bgm)
    {
        if (sfx != null) sfx.onValueChanged.RemoveListener(SetSfxVolume01);
        if (bgm != null) bgm.onValueChanged.RemoveListener(SetBgmVolume01);
        SaveVolumes();
    }

    private static void Bind(UnityEngine.UI.Slider slider, float value,
                             UnityEngine.Events.UnityAction<float> setter)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.RemoveListener(setter);   // 혹시 Unbind 없이 다시 열려도 겹치지 않게
        slider.onValueChanged.AddListener(setter);
    }

    // ══════════════════════════════════════════════
    //  진단
    // ══════════════════════════════════════════════

    /// <summary>소리가 안 날 때 원인을 한 번에 확인합니다.</summary>
    [ContextMenu("사운드 진단")]
    public void Diagnose()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("═════ 사운드 진단 ═════");

        AudioListener listener = FindFirstObjectByType<AudioListener>();
        if (listener == null)
            sb.AppendLine("❌ 씬에 AudioListener 가 없습니다. 보통 Main Camera 에 붙어 있어야 합니다.");
        else
            sb.AppendLine($"✅ AudioListener : '{listener.name}' (enabled={listener.enabled})");

        sb.AppendLine($"{(AudioListener.volume > 0.001f ? "✅" : "❌")} AudioListener.volume : {AudioListener.volume:0.##}");
        sb.AppendLine($"{(AudioListener.pause ? "❌" : "✅")} AudioListener.pause  : {AudioListener.pause}" +
                      (AudioListener.pause ? "  ← 일시정지 상태라 소리가 나지 않습니다" : ""));

        sb.AppendLine($"볼륨 : master {master:0.##} / bgm {bgmVol:0.##} / sfx {sfxVol:0.##}");
        if (master * sfxVol <= 0.001f)
            sb.AppendLine("❌ 효과음 실효 볼륨이 0입니다.");

        DescribeSource(sb, "BGM", bgmSource);
        DescribeSource(sb, "SFX", sfxSource);

        Debug.Log(sb.ToString(), this);
    }

    private static void DescribeSource(System.Text.StringBuilder sb, string label, AudioSource s)
    {
        if (s == null) { sb.AppendLine($"❌ {label} AudioSource 없음"); return; }

        sb.AppendLine($"✅ {label} AudioSource : volume={s.volume:0.##}, mute={s.mute}, " +
                      $"spatialBlend={s.spatialBlend:0.##}, enabled={s.enabled}, " +
                      $"활성={s.gameObject.activeInHierarchy}");

        if (s.mute)                sb.AppendLine($"   ⚠️ {label} 이 음소거 상태입니다.");
        if (s.spatialBlend > 0.5f) sb.AppendLine($"   ⚠️ {label} 이 3D 사운드로 설정돼 있어 거리에 따라 안 들릴 수 있습니다.");
        if (!s.gameObject.activeInHierarchy) sb.AppendLine($"   ⚠️ {label} 오브젝트가 비활성 상태입니다. 비활성 오브젝트에서는 재생되지 않습니다.");
    }
}
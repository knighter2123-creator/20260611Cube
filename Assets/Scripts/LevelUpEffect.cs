using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// FF14 스타일 레벨업 연출.
/// 발밑에서 황금빛 광주가 솟구치고 → 링이 퍼지며 → "LEVEL UP!" 텍스트가 팝인합니다.
///
/// 스프라이트/프리팹을 전혀 준비할 필요가 없습니다. 모든 텍스처를 런타임에 생성하고
/// Custom/2D/SpriteAdditiveHDR 셰이더로 HDR 가산 합성해서 Bloom 이 반응하게 만듭니다.
///
/// 사용법 :
///   LevelUpEffect.Instance.Play(newLevel);          // 씬에 하나 배치해두고 호출
///   LevelUpEffect.Instance.PlayAt(player, newLevel); // 대상 지정
/// </summary>
[DisallowMultipleComponent]
public class LevelUpEffect : MonoBehaviour
{
    public static LevelUpEffect Instance { get; private set; }

    private const string AdditiveShader = "Custom/2D/SpriteAdditiveHDR";
    private const float  PPU = 100f;      // 절차 생성 스프라이트의 Pixels Per Unit

    // ─────────────────────────────────────────────
    [Header("대상")]
    [Tooltip("연출이 재생될 위치. 비우면 이 오브젝트 자신을 사용")]
    [SerializeField] private Transform target;
    [Tooltip("대상 기준 위치 보정 (보통 발밑으로 내림)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, -0.4f, 0f);

    [Header("렌더 정렬")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 200;

    [Header("색상 (HDR — 1을 넘겨야 블룸이 반응합니다)")]
    [ColorUsage(true, true)] public Color pillarColor = new Color(4.5f, 3.6f, 1.8f, 1f);
    [ColorUsage(true, true)] public Color ringColor   = new Color(5.0f, 4.2f, 2.4f, 1f);
    [ColorUsage(true, true)] public Color glowColor   = new Color(6.0f, 5.0f, 3.0f, 1f);
    [ColorUsage(true, true)] public Color sparkColor  = new Color(5.0f, 4.4f, 2.6f, 1f);
    [ColorUsage(true, true)] public Color flashColor  = new Color(1.6f, 1.5f, 1.2f, 1f);

    [Header("광주")]
    [Tooltip("광주 최대 높이(월드 유닛)")]
    public float pillarHeight = 3.6f;
    [Tooltip("광주 폭(월드 유닛)")]
    public float pillarWidth = 1.1f;
    public float pillarRiseTime = 0.28f;
    public float pillarHoldTime = 0.45f;
    public float pillarFadeTime = 0.70f;

    [Header("확산 링")]
    [Tooltip("링이 퍼지는 최대 반지름(월드 유닛)")]
    public float ringMaxRadius = 2.2f;
    public float ringExpandTime = 0.75f;
    [Tooltip("두 번째 링 지연 시간")]
    public float ringSecondDelay = 0.20f;

    [Header("상승 파티클")]
    [Range(0, 32)] public int sparkCount = 14;
    public float sparkRiseHeight = 3.0f;
    public float sparkSpread = 0.9f;
    public float sparkLifetime = 1.1f;

    [Header("텍스트")]
    [Tooltip("비우면 TMP 기본 폰트 사용")]
    [SerializeField] private TMP_FontAsset fontAsset;
    public string mainText = "LEVEL UP!";
    [Tooltip("서브 텍스트 포맷. {0} 자리에 레벨이 들어갑니다. 비우면 표시 안 함")]
    public string subTextFormat = "Lv. {0}";
    public float textOffsetY = 1.5f;
    public float mainFontSize = 3.2f;
    public float subFontSize  = 1.6f;
    public Color textFaceColor = new Color(1f, 0.96f, 0.80f, 1f);
    [Tooltip("텍스트 자체의 HDR 배율. 글자까지 빛나게 하려면 2 이상")]
    [Range(1f, 8f)] public float textHdrBoost = 3.0f;
    public float textAppearDelay = 0.10f;
    public float textPopTime = 0.32f;
    public float textHoldTime = 0.85f;
    public float textFadeTime = 0.55f;
    [Tooltip("텍스트가 떠오르는 거리")]
    public float textRise = 0.45f;

    [Header("추가 연출")]
    [Tooltip("연출이 재생되는 동안에만 Bloom 을 켭니다. 씬에 BloomController 가 있어야 동작합니다")]
    public bool controlBloom = true;
    [Tooltip("연출이 끝난 뒤에도 블룸을 유지할 여유 시간(페이드아웃용)")]
    public float bloomTailTime = 0.4f;

    [Tooltip("블룸 설정이 꺼져 있으면 레벨업 연출 자체를 아예 재생하지 않습니다.\n" +
             "체크 해제(기본)면 블룸만 빠지고 광주·링·텍스트는 그대로 나옵니다.")]
    public bool skipWhenBloomDisabled = false;

    public bool useScreenFlash = true;
    [Tooltip("직교 카메라에서만 동작합니다")]
    public float flashTime = 0.18f;
    [SerializeField] private ScreenShake screenShake;

    [Header("사운드")]
    [Tooltip("SoundManager 가 있으면 그쪽으로 재생합니다. 볼륨 설정이 적용되므로 권장")]
    [SerializeField] private bool preferSoundManager = true;
    [Tooltip("SoundManager 가 없을 때 쓸 예비 AudioSource (선택)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip   levelUpSfx;
    [Range(0f, 2f)]
    [SerializeField] private float sfxVolumeScale = 1f;

    [Header("자동 연결")]
    [Tooltip("LevelUpManager.OnLevelUp 을 자동으로 구독해서, 레벨업 시 알아서 재생합니다")]
    public bool autoBindLevelUpManager = true;
    [Tooltip("연결 성공/재생 시 콘솔에 로그를 남깁니다")]
    public bool logBinding = true;

    [Tooltip("씬 시작 후 이 시간(초) 동안은 연출을 재생하지 않고 현재 레벨을 기준값으로만 따라갑니다. " +
             "세이브 로드로 Lv.1 → Lv.15 가 복원될 때 레벨업 연출이 터지는 걸 막습니다.")]
    public float suppressAfterLoadSeconds = 1.5f;

    [Header("기타")]
    [Tooltip("이 간격 안에 다시 호출되면 무시 (연속 레벨업 스팸 방지)")]
    public float minInterval = 0.15f;
    [Tooltip("Time.timeScale 영향을 받지 않게 재생")]
    public bool useUnscaledTime = true;

    // ─────────────────────────────────────────────
    private Transform  root;
    private SpriteRenderer pillar, ring1, ring2, baseGlow, flash;
    private SpriteRenderer[] sparks;
    private Spark[] sparkData;
    private TextMeshPro mainLabel, subLabel;

    private Coroutine playing;
    private float lastPlayTime = -999f;
    private bool built;

    private struct Spark
    {
        public float angle;     // 좌우 퍼짐 방향(-1 ~ 1)
        public float speed;     // 상승 속도 배율
        public float delay;
        public float size;
        public float wobble;    // 좌우 흔들림 위상
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // ─────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[LevelUpEffect] 씬에 인스턴스가 둘 이상입니다. 나중 것을 사용합니다.", this);
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        Unbind();

        // root 는 씬 루트에 따로 만들어지므로 직접 정리
        if (root != null) Destroy(root.gameObject);
        if (flash != null && flash.transform.parent != root) Destroy(flash.gameObject);
    }

    // ─────────────────────────────────────────────
    // LevelUpManager 자동 연결
    // ─────────────────────────────────────────────
    private LevelUpManager boundManager;
    private int   lastKnownLevel = -1;
    private float suppressUntil;

    /// <summary>지금이 '시작 직후 억제 구간'인가</summary>
    private bool IsSuppressed => Time.unscaledTime < suppressUntil;

    private void OnEnable()
    {
        // 씬이 새로 로드될 때마다 억제 구간을 다시 잡습니다
        suppressUntil = Time.unscaledTime + Mathf.Max(0f, suppressAfterLoadSeconds);
        TryBind();
    }

    private void OnDisable() => Unbind();

    private void Update()
    {
        // LevelUpManager 가 나중에 만들어지는 구조여도 붙을 때까지 재시도
        if (autoBindLevelUpManager && boundManager == null) TryBind();

        // ★ 억제 구간 동안은 연출 없이 현재 레벨만 계속 따라갑니다.
        //   세이브 로드가 언제 끝나든(스탯이 나중에 주입돼도) 기준값이 복원된 레벨로 맞춰집니다.
        if (boundManager != null && IsSuppressed)
            lastKnownLevel = boundManager.CurrentLevel;
    }

    private void TryBind()
    {
        if (!autoBindLevelUpManager) return;
        if (boundManager != null) return;
        if (LevelUpManager.Instance == null) return;

        boundManager = LevelUpManager.Instance;
        boundManager.OnLevelUp += HandleLevelUp;
        lastKnownLevel = boundManager.CurrentLevel;

        if (logBinding)
            Debug.Log($"[LevelUpEffect] LevelUpManager 에 연결됐습니다. (현재 Lv.{lastKnownLevel}, " +
                      $"{Mathf.Max(0f, suppressUntil - Time.unscaledTime):0.0}초간 연출 억제)", this);
    }

    /// <summary>
    /// 세이브 로드가 끝난 직후 직접 호출하면, 시간 창에 의존하지 않고 기준 레벨을 확정할 수 있습니다.
    /// 예) SaveManager 로드 완료 지점에서 LevelUpEffect.Instance?.SyncLevelBaseline();
    /// </summary>
    public void SyncLevelBaseline()
    {
        if (boundManager == null) TryBind();
        if (boundManager == null) return;

        lastKnownLevel = boundManager.CurrentLevel;
        suppressUntil  = 0f;   // 기준이 확정됐으므로 억제 해제

        if (logBinding)
            Debug.Log($"[LevelUpEffect] 기준 레벨을 Lv.{lastKnownLevel} 로 확정했습니다.", this);
    }

    private void Unbind()
    {
        if (boundManager != null) boundManager.OnLevelUp -= HandleLevelUp;
        boundManager = null;
    }

    private void HandleLevelUp(int newLevel)
    {
        // ★ 시작 직후 억제 구간 : 세이브 로드로 Lv.1 → Lv.15 가 복원되는 것을
        //   레벨업으로 오인하지 않도록, 기준값만 올리고 연출은 건너뜁니다.
        if (IsSuppressed)
        {
            lastKnownLevel = Mathf.Max(lastKnownLevel, newLevel);

            if (logBinding)
                Debug.Log($"[LevelUpEffect] 시작 직후라 Lv.{newLevel} 통보를 복원으로 간주하고 연출을 생략했습니다.", this);
            return;
        }

        // 씬 전환 시 스탯 복원으로 같은(또는 더 낮은) 레벨이 재통보되는 경우도 연출하지 않음
        if (newLevel <= lastKnownLevel)
        {
            lastKnownLevel = newLevel;
            return;
        }

        lastKnownLevel = newLevel;

        if (logBinding)
            Debug.Log($"[LevelUpEffect] 레벨업 감지 → Lv.{newLevel} 연출 재생", this);

        Play(newLevel);
    }

    // ─────────────────────────────────────────────
    // 재생
    // ─────────────────────────────────────────────

    /// <summary>기본 대상 위치에서 레벨업 연출을 재생합니다.</summary>
    public void Play(int newLevel) => PlayAt(target != null ? target : transform, newLevel);

    /// <summary>지정한 대상 위치에서 레벨업 연출을 재생합니다.</summary>
    public void PlayAt(Transform at, int newLevel)
    {
        // 저사양 모드처럼 "블룸을 끄면 연출도 통째로 끄고 싶다"는 경우
        if (skipWhenBloomDisabled &&
            BloomController.Instance != null &&
            !BloomController.Instance.UserEnabled)
        {
            if (logBinding)
                Debug.Log("[LevelUpEffect] 블룸 설정이 꺼져 있어 연출을 건너뜁니다.", this);
            return;
        }

        float now = useUnscaledTime ? Time.unscaledTime : Time.time;
        if (now - lastPlayTime < minInterval) return;
        lastPlayTime = now;

        EnsureBuilt();
        if (!built) return;

        if (at != null)
            root.position = at.position + offset;

        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(Sequence(newLevel));
    }

    private IEnumerator Sequence(int newLevel)
    {
        // ── 초기 상태 ──
        root.gameObject.SetActive(true);
        RandomizeSparks();

        if (mainLabel != null) mainLabel.text = mainText;
        if (subLabel  != null)
            subLabel.text = string.IsNullOrEmpty(subTextFormat) ? string.Empty : string.Format(subTextFormat, newLevel);

        PlaySfx();

        if (screenShake != null)
            screenShake.Shake();

        SetupFlashQuad();

        float pillarTotal = pillarRiseTime + pillarHoldTime + pillarFadeTime;
        float textTotal   = textAppearDelay + textPopTime + textHoldTime + textFadeTime;
        float sparkTotal  = sparkLifetime + 0.4f;
        float total = Mathf.Max(pillarTotal, Mathf.Max(textTotal, sparkTotal));

        // 연출 구간에만 블룸을 켠다 (BloomController 가 없으면 조용히 건너뜀)
        if (controlBloom && BloomController.Instance != null)
            BloomController.Instance.PulseFor(total + bloomTailTime);

        float t = 0f;
        while (t < total)
        {
            UpdateFlash(t);
            UpdatePillar(t);
            UpdateRing(ring1, t, 0f);
            UpdateRing(ring2, t, ringSecondDelay);
            UpdateGlow(t);
            UpdateSparks(t);
            UpdateText(t);

            t += DeltaTime;
            yield return null;
        }

        root.gameObject.SetActive(false);
        playing = null;
    }

    /// <summary>
    /// 효과음 재생. 원래는 audioSource/levelUpSfx 가 비어 있으면 조용히 아무 일도 안 해서
    /// "왜 소리가 안 나지"를 알 방법이 없었습니다. 실패 원인을 명시합니다.
    /// </summary>
    private void PlaySfx()
    {
        if (levelUpSfx == null)
        {
            if (logBinding)
                Debug.LogWarning("[LevelUpEffect] levelUpSfx 가 비어 있어 효과음을 재생하지 않습니다.", this);
            return;
        }

        if (preferSoundManager && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySfx(levelUpSfx, sfxVolumeScale);
            return;
        }

        if (audioSource == null)
        {
            Debug.LogWarning("[LevelUpEffect] SoundManager 도 없고 audioSource 도 비어 있어 효과음을 재생할 수 없습니다. " +
                             "씬에 SoundManager 를 두거나 audioSource 를 연결하세요.", this);
            return;
        }

        if (!audioSource.isActiveAndEnabled)
        {
            Debug.LogWarning($"[LevelUpEffect] audioSource('{audioSource.name}') 가 비활성 상태라 재생되지 않습니다. " +
                             "비활성 오브젝트의 AudioSource 는 소리를 내지 못합니다.", audioSource);
            return;
        }

        audioSource.PlayOneShot(levelUpSfx, Mathf.Clamp01(sfxVolumeScale));
    }

    // ── 개별 요소 ────────────────────────────────

    private void UpdateFlash(float t)
    {
        if (flash == null) return;

        if (!useScreenFlash || t >= flashTime)
        {
            SetIntensity(flash, flashColor, 0f);
            return;
        }

        float k = 1f - (t / flashTime);
        SetIntensity(flash, flashColor, k * k);      // 빠르게 감쇠
    }

    private void UpdatePillar(float t)
    {
        float height, intensity;

        if (t < pillarRiseTime)
        {
            float k = t / pillarRiseTime;
            height    = EaseOutQuint(k);
            intensity = Mathf.Lerp(0.35f, 1f, EaseOutCubic(k));
        }
        else if (t < pillarRiseTime + pillarHoldTime)
        {
            height    = 1f;
            intensity = 1f;
        }
        else
        {
            float k = (t - pillarRiseTime - pillarHoldTime) / pillarFadeTime;
            if (k >= 1f) { SetIntensity(pillar, pillarColor, 0f); return; }
            height    = 1f + k * 0.25f;              // 사라지면서 살짝 더 뻗음
            intensity = 1f - EaseOutCubic(k);
        }

        // 솟구칠 때 폭이 좁아지며 조여드는 느낌
        float widthK = Mathf.Lerp(1.45f, 1f, EaseOutCubic(Mathf.Clamp01(t / (pillarRiseTime * 1.6f))));

        pillar.transform.localScale = new Vector3(
            (pillarWidth  * widthK) / (BeamW / PPU),
            (pillarHeight * height) / (BeamH / PPU),
            1f);

        SetIntensity(pillar, pillarColor, intensity);
    }

    private void UpdateRing(SpriteRenderer r, float t, float delay)
    {
        float local = t - delay;
        if (local < 0f || local >= ringExpandTime)
        {
            SetIntensity(r, ringColor, 0f);
            return;
        }

        float k = local / ringExpandTime;
        float radius = Mathf.Lerp(0.15f, ringMaxRadius, EaseOutQuint(k));
        float scale  = (radius * 2f) / (RingS / PPU);

        // 원근감을 주기 위해 세로를 눌러 타원으로
        r.transform.localScale = new Vector3(scale, scale * 0.42f, 1f);
        SetIntensity(r, ringColor, (1f - k) * (1f - k));
    }

    private void UpdateGlow(float t)
    {
        const float pop = 0.09f;
        const float decay = 0.55f;

        float intensity;
        float scale;

        if (t < pop)
        {
            float k = t / pop;
            intensity = k;
            scale = Mathf.Lerp(0.4f, 1.15f, EaseOutCubic(k));
        }
        else if (t < pop + decay)
        {
            float k = (t - pop) / decay;
            intensity = 1f - EaseOutCubic(k);
            scale = Mathf.Lerp(1.15f, 0.85f, k);
        }
        else
        {
            SetIntensity(baseGlow, glowColor, 0f);
            return;
        }

        float s = (pillarWidth * 1.9f * scale) / (GlowS / PPU);
        baseGlow.transform.localScale = new Vector3(s, s * 0.75f, 1f);
        SetIntensity(baseGlow, glowColor, intensity);
    }

    private void UpdateSparks(float t)
    {
        if (sparks == null) return;

        for (int i = 0; i < sparks.Length; i++)
        {
            SpriteRenderer s = sparks[i];
            Spark d = sparkData[i];

            float local = t - d.delay;
            if (local < 0f || local >= sparkLifetime)
            {
                SetIntensity(s, sparkColor, 0f);
                continue;
            }

            float k = local / sparkLifetime;
            float y = EaseOutCubic(k) * sparkRiseHeight * d.speed;
            float x = d.angle * sparkSpread + Mathf.Sin(k * 6.2831853f + d.wobble) * 0.12f;

            s.transform.localPosition = new Vector3(x, y, 0f);

            float scale = (d.size * (1f - k * 0.4f)) / (SparkS / PPU);
            s.transform.localScale = new Vector3(scale, scale, 1f);

            // 처음엔 밝게 떴다가 위로 갈수록 사그라듦
            float intensity = Mathf.Min(1f, k * 6f) * (1f - EaseOutCubic(k));
            SetIntensity(s, sparkColor, intensity);
        }
    }

    private void UpdateText(float t)
    {
        if (mainLabel == null) return;   // 폰트 애셋을 못 찾아 라벨이 안 만들어진 경우

        float local = t - textAppearDelay;
        if (local < 0f)
        {
            SetTextAlpha(0f);
            return;
        }

        float scale, intensity, rise;

        if (local < textPopTime)
        {
            float k = local / textPopTime;
            // EaseOutBack 은 1을 살짝 넘겨 튀는 맛을 내므로 클램프되지 않는 Lerp 사용
            scale     = Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(k));
            intensity = Mathf.Min(1f, k * 3f);
            rise      = 0f;
        }
        else if (local < textPopTime + textHoldTime)
        {
            float k = (local - textPopTime) / textHoldTime;
            scale     = 1f + k * 0.03f;               // 아주 미세하게 커짐
            intensity = 1f;
            rise      = k * textRise * 0.35f;
        }
        else
        {
            float k = (local - textPopTime - textHoldTime) / textFadeTime;
            if (k >= 1f) { SetTextAlpha(0f); return; }
            scale     = 1.03f + k * 0.05f;
            intensity = 1f - k;
            rise      = textRise * (0.35f + 0.65f * EaseOutCubic(k));
        }

        Transform tr = mainLabel.transform.parent;   // 텍스트 묶음 루트
        tr.localPosition = new Vector3(0f, textOffsetY + rise, 0f);
        tr.localScale    = new Vector3(scale, scale, 1f);

        SetTextAlpha(intensity);
    }

    // ── 유틸 ─────────────────────────────────────

    private void SetIntensity(SpriteRenderer r, Color hdr, float k)
    {
        if (r == null) return;

        if (k <= 0f)
        {
            if (r.enabled) r.enabled = false;
            return;
        }

        if (!r.enabled) r.enabled = true;
        // MakeSprite 에서 렌더러마다 고유 머티리얼을 이미 넣었으므로 sharedMaterial 로 충분
        r.sharedMaterial.SetColor(ShaderColorId, new Color(hdr.r * k, hdr.g * k, hdr.b * k, hdr.a * k));
    }

    private void SetTextAlpha(float k)
    {
        SetTextAlpha(mainLabel, k);
        SetTextAlpha(subLabel, k);
    }

    private void SetTextAlpha(TextMeshPro label, float k)
    {
        if (label == null) return;

        if (k <= 0f)
        {
            if (label.enabled) label.enabled = false;
            return;
        }

        if (!label.enabled) label.enabled = true;

        float boost = Mathf.Lerp(1f, textHdrBoost, k);
        Color face = new Color(
            textFaceColor.r * boost,
            textFaceColor.g * boost,
            textFaceColor.b * boost,
            Mathf.Clamp01(k));

        label.fontMaterial.SetColor(ShaderUtilities.ID_FaceColor, face);
    }

    private void RandomizeSparks()
    {
        if (sparkData == null) return;

        for (int i = 0; i < sparkData.Length; i++)
        {
            sparkData[i] = new Spark
            {
                angle  = Random.Range(-1f, 1f),
                speed  = Random.Range(0.65f, 1.15f),
                delay  = Random.Range(0f, 0.35f),
                size   = Random.Range(0.10f, 0.24f),
                wobble = Random.Range(0f, 6.2831853f)
            };
        }
    }

    private void SetupFlashQuad()
    {
        if (flash == null) return;

        Camera cam = Camera.main;
        if (!useScreenFlash || cam == null || !cam.orthographic)
        {
            flash.enabled = false;
            return;
        }

        // 화면 전체를 덮도록 카메라 앞에 배치
        Transform ft = flash.transform;
        ft.SetParent(cam.transform, false);
        ft.localPosition = new Vector3(0f, 0f, Mathf.Max(0.2f, cam.nearClipPlane + 0.1f));
        ft.localRotation = Quaternion.identity;

        float h = cam.orthographicSize * 2.2f;
        float w = h * cam.aspect;
        ft.localScale = new Vector3(w / (FlashS / PPU), h / (FlashS / PPU), 1f);
    }

    private static readonly int ShaderColorId = Shader.PropertyToID("_Color");

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
    private static float EaseOutQuint(float x) => 1f - Mathf.Pow(1f - x, 5f);
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float p = x - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }

    // ─────────────────────────────────────────────
    // 오브젝트 생성 (최초 1회)
    // ─────────────────────────────────────────────
    private const int BeamW = 64, BeamH = 256, RingS = 256, GlowS = 128, SparkS = 32, FlashS = 8;

    private void EnsureBuilt()
    {
        if (built) return;

        Shader shader = Shader.Find(AdditiveShader);
        if (shader == null)
        {
            Debug.LogError(
                $"[LevelUpEffect] '{AdditiveShader}' 셰이더를 찾을 수 없습니다. " +
                "Project Settings > Graphics > Always Included Shaders 에 추가하세요.", this);
            return;
        }

        GameObject rootGo = new GameObject("LevelUpEffect_Root");
        rootGo.transform.SetParent(null);
        root = rootGo.transform;

        baseGlow = MakeSprite("Glow",   root, GlowSprite(),  shader, sortingOrder);
        ring1    = MakeSprite("Ring1",  root, RingSprite(),  shader, sortingOrder + 1);
        ring2    = MakeSprite("Ring2",  root, RingSprite(),  shader, sortingOrder + 1);
        pillar   = MakeSprite("Pillar", root, BeamSprite(),  shader, sortingOrder + 2);
        flash    = MakeSprite("Flash",  root, FlashSprite(), shader, sortingOrder + 50);

        sparks     = new SpriteRenderer[sparkCount];
        sparkData  = new Spark[sparkCount];
        Sprite sparkSprite = SparkSprite();
        for (int i = 0; i < sparkCount; i++)
            sparks[i] = MakeSprite($"Spark{i}", root, sparkSprite, shader, sortingOrder + 3);

        BuildLabels();

        rootGo.SetActive(false);
        built = true;
    }

    private SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Shader shader, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.material = new Material(shader) { name = $"{name}_AdditiveHDR" };
        sr.sortingOrder = order;
        if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;
        sr.enabled = false;
        return sr;
    }

    private void BuildLabels()
    {
        TMP_FontAsset font = fontAsset != null ? fontAsset : TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogError("[LevelUpEffect] TMP 폰트 애셋이 없습니다. Window > TextMeshPro > Import TMP Essential Resources 를 실행하거나 fontAsset 을 지정하세요.", this);
            return;
        }

        GameObject holder = new GameObject("TextRoot");
        holder.transform.SetParent(root, false);

        mainLabel = MakeLabel("Main", holder.transform, font, mainFontSize, sortingOrder + 10);
        mainLabel.transform.localPosition = Vector3.zero;

        if (!string.IsNullOrEmpty(subTextFormat))
        {
            subLabel = MakeLabel("Sub", holder.transform, font, subFontSize, sortingOrder + 10);
            subLabel.transform.localPosition = new Vector3(0f, -mainFontSize * 0.62f, 0f);
        }
    }

    private TextMeshPro MakeLabel(string name, Transform parent, TMP_FontAsset font, float size, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.font      = font;
        tmp.fontSize  = size;
        tmp.alignment = TextAlignmentOptions.Center;
        // 색은 전적으로 _FaceColor 로 제어 (버텍스 컬러와 곱해지면 이중 착색됨)
        tmp.color     = Color.white;

        RectTransform rt = tmp.rectTransform;
        rt.sizeDelta = new Vector2(20f, size * 1.6f);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sortingOrder = order;
        if (!string.IsNullOrEmpty(sortingLayerName)) mr.sortingLayerName = sortingLayerName;

        tmp.enabled = false;
        return tmp;
    }

    // ── 절차적 텍스처 ────────────────────────────

    private static Sprite _beam, _ring, _glow, _spark, _flash;

    /// <summary>세로로 뻗는 광주. 피벗을 아래쪽에 둬서 위로 자라나게 함.</summary>
    private static Sprite BeamSprite()
    {
        if (_beam != null) return _beam;

        Texture2D tex = NewTexture(BeamW, BeamH, "LU_Beam");
        Color32[] px = new Color32[BeamW * BeamH];
        float cx = (BeamW - 1) * 0.5f;

        for (int y = 0; y < BeamH; y++)
        {
            float v = y / (float)(BeamH - 1);

            // 아래쪽은 진하고 위로 갈수록 옅어지며, 맨 아래도 살짝 페이드
            float vertical = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.06f))
                           * Mathf.Pow(1f - v, 1.4f);

            for (int x = 0; x < BeamW; x++)
            {
                float dx = (x - cx) / cx;                  // -1 ~ 1
                float horizontal = Mathf.Exp(-dx * dx * 5.5f);

                // 중심에 얇은 코어를 더해 심지가 보이게
                float core = Mathf.Exp(-dx * dx * 55f) * 0.85f;

                float a = Mathf.Clamp01((horizontal * 0.75f + core) * vertical);
                px[y * BeamW + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        _beam = Sprite.Create(tex, new Rect(0, 0, BeamW, BeamH), new Vector2(0.5f, 0f), PPU);
        _beam.name = "LU_Beam";
        return _beam;
    }

    /// <summary>부드러운 가장자리의 링(도넛).</summary>
    private static Sprite RingSprite()
    {
        if (_ring != null) return _ring;

        Texture2D tex = NewTexture(RingS, RingS, "LU_Ring");
        Color32[] px = new Color32[RingS * RingS];
        float c = (RingS - 1) * 0.5f;

        const float radius = 0.40f;    // 정규화 반지름
        const float sigma  = 0.055f;

        for (int y = 0; y < RingS; y++)
        {
            for (int x = 0; x < RingS; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                float e = (d - radius) / sigma;
                float a = Mathf.Exp(-e * e * 0.5f);
                px[y * RingS + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        _ring = Sprite.Create(tex, new Rect(0, 0, RingS, RingS), new Vector2(0.5f, 0.5f), PPU);
        _ring.name = "LU_Ring";
        return _ring;
    }

    /// <summary>발밑에 깔리는 부드러운 원형 광원.</summary>
    private static Sprite GlowSprite()
    {
        if (_glow != null) return _glow;

        Texture2D tex = NewTexture(GlowS, GlowS, "LU_Glow");
        Color32[] px = new Color32[GlowS * GlowS];
        float c = (GlowS - 1) * 0.5f;

        for (int y = 0; y < GlowS; y++)
        {
            for (int x = 0; x < GlowS; x++)
            {
                float d = Mathf.Clamp01(Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c);
                float a = Mathf.Pow(1f - d, 2.6f);
                px[y * GlowS + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        _glow = Sprite.Create(tex, new Rect(0, 0, GlowS, GlowS), new Vector2(0.5f, 0.5f), PPU);
        _glow.name = "LU_Glow";
        return _glow;
    }

    /// <summary>위로 올라가는 작은 빛 입자. 세로로 살짝 긴 마름모꼴.</summary>
    private static Sprite SparkSprite()
    {
        if (_spark != null) return _spark;

        Texture2D tex = NewTexture(SparkS, SparkS, "LU_Spark");
        Color32[] px = new Color32[SparkS * SparkS];
        float c = (SparkS - 1) * 0.5f;

        for (int y = 0; y < SparkS; y++)
        {
            for (int x = 0; x < SparkS; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;

                float d = Mathf.Sqrt(dx * dx * 2.2f + dy * dy);   // 세로로 늘린 타원
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f);
                px[y * SparkS + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        _spark = Sprite.Create(tex, new Rect(0, 0, SparkS, SparkS), new Vector2(0.5f, 0.5f), PPU);
        _spark.name = "LU_Spark";
        return _spark;
    }

    private static Sprite FlashSprite()
    {
        if (_flash != null) return _flash;

        Texture2D tex = NewTexture(FlashS, FlashS, "LU_Flash");
        Color32[] px = new Color32[FlashS * FlashS];
        for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);

        tex.SetPixels32(px);
        tex.Apply();

        _flash = Sprite.Create(tex, new Rect(0, 0, FlashS, FlashS), new Vector2(0.5f, 0.5f), PPU);
        _flash.name = "LU_Flash";
        return _flash;
    }

    private static Texture2D NewTexture(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode   = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            name       = name
        };
    }

#if UNITY_EDITOR
    [ContextMenu("테스트 재생 (Lv. 10)")]
    private void TestPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LevelUpEffect] 플레이 모드에서만 테스트할 수 있습니다.");
            return;
        }
        Play(10);
    }
#endif
}
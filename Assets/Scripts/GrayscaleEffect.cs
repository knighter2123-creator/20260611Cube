using UnityEngine;

/// <summary>
/// 대상(몬스터 등)의 SpriteRenderer 머티리얼을 흑백 머티리얼로 교체했다가
/// 지속시간이 끝나면 원본으로 복구합니다.
///
/// 사용법 : GrayscaleEffect.Apply(enemy.gameObject, 2f);
/// 컴포넌트를 미리 붙여둘 필요 없음 (없으면 자동으로 추가됨).
/// </summary>
[DisallowMultipleComponent]
public class GrayscaleEffect : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // 공용 흑백 머티리얼 (모든 대상이 공유 → 배칭 유지)
    // ─────────────────────────────────────────────
    private const string ShaderName   = "Custom/2D/SpriteGrayscale";
    private const string ResourceName = "Mat_Grayscale";   // Assets/Resources/Mat_Grayscale.mat

    private static Material _grayMaterial;
    private static bool     _warned;

    private static Material GrayMaterial
    {
        get
        {
            if (_grayMaterial != null) return _grayMaterial;

            // 1순위 : Resources 폴더의 머티리얼 (인스펙터에서 색감 조절 가능)
            _grayMaterial = Resources.Load<Material>(ResourceName);

            // 2순위 : 셰이더로 런타임 생성 (셰이더가 Always Included Shaders 에 있어야 빌드에서 동작)
            if (_grayMaterial == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    _grayMaterial = new Material(shader) { name = "Mat_Grayscale (runtime)" };
                }
                else if (!_warned)
                {
                    _warned = true;
                    Debug.LogError($"[GrayscaleEffect] '{ShaderName}' 셰이더를 찾을 수 없습니다. " +
                                   $"Assets/Resources/{ResourceName}.mat 을 만들거나 " +
                                   "Project Settings > Graphics > Always Included Shaders 에 셰이더를 추가하세요.");
                }
            }
            return _grayMaterial;
        }
    }

    // ─────────────────────────────────────────────
    // 정적 진입점
    // ─────────────────────────────────────────────

    /// <summary>대상을 duration 초 동안 회색으로 만듭니다. (중첩 시 더 긴 쪽으로 연장)</summary>
    public static void Apply(GameObject target, float duration)
    {
        if (target == null || duration <= 0f) return;

        GrayscaleEffect fx = target.GetComponent<GrayscaleEffect>();
        if (fx == null) fx = target.AddComponent<GrayscaleEffect>();
        fx.Begin(duration);
    }

    /// <summary>즉시 원본 색으로 되돌립니다. (스턴 조기 해제 / 사망 처리 등)</summary>
    public static void Clear(GameObject target)
    {
        if (target == null) return;

        GrayscaleEffect fx = target.GetComponent<GrayscaleEffect>();
        if (fx != null) fx.Stop();
    }

    // ─────────────────────────────────────────────
    // 인스턴스
    // ─────────────────────────────────────────────
    private SpriteRenderer[] _renderers;
    private Material[]       _originals;
    private bool             _isGray;
    private float            _endTime;

    public bool IsGray => _isGray;

    private void Awake() => CacheRenderers();

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originals = new Material[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originals[i] = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
    }

    public void Begin(float duration)
    {
        if (_renderers == null || _renderers.Length == 0) CacheRenderers();

        float end = Time.time + duration;
        if (end > _endTime) _endTime = end;   // 중첩되면 더 긴 시간으로 연장

        SetGray(true);
    }

    public void Stop()
    {
        _endTime = 0f;
        SetGray(false);
    }

    private void Update()
    {
        if (!_isGray) return;
        if (Time.time >= _endTime) SetGray(false);
    }

    private void SetGray(bool on)
    {
        if (_isGray == on) return;
        if (_renderers == null) return;

        Material gray = on ? GrayMaterial : null;
        if (on && gray == null) return;   // 셰이더 못 찾음 → 아무것도 하지 않음

        for (int i = 0; i < _renderers.Length; i++)
        {
            SpriteRenderer sr = _renderers[i];
            if (sr == null) continue;

            if (on)
            {
                _originals[i]   = sr.sharedMaterial;   // 교체 직전 상태를 기억
                sr.sharedMaterial = gray;
            }
            else
            {
                sr.sharedMaterial = _originals[i];
            }
        }

        _isGray = on;
    }

    // 오브젝트 풀링으로 비활성화되거나 파괴될 때 원본 머티리얼을 반드시 복구
    private void OnDisable()
    {
        if (_isGray) SetGray(false);
        _endTime = 0f;
    }
}

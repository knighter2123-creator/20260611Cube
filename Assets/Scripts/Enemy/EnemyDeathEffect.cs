using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 적 사망 연출 — 스프라이트가 녹색으로 확 물들었다가 페이드아웃됩니다.
///
/// Custom/2D/SpriteAddTexColor 셰이더의 _AddAmount / _Alpha 를 애니메이션합니다.
/// 값은 MaterialPropertyBlock 으로 개체별로 넣기 때문에, 여러 마리가 동시에 죽어도
/// 머티리얼 인스턴스가 늘어나지 않습니다.
///
/// 사용법 : deathEffect.Play(ReturnToPool);   // 연출이 끝나면 콜백 호출
/// </summary>
[DisallowMultipleComponent]
public class EnemyDeathEffect : MonoBehaviour
{
    private const string ShaderName   = "Custom/2D/SpriteAddTexColor";
    private const string ResourceName = "Mat_AddTexColor";   // Assets/Resources/Mat_AddTexColor.mat

    // ─────────────────────────────────────────────
    // 공용 머티리얼
    // ─────────────────────────────────────────────
    private static Material _deathMaterial;
    private static bool     _warned;

    private static Material DeathMaterial
    {
        get
        {
            if (_deathMaterial != null) return _deathMaterial;

            _deathMaterial = Resources.Load<Material>(ResourceName);

            if (_deathMaterial == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader != null)
                {
                    _deathMaterial = new Material(shader) { name = "Mat_AddTexColor (runtime)" };
                }
                else if (!_warned)
                {
                    _warned = true;
                    Debug.LogError($"[EnemyDeathEffect] '{ShaderName}' 셰이더를 찾을 수 없습니다. " +
                                   $"Assets/Resources/{ResourceName}.mat 을 만들거나 " +
                                   "Project Settings > Graphics > Always Included Shaders 에 셰이더를 추가하세요.");
                }
            }
            return _deathMaterial;
        }
    }

    // ─────────────────────────────────────────────
    [Header("색")]
    [Tooltip("사망 시 더해질 색. Intensity 를 1 이상으로 올리면 블룸에도 반응합니다")]
    [ColorUsage(true, true)]
    public Color deathColor = new Color(0.15f, 0.85f, 0.30f, 1f);

    [Header("타이밍")]
    [Tooltip("녹색이 확 드는 시간")]
    public float tintInTime = 0.07f;
    [Tooltip("녹색 상태로 머무는 시간")]
    public float holdTime = 0.06f;
    [Tooltip("투명해지며 사라지는 시간")]
    public float fadeOutTime = 0.28f;

    /// <summary>연출 전체 길이(초)</summary>
    public float TotalDuration => tintInTime + holdTime + fadeOutTime;

    /// <summary>지금 사망 연출이 재생 중인가</summary>
    public bool IsPlaying { get; private set; }

    // ─────────────────────────────────────────────
    private static readonly int AddColorId  = Shader.PropertyToID("_AddColor");
    private static readonly int AddAmountId = Shader.PropertyToID("_AddAmount");
    private static readonly int AlphaId     = Shader.PropertyToID("_Alpha");

    private SpriteRenderer[]     renderers;
    private Material[]           originals;
    private MaterialPropertyBlock mpb;
    private bool   swapped;
    private Action pending;

    // ─────────────────────────────────────────────
    public static EnemyDeathEffect GetOrAdd(GameObject go)
    {
        if (go == null) return null;

        EnemyDeathEffect fx = go.GetComponent<EnemyDeathEffect>();
        if (fx == null) fx = go.AddComponent<EnemyDeathEffect>();
        return fx;
    }

    private void Awake()
    {
        mpb = new MaterialPropertyBlock();
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originals = new Material[renderers.Length];
    }

    /// <summary>사망 연출을 재생하고, 끝나면 onComplete 를 호출합니다.</summary>
    public void Play(Action onComplete)
    {
        if (renderers == null || renderers.Length == 0) CacheRenderers();

        // 비활성 상태면 코루틴이 돌지 않으므로 즉시 콜백
        if (!isActiveAndEnabled || DeathMaterial == null)
        {
            onComplete?.Invoke();
            return;
        }

        pending = onComplete;

        StopAllCoroutines();
        StartCoroutine(Run());
    }

    /// <summary>풀에서 다시 꺼낼 때 호출. 머티리얼과 상태를 원래대로 되돌립니다.</summary>
    public void ResetState()
    {
        StopAllCoroutines();
        IsPlaying = false;
        pending   = null;
        Restore();
    }

    // ─────────────────────────────────────────────
    private IEnumerator Run()
    {
        IsPlaying = true;

        SwapToDeathMaterial();
        SetValues(0f, 1f);

        // ① 녹색이 확 든다
        float t = 0f;
        while (t < tintInTime)
        {
            t += Time.deltaTime;
            SetValues(Mathf.Clamp01(t / tintInTime), 1f);
            yield return null;
        }
        SetValues(1f, 1f);

        // ② 잠깐 유지
        if (holdTime > 0f)
        {
            float h = 0f;
            while (h < holdTime) { h += Time.deltaTime; yield return null; }
        }

        // ③ 페이드아웃
        t = 0f;
        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            SetValues(1f, 1f - Mathf.Clamp01(t / fadeOutTime));
            yield return null;
        }
        SetValues(1f, 0f);

        Finish();
    }

    private void Finish()
    {
        IsPlaying = false;
        Restore();

        Action cb = pending;
        pending = null;
        cb?.Invoke();
    }

    private void SwapToDeathMaterial()
    {
        if (swapped) return;

        Material mat = DeathMaterial;
        if (mat == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            originals[i]     = sr.sharedMaterial;
            sr.sharedMaterial = mat;
        }

        swapped = true;
    }

    private void Restore()
    {
        if (renderers == null) return;

        // 값을 원래대로 되돌린 뒤 머티리얼 복구
        SetValues(0f, 1f);

        if (!swapped) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            sr.sharedMaterial = originals[i];
        }

        swapped = false;
    }

    private void SetValues(float addAmount, float alpha)
    {
        if (renderers == null) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;

            // Get 으로 시작해야 SpriteRenderer 가 쓰는 기존 per-renderer 데이터가 보존됩니다
            sr.GetPropertyBlock(mpb);
            mpb.SetColor(AddColorId, deathColor);
            mpb.SetFloat(AddAmountId, addAmount);
            mpb.SetFloat(AlphaId, alpha);
            sr.SetPropertyBlock(mpb);
        }
    }

    // 연출 도중 강제로 비활성화되면(스테이지 초기화 등) 머티리얼을 복구하고 콜백을 흘려보냅니다.
    // Enemy.ReturnToPool 쪽에 이중 반환 가드가 있어 안전합니다.
    private void OnDisable()
    {
        if (!IsPlaying && !swapped) return;

        IsPlaying = false;
        Restore();

        Action cb = pending;
        pending = null;
        cb?.Invoke();
    }
}
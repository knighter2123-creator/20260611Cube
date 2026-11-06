using System;
using System.Collections;
using UnityEngine;

/// <summary>사망 연출 — 사망 프레임을 넘기고, 프레임이 없으면 색 알파만 페이드(머티리얼 교체 없음 → 배칭 유지·첫 그리기 멈칫 없음), Play(콜백).</summary>
[DisallowMultipleComponent]
public class EnemyDeathAnimator : MonoBehaviour
{
    [Tooltip("순서대로 재생할 사망 프레임 (Assets/Sprites/Enemy/Death 의 시트를 슬라이스한 스프라이트)")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("프레임별 표시 시간(초). 프레임 수보다 짧으면 마지막 값을 계속 씁니다")]
    [SerializeField] private float[] frameDurations = { 0.08f, 0.10f, 0.11f, 0.12f };

    [Tooltip("사망 프레임이 없을 때 페이드아웃 시간(초)")]
    [SerializeField] private float fallbackFadeTime = 0.3f;

    /// <summary>지금 사망 연출이 재생 중인가</summary>
    public bool IsPlaying { get; private set; }

    private SpriteRenderer sr;
    private Sprite originalSprite;
    private Color  originalColor;
    private Action pending;

    // ─────────────────────────────────────────────
    public static EnemyDeathAnimator GetOrAdd(GameObject go)
    {
        if (go == null) return null;

        EnemyDeathAnimator anim = go.GetComponent<EnemyDeathAnimator>();
        if (anim == null) anim = go.AddComponent<EnemyDeathAnimator>();
        return anim;
    }

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>사망 연출을 재생하고, 끝나면 onComplete 를 호출합니다.</summary>
    public void Play(Action onComplete)
    {
        // 비활성 상태면 코루틴이 돌지 않으므로 즉시 콜백
        if (!isActiveAndEnabled || sr == null)
        {
            onComplete?.Invoke();
            return;
        }

        pending = onComplete;

        StopAllCoroutines();
        StartCoroutine(Run());
    }

    /// <summary>풀에서 다시 꺼낼 때 호출. 연출이 남아 있으면 끊고 원래 모습으로 되돌립니다.</summary>
    public void ResetState()
    {
        StopAllCoroutines();
        pending = null;
        if (IsPlaying) Restore();
    }

    // ─────────────────────────────────────────────
    private IEnumerator Run()
    {
        IsPlaying      = true;
        originalSprite = sr.sprite;
        originalColor  = sr.color;

        if (frames != null && frames.Length > 0)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                sr.sprite = frames[i];
                yield return Wait(FrameDuration(i));
            }
        }
        else
        {
            float t = 0f;
            while (t < fallbackFadeTime)
            {
                t += Time.deltaTime;
                Color c = originalColor;
                c.a *= 1f - Mathf.Clamp01(t / fallbackFadeTime);
                sr.color = c;
                yield return null;
            }
        }

        // 풀로 돌아가기 전에 원래 모습으로 — 다음에 꺼낼 때 사망 프레임이 한 번 비치지 않도록
        Restore();

        Action cb = pending;
        pending = null;
        cb?.Invoke();
    }

    private float FrameDuration(int index)
    {
        if (frameDurations == null || frameDurations.Length == 0) return 0.1f;
        return frameDurations[Mathf.Min(index, frameDurations.Length - 1)];
    }

    // WaitForSeconds 는 매번 할당되므로 deltaTime 누적으로 기다립니다 (게임 배속도 그대로 따라감).
    private static IEnumerator Wait(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void Restore()
    {
        IsPlaying = false;
        if (sr == null) return;
        sr.sprite = originalSprite;
        sr.color  = originalColor;
    }

    // 연출 중 비활성화(사실상 씬 언로드)되면 모습만 복구하고 콜백 버림 — 꺼지는 중 풀 반환은 계층 변경 에러
    private void OnDisable()
    {
        if (!IsPlaying) return;
        pending = null;
        Restore();
    }
}

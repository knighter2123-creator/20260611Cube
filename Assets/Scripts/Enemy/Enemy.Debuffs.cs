using System.Collections;
using UnityEngine;

/// <summary>
/// 스킬 디버프 로직 (둔화 / 방어력 감소 / 독 / 스턴)
/// 속도는 TargetMove의 배율 API를 통해서만 조작합니다 — 직접 대입 금지.
///
/// 같은 디버프가 다시 걸리면 이전 코루틴을 멈추고 새로 시작합니다(지속시간 갱신, 중첩 없음).
/// </summary>
public partial class Enemy
{
    // ── 상태 필드 ───────────────────────────────
    private float _armorBreakMultiplier = 1f;   // 1 = 정상, 0.6 = 방어력 40% 감소

    private Coroutine _slowCoroutine;
    private Coroutine _armorBreakCoroutine;
    private Coroutine _dotCoroutine;
    private Coroutine _stunCoroutine;

    // 컴포넌트 캐시 (Enemy.Awake에서 채움)
    private SpriteRenderer _sr;
    private TargetMove     _move;
    private Color          _originalColor;

    private static readonly Color SlowColor = new Color(0.3f, 0.6f, 1f);

    /// <summary>Enemy.Awake에서 1회 호출 — 컴포넌트 캐싱 및 원본 색 보관</summary>
    private void CacheDebuffRefs()
    {
        _sr   = GetComponent<SpriteRenderer>();
        _move = GetComponent<TargetMove>();
        if (_sr != null) _originalColor = _sr.color;
    }

    /// <summary>풀에서 꺼낼 때 호출 — 모든 디버프를 완전 초기화</summary>
    public void ResetDebuffs()
    {
        StopAllDebuffs();

        _armorBreakMultiplier = 1f;
        SetTint(_originalColor);

        _move?.ResetForSpawn();
    }

    // ──────────────────────────────────────────
    //  둔화
    // ──────────────────────────────────────────
    public void ApplySlow(float rate, float duration)
    {
        if (_move == null) return;
        RestartDebuff(ref _slowCoroutine, SlowRoutine(rate, duration));
    }

    private IEnumerator SlowRoutine(float rate, float duration)
    {
        _move.SetSlowMultiplier(1f - rate);   // 누적이 아니라 대입 — 겹쳐도 안전
        SetTint(SlowColor);

        yield return new WaitForSeconds(duration);

        _move.ClearSlow();
        SetTint(_originalColor);
        _slowCoroutine = null;
    }

    // ──────────────────────────────────────────
    //  방어력 감소
    // ──────────────────────────────────────────
    public void ApplyArmorBreak(float rate, float duration)
    {
        RestartDebuff(ref _armorBreakCoroutine, ArmorBreakRoutine(rate, duration));
    }

    private IEnumerator ArmorBreakRoutine(float rate, float duration)
    {
        _armorBreakMultiplier = Mathf.Max(1f - rate, 0.01f);   // 방어력이 음수가 되지 않도록 하한

        yield return new WaitForSeconds(duration);

        _armorBreakMultiplier = 1f;
        _armorBreakCoroutine  = null;
    }

    // ──────────────────────────────────────────
    //  독 (DoT)
    // ──────────────────────────────────────────
    public void ApplyDot(float dotDamage, float dotInterval, float duration)
    {
        RestartDebuff(ref _dotCoroutine, DotRoutine(dotDamage, dotInterval, duration));
    }

    private IEnumerator DotRoutine(float dotDamage, float dotInterval, float duration)
    {
        var   wait    = new WaitForSeconds(dotInterval);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return wait;
            elapsed += dotInterval;
            if (isDead) yield break;
            TakeDamage(dotDamage);
        }

        _dotCoroutine = null;
    }

    // ──────────────────────────────────────────
    //  스턴
    // ──────────────────────────────────────────
    public void ApplyStun(float duration)
    {
        if (_move == null) return;
        RestartDebuff(ref _stunCoroutine, StunRoutine(duration));
    }

    private IEnumerator StunRoutine(float duration)
    {
        _move.SetStunned(true);   // 속도값을 저장/복원하지 않음 — 플래그만 토글

        yield return new WaitForSeconds(duration);

        _move.SetStunned(false);
        _stunCoroutine = null;
    }

    // ──────────────────────────────────────────
    //  유틸
    // ──────────────────────────────────────────

    /// <summary>살아 있을 때만, 같은 종류의 이전 디버프를 끊고 새로 시작합니다.</summary>
    private void RestartDebuff(ref Coroutine slot, IEnumerator routine)
    {
        if (isDead) return;

        StopCoroutineIfRunning(ref slot);
        slot = StartCoroutine(routine);
    }

    private void StopCoroutineIfRunning(ref Coroutine co)
    {
        if (co == null) return;
        StopCoroutine(co);
        co = null;
    }

    private void SetTint(Color color)
    {
        if (_sr != null) _sr.color = color;
    }

    /// <summary>Die() / ResetDebuffs()에서 호출 — 모든 디버프 코루틴 일괄 정리</summary>
    protected void StopAllDebuffs()
    {
        StopCoroutineIfRunning(ref _slowCoroutine);
        StopCoroutineIfRunning(ref _armorBreakCoroutine);
        StopCoroutineIfRunning(ref _dotCoroutine);
        StopCoroutineIfRunning(ref _stunCoroutine);
    }
}

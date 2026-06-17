using System.Collections;
using UnityEngine;

/// <summary>
/// 스킬 디버프 관련 로직 (둔화 / 방어력 감소 / 독 / 스턴)
/// Enemy.cs와 partial class로 연결됩니다.
/// </summary>
public partial class Enemy
{
    // ── 상태 필드 ───────────────────────────────
    private float _armorBreakMultiplier = 1f;   // 1 = 정상, 0.6 = 방어력 40% 감소

    private Coroutine _slowCoroutine;
    private Coroutine _armorBreakCoroutine;
    private Coroutine _dotCoroutine;
    private Coroutine _stunCoroutine;

    // ──────────────────────────────────────────
    //  둔화
    // ──────────────────────────────────────────
    public void ApplySlow(float slowRate, float duration)
    {
        if (isDead) return;

        TargetMove moveScript = GetComponent<TargetMove>();
        if (moveScript == null) return;

        StopCoroutineIfRunning(ref _slowCoroutine);
        _slowCoroutine = StartCoroutine(SlowCoroutine(moveScript, slowRate, duration));
    }

    private IEnumerator SlowCoroutine(TargetMove moveScript, float slowRate, float duration)
    {
        float originalSpeed = moveScript.GetSpeed();
        moveScript.SetSpeed(originalSpeed * (1f - slowRate));
        Debug.Log($"[Debuff] 둔화 적용 {slowRate * 100}% ({duration}s)");

        yield return new WaitForSeconds(duration);

        moveScript.SetSpeed(originalSpeed);
        _slowCoroutine = null;
        Debug.Log("[Debuff] 둔화 해제");
    }

    // ──────────────────────────────────────────
    //  방어력 감소
    // ──────────────────────────────────────────
    public void ApplyArmorBreak(float rate, float duration)
    {
        if (isDead) return;

        StopCoroutineIfRunning(ref _armorBreakCoroutine);
        _armorBreakCoroutine = StartCoroutine(ArmorBreakRoutine(rate, duration));
    }

    private IEnumerator ArmorBreakRoutine(float rate, float duration)
    {
        _armorBreakMultiplier = 1f - rate;
        Debug.Log($"[Debuff] 방어력 {rate * 100}% 감소 ({duration}s)");

        yield return new WaitForSeconds(duration);

        _armorBreakMultiplier = 1f;
        Debug.Log("[Debuff] 방어력 감소 해제");
    }

    // ──────────────────────────────────────────
    //  독 (DoT)
    // ──────────────────────────────────────────
    public void ApplyDot(float dotDamage, float dotInterval, float duration)
    {
        if (isDead) return;

        StopCoroutineIfRunning(ref _dotCoroutine);
        _dotCoroutine = StartCoroutine(DotRoutine(dotDamage, dotInterval, duration));
    }

    private IEnumerator DotRoutine(float dotDamage, float dotInterval, float duration)
    {
        float elapsed = 0f;
        Debug.Log($"[Debuff] 독 {dotDamage}/틱, {dotInterval}s 간격 ({duration}s)");

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(dotInterval);
            elapsed += dotInterval;
            if (isDead) yield break;
            TakeDamage(dotDamage);
        }
    }

    // ──────────────────────────────────────────
    //  스턴
    // ──────────────────────────────────────────
    public void ApplyStun(float duration)
    {
        if (isDead) return;

        TargetMove moveScript = GetComponent<TargetMove>();
        if (moveScript == null) return;

        StopCoroutineIfRunning(ref _stunCoroutine);
        _stunCoroutine = StartCoroutine(StunRoutine(moveScript, duration));
    }

    private IEnumerator StunRoutine(TargetMove moveScript, float duration)
    {
        float originalSpeed = moveScript.GetSpeed();
        moveScript.SetSpeed(0f);
        Debug.Log($"[Debuff] 스턴 ({duration}s)");

        yield return new WaitForSeconds(duration);

        moveScript.SetSpeed(originalSpeed);
        _stunCoroutine = null;
        Debug.Log("[Debuff] 스턴 해제");
    }

    // ──────────────────────────────────────────
    //  유틸
    // ──────────────────────────────────────────
    private void StopCoroutineIfRunning(ref Coroutine co)
    {
        if (co == null) return;
        StopCoroutine(co);
        co = null;
    }

    /// <summary>Die() 에서 호출 — 모든 디버프 코루틴 일괄 정리</summary>
    protected void StopAllDebuffs()
    {
        StopCoroutineIfRunning(ref _slowCoroutine);
        StopCoroutineIfRunning(ref _armorBreakCoroutine);
        StopCoroutineIfRunning(ref _dotCoroutine);
        StopCoroutineIfRunning(ref _stunCoroutine);
    }
}
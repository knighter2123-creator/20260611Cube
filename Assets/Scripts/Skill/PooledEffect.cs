using UnityEngine;

/// <summary>
/// 풀에서 꺼낸 일회성 이펙트를 지정한 시간 뒤에 ObjectPoolManager 로 돌려보냅니다.
///
/// ★ [프레임 드랍 수정] 스킬 이펙트는 시전마다 Instantiate → Destroy(fx, 시간) 했습니다.
///   동료 6명이 쿨다운마다 쏘면 초당 수 개씩 만들고 버려서 생성 비용과 GC 가 계속 쌓였습니다.
///   이제 꺼서 보관했다가 다시 씁니다. (Animator 는 다시 켜질 때 처음부터 재생됩니다)
///
/// 코루틴 대신 Update 로 시간을 재는 이유: 시전마다 코루틴 객체(할당)가 생기지 않게.
/// 대기 중(꺼져 있음)에는 Update 가 돌지 않으므로 비용이 없습니다.
/// </summary>
[DisallowMultipleComponent]
public class PooledEffect : MonoBehaviour
{
    private float returnAt;

    /// <summary>fx 를 delay 초(게임 시간) 뒤 풀로 돌려보내도록 예약합니다. fx 는 이미 켜져 있어야 합니다.</summary>
    public static void ReturnAfter(GameObject fx, float delay)
    {
        if (fx == null) return;

        if (!fx.TryGetComponent(out PooledEffect pe))
            pe = fx.AddComponent<PooledEffect>();

        // Destroy(fx, delay) 와 같은 기준 — 게임 시간(timeScale 영향 받음)
        pe.returnAt = Time.time + Mathf.Max(0f, delay);
    }

    private void Update()
    {
        if (Time.time < returnAt) return;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.Return(gameObject);
        else
            Destroy(gameObject);
    }
}

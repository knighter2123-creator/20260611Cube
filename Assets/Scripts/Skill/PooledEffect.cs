using UnityEngine;

/// <summary>풀 출신 일회성 이펙트를 지정 시간 뒤 풀로 반환(시전마다 생성·파괴의 GC 방지) — 코루틴 할당을 피해 Update 로 시간을 잼, 꺼져 있으면 비용 없음.</summary>
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

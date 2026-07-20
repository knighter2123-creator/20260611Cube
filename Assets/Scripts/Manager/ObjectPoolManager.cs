using System.Collections.Generic;
using UnityEngine;

public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [Header("Bullet Pool 설정")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int initialPoolSize = 20;

    private Queue<GameObject> pool = new Queue<GameObject>();

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitPool();
    }

    // ──────────────────────────────────────────────
    //  풀 초기화
    // ──────────────────────────────────────────────
    private void InitPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = CreateBullet();
            pool.Enqueue(obj);
        }
    }

    private GameObject CreateBullet()
    {
        // 부모 없이 생성 → 월드 좌표계 기준으로 위치가 적용됨
        GameObject obj = Instantiate(bulletPrefab);
        obj.SetActive(false);
        return obj;
    }
    
    /// <summary>
    /// 풀에서 Bullet을 꺼내 반환합니다. (비활성 상태)
    /// 호출자가 위치·Init 설정 후 직접 SetActive(true) 해야 합니다.
    /// </summary>
    public GameObject GetBulletInactive()
    {
        if (pool.Count == 0)
            pool.Enqueue(CreateBullet()); // 총알 장전

        return pool.Dequeue();   // 맨 앞의 총알 소모
    }

    /// <summary>사용이 끝난 Bullet을 풀에 반환합니다.</summary>
    public void ReturnBullet(GameObject obj)
    {
        obj.SetActive(false);
        // 부모를 null로 해제 — 씬 루트에 보관해 좌표 오염 방지
        obj.transform.SetParent(null);
        pool.Enqueue(obj);
    }
    // ══════════════════════════════════════════════
    //  프리팹별 범용 풀 (Enemy / Boss 등)
    // ══════════════════════════════════════════════

    // 프리팹 → 대기 중인 인스턴스 큐
    private readonly Dictionary<GameObject, Queue<GameObject>> prefabPools
        = new Dictionary<GameObject, Queue<GameObject>>();

    // 인스턴스 → 원본 프리팹 (반환 시 어느 풀로 돌려보낼지 판단)
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab
        = new Dictionary<GameObject, GameObject>();

    /// <summary>
    /// 프리팹 풀에서 오브젝트를 꺼냅니다. (비활성 상태로 반환)
    /// 호출자가 초기화 후 직접 SetActive(true) 해야 합니다.
    /// </summary>
    public GameObject GetInactive(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null) return null;

        if (!prefabPools.TryGetValue(prefab, out var queue))
            prefabPools[prefab] = queue = new Queue<GameObject>();

        GameObject obj = null;

        // 씬 전환 등으로 파괴된 인스턴스가 섞여 있을 수 있으므로 살아있는 것만 꺼냄
        while (queue.Count > 0)
        {
            obj = queue.Dequeue();
            if (obj != null) break;
            obj = null;
        }

        if (obj == null)
        {
            obj = Instantiate(prefab);
            obj.SetActive(false);
            instanceToPrefab[obj] = prefab;
        }

        obj.transform.SetParent(null);
        obj.transform.SetPositionAndRotation(position, rotation);
        return obj;
    }

    /// <summary>사용이 끝난 오브젝트를 원래 풀로 반환합니다.</summary>
    public void Return(GameObject obj)
    {
        if (obj == null) return;

        obj.SetActive(false);
        obj.transform.SetParent(null);

        // 풀에서 나온 게 아니면(직접 Instantiate된 것) 그냥 파괴
        if (!instanceToPrefab.TryGetValue(obj, out var prefab))
        {
            Destroy(obj);
            return;
        }

        if (!prefabPools.TryGetValue(prefab, out var queue))
            prefabPools[prefab] = queue = new Queue<GameObject>();

        queue.Enqueue(obj);
    }

    /// <summary>미리 생성해 첫 스폰 시 렉을 방지합니다. (선택)</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        if (!prefabPools.TryGetValue(prefab, out var queue))
            prefabPools[prefab] = queue = new Queue<GameObject>();

        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            instanceToPrefab[obj] = prefab;
            queue.Enqueue(obj);
        }
    }
}
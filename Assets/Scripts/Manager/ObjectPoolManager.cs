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

    // ──────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────

    /// <summary>
    /// 풀에서 Bullet을 꺼내 반환합니다. (비활성 상태)
    /// 호출자가 위치·Init 설정 후 직접 SetActive(true) 해야 합니다.
    /// </summary>
    public GameObject GetBulletInactive()
    {
        if (pool.Count == 0)
            pool.Enqueue(CreateBullet());

        return pool.Dequeue();   // SetActive는 호출자 책임
    }

    /// <summary>풀에서 Bullet을 꺼내 즉시 활성화해 반환합니다. (하위 호환용)</summary>
    public GameObject GetBullet()
    {
        GameObject obj = GetBulletInactive();
        obj.SetActive(true);
        return obj;
    }

    /// <summary>사용이 끝난 Bullet을 풀에 반환합니다.</summary>
    public void ReturnBullet(GameObject obj)
    {
        obj.SetActive(false);
        // 부모를 null로 해제 — 씬 루트에 보관해 좌표 오염 방지
        obj.transform.SetParent(null);
        pool.Enqueue(obj);
    }
}
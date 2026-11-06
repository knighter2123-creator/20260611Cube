using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>오브젝트 풀(총알 전용 + 프리팹별 범용): 만들어 두고 껐다 켜며 재사용 — Instantiate/Destroy 와 GC 로 인한 프레임 드랍을 막으려고, 예열은 여러 프레임에 나눠서.</summary>
public class ObjectPoolManager : MonoBehaviour
{
    public static ObjectPoolManager Instance { get; private set; }

    [Header("Bullet Pool 설정")]
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("각성으로 한 번에 여러 발이 나가면 순간 소비량이 늘어납니다. 60 권장.")]
    [SerializeField] private int initialPoolSize = 60;

    [Tooltip("한 프레임에 미리 만들 총알 수 (시작 직후 프레임 드랍 방지)")]
    [SerializeField] private int bulletsPerFrame = 6;

    [Header("시작 예열")]
    [Tooltip("배치된 동료의 스킬 이펙트를 프리팹마다 몇 개 미리 만들지. 0 이면 예열하지 않음.")]
    [SerializeField] private int skillEffectPrewarm = 2;

    private Queue<GameObject> pool = new Queue<GameObject>();

    // ── Unity 생명 주기 ──
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 꺼지면 예열 코루틴이 멈추므로 표시를 비움 (남기면 그 프리팹은 다시 예열 안 됨)
    void OnDisable()
    {
        prewarming.Clear();
        LogUsage();
    }

    // ── 사용량 기록 — 예열 개수를 '최대 동시 사용'에 맞추려고 씬 종료 때 로그 (전투 중 생성 > 0 이면 예열 부족, 만든 수 > 최대 사용이면 줄여도 됨) ──

    private sealed class Usage
    {
        public string Name;
        public int    Created;          // 지금까지 만든 수
        public int    CreatedInBattle;  // 풀이 비어서 꺼내는 순간 만든 수 (= 그 프레임이 멈칫했을 수 있음)
        public int    Peak;             // 동시에 밖에 나가 있던 최대 수
    }

    private readonly Dictionary<GameObject, Usage> usage = new Dictionary<GameObject, Usage>();
    private readonly Usage bulletUsage = new Usage { Name = "총알" };

    private Usage UsageOf(GameObject prefab)
    {
        if (!usage.TryGetValue(prefab, out Usage u)) usage[prefab] = u = new Usage { Name = prefab.name };
        return u;
    }

    /// <summary>꺼낸 직후 — 만든 수에서 창고에 남은 수를 빼면 지금 밖에 나가 있는 수.</summary>
    private static void TrackTake(Usage u, int remainingInQueue)
    {
        int inUse = u.Created - remainingInQueue;
        if (inUse > u.Peak) u.Peak = inUse;
    }

    private void LogUsage()
    {
        if (bulletUsage.Created > 0)
            GameLog.Info($"[ObjectPool] {bulletUsage.Name}: 예열 {initialPoolSize} / 만든 수 {bulletUsage.Created} / 최대 동시 사용 {bulletUsage.Peak} / 전투 중 생성 {bulletUsage.CreatedInBattle}");
        foreach (Usage u in usage.Values)
            GameLog.Info($"[ObjectPool] {u.Name}: 만든 수 {u.Created} / 최대 동시 사용 {u.Peak} / 전투 중 생성 {u.CreatedInBattle}");
    }

    // ── 시작 예열 ──

    /// <summary>'처음 한 번' 비용을 전투 전에 여러 프레임에 나눠 치름: 흑백 셰이더 워밍업, 배치된 동료 스킬 이펙트, 총알 bulletsPerFrame 개씩 — 첫 프레임은 Awake/Start 가 몰리고 배치 복원 뒤에 세야 해서 건너뜀.</summary>
    private IEnumerator Start()
    {
        yield return null;

        StartCoroutine(RenderWarmup.Run(GrayscaleEffect.Preload()));
        PrewarmPlacedSkillEffects();

        int perFrame = Mathf.Max(1, bulletsPerFrame);
        int made = 0;
        while (made < initialPoolSize)
        {
            for (int i = 0; i < perFrame && made < initialPoolSize; i++, made++)
                pool.Enqueue(CreateBullet());
            yield return null;
        }
        bulletPrewarmDone = true;
    }

    private bool bulletPrewarmDone;   // 이후에 만드는 총알은 '전투 중 생성' 으로 셉니다

    /// <summary>지금 배치된 동료들의 스킬 이펙트를 프리팹마다 skillEffectPrewarm 개씩 나눠서 만듭니다.</summary>
    private void PrewarmPlacedSkillEffects()
    {
        CompanionManager companions = CompanionManager.Instance;
        if (companions == null || skillEffectPrewarm <= 0) return;

        foreach (Companion c in companions.GetOwnedCompanions())
        {
            if (c == null || !c.IsPlaced) continue;

            ActiveSkill skill = c.OwnedSkill;
            if (skill != null && skill.effectPrefab != null)
                PrewarmOverFrames(skill.effectPrefab, skillEffectPrewarm, 1);   // 같은 프리팹이 겹치면 PrewarmOverFrames 가 거름
        }
    }

    // ── 총알 풀 ──

    private GameObject CreateBullet()
    {
        // 부모 없이 생성 → 월드 좌표계 기준으로 위치가 적용됨
        GameObject obj = Instantiate(bulletPrefab);
        obj.SetActive(false);
        bulletUsage.Created++;
        return obj;
    }

    /// <summary>풀에서 비활성 총알을 꺼냄(호출자가 Init 후 SetActive) — 씬 전환으로 파괴된 총알은 Unity == null 로 걸러 버림, 없으면 새로 만듦.</summary>
    public GameObject GetBulletInactive()
    {
        // 살아있는 총알이 나올 때까지 꺼냅니다 (파괴된 건 버림)
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj == null) continue;
            TrackTake(bulletUsage, pool.Count);
            return obj;
        }

        // 쓸 만한 게 없으면 새로 만든다 (풀은 "최대치"가 아니라 "재활용 창고")
        GameObject created = CreateBullet();
        if (bulletPrewarmDone) bulletUsage.CreatedInBattle++;
        TrackTake(bulletUsage, pool.Count);
        return created;
    }

    /// <summary>사용이 끝난 Bullet을 풀에 반환합니다.</summary>
    public void ReturnBullet(GameObject obj)
    {
        // ★ 파괴된 오브젝트를 다시 큐에 넣으면 쓰레기가 계속 누적됩니다.
        if (obj == null) return;

        obj.SetActive(false);
        // 부모를 null로 해제 — 씬 루트에 보관해 좌표 오염 방지
        obj.transform.SetParent(null);
        pool.Enqueue(obj);
    }

    // ── 프리팹별 범용 풀 (Enemy / Boss 등) ──

    // 프리팹 → 그 프리팹 전용 대기 큐 (종류가 늘어도 섞이지 않게)
    private readonly Dictionary<GameObject, Queue<GameObject>> prefabPools
        = new Dictionary<GameObject, Queue<GameObject>>();

    // 인스턴스 → 원본 프리팹 '출생 기록' (인스턴스는 자기 프리팹을 몰라 Return 때 어느 풀인지 조회)
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab
        = new Dictionary<GameObject, GameObject>();

    /// <summary>프리팹 풀에서 비활성으로 꺼냄 — '꺼냄 → 초기화 → 켬' 순서 강제 (먼저 켜면 OnEnable 로 체력 0 적이 조준 목록에 오름).</summary>
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
            // 풀이 비었으면 그때 새로 만든다 (풀은 "최대치"가 아니라 "재활용 창고")
            obj = CreatePooled(prefab);
            UsageOf(prefab).CreatedInBattle++;
        }
        TrackTake(UsageOf(prefab), queue.Count);

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

    /// <summary>첫 스폰 렉 방지 예열 — 대기 중인 수를 빼고 부족분만 채움 (여러 번 불러도 불어나지 않음).</summary>
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        if (!prefabPools.TryGetValue(prefab, out var queue))
            prefabPools[prefab] = queue = new Queue<GameObject>();

        int need = count - queue.Count;   // ★ 부족분만 계산
        for (int i = 0; i < need; i++)
            queue.Enqueue(CreatePooled(prefab));
    }

    /// <summary>범용 풀의 인스턴스를 만드는 유일한 곳 — 출생 기록(instanceToPrefab)과 사용량 집계를 빠뜨리지 않게.</summary>
    private GameObject CreatePooled(GameObject prefab)
    {
        GameObject obj = Instantiate(prefab);
        obj.SetActive(false);
        instanceToPrefab[obj] = prefab;
        UsageOf(prefab).Created++;
        return obj;
    }

    // 지금 나눠서 예열 중인 프리팹 (같은 프리팹을 두 코루틴이 동시에 채우지 않게)
    private readonly HashSet<GameObject> prewarming = new HashSet<GameObject>();

    /// <summary>Prewarm 을 프레임당 perFrame 개씩 나눠서 — 스테이지 전환 때 한꺼번에 만들면 멈칫하고, 첫 스폰까지 여유가 있어서.</summary>
    public void PrewarmOverFrames(GameObject prefab, int count, int perFrame = 2)
    {
        if (prefab == null || count <= 0) return;
        if (prewarming.Contains(prefab)) return;

        if (!prefabPools.TryGetValue(prefab, out var queue))
            prefabPools[prefab] = queue = new Queue<GameObject>();

        int need = count - queue.Count;   // 시작 시점의 부족분만 (스폰으로 큐가 줄어도 더 만들지 않음)
        if (need <= 0) return;

        StartCoroutine(PrewarmRoutine(prefab, queue, need, Mathf.Max(1, perFrame)));
    }

    private IEnumerator PrewarmRoutine(GameObject prefab, Queue<GameObject> queue, int need, int perFrame)
    {
        prewarming.Add(prefab);

        // 먼저 한 프레임 쉼 — StartCoroutine 은 첫 yield 까지 즉시 실행돼 첫 묶음이 부른 쪽 프레임에 얹히므로
        int made = 0;
        while (made < need)
        {
            yield return null;
            for (int i = 0; i < perFrame && made < need; i++, made++)
                queue.Enqueue(CreatePooled(prefab));
        }

        prewarming.Remove(prefab);
    }
    
}
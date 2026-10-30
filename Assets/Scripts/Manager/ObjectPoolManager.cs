using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 오브젝트 풀 관리자 (총알 전용 풀 + 프리팹별 범용 풀)
///
/// ★ 이번 수정: GetBulletInactive()에 "파괴된 인스턴스 건너뛰기" 가드 추가
///   (범용 풀 GetInactive()에는 이미 있던 것이 총알 풀에만 빠져 있었습니다)
///
/// ─── 오브젝트 풀링이란? (학습 포인트) ────────────────────────────────
/// Instantiate(생성)와 Destroy(파괴)는 생각보다 비쌉니다. 특히 Destroy는
/// 쓰레기(garbage)를 남기고, 그게 쌓이면 GC(가비지 컬렉션)가 돌면서
/// 프레임이 뚝 떨어집니다 — 모바일에서 특히 체감돼요.
///
/// 그래서 "만들고 버리는" 대신 "만들어 두고 껐다 켰다" 합니다.
///   - 필요할 때: 꺼져 있는 오브젝트를 큐에서 꺼내 SetActive(true)
///   - 다 썼을 때: SetActive(false) 하고 큐에 다시 넣기
///
/// 방치형 게임처럼 적이 끝없이 나오는 장르에서는 사실상 필수입니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
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
    }

    // 꺼지면 예열 코루틴이 중간에 멈춥니다. 표시를 남겨 두면 그 프리팹은 다시 예열되지 않으므로 비웁니다.
    void OnDisable()
    {
        prewarming.Clear();
        LogUsage();
    }

    // ──────────────────────────────────────────────
    //  사용량 기록 — 예열 개수를 데이터로 정하기 위한 것
    // ──────────────────────────────────────────────
    //
    // 예열 개수는 "동시에 최대 몇 개가 쓰이는가" 에 맞추는 게 정답입니다.
    //   · 너무 적으면 → 전투 중에 풀이 비어 Instantiate 가 일어나고, 그 프레임이 멈칫합니다 (풀링을 하는 이유 그 자체)
    //   · 너무 많으면 → 메모리와 시작 시간만 씁니다 (시작 예열은 여러 프레임에 나눠서 하므로 끊김은 없음)
    // 그래서 짐작으로 줄이지 않고, 실제로 쓴 최대치를 씬이 끝날 때 남깁니다 (GameLog — 상세 로그를 켰을 때만 보임).
    //   "만든 수 > 최대 동시 사용" 이면 예열을 그만큼 줄여도 되고, "전투 중 생성" 이 0 보다 크면 예열이 모자란 것입니다.

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

    // ──────────────────────────────────────────────
    //  시작 예열
    // ──────────────────────────────────────────────

    /// <summary>
    /// ★ [프레임 드랍 수정] "처음 한 번" 비용을 전투가 시작되기 전에, 여러 프레임에 나눠 치릅니다.
    ///
    ///   ① 총알 60개 — 예전에는 Awake 에서 한 프레임에 전부 만들었습니다. 같은 프레임에
    ///      데미지 텍스트·HP 바 예열까지 겹쳐 씬이 켜지는 순간 크게 멈칫했습니다.
    ///      그사이 총알이 부족하면 GetBulletInactive 가 하나씩 만들어 주므로 발사가 막히지 않습니다.
    ///   ② 배치된 동료의 스킬 이펙트 — 풀에서 꺼내 쓰지만 미리 만들어 두지 않아서
    ///      동료마다 첫 스킬을 쓰는 순간 Instantiate + 첫 렌더링 비용이 한꺼번에 들었습니다.
    ///   ③ 흑백 연출 머티리얼 — 처음 쓸 때 Resources.Load 로 동기 로드해서 첫 기절 순간 멈칫했습니다.
    ///      머티리얼만 읽어 두면 셰이더의 GPU 프로그램은 여전히 '처음 그릴 때' 만들어지므로,
    ///      RenderWarmup 으로 투명하게 한 번 그려서 그 비용까지 미리 치릅니다.
    ///
    ///   첫 프레임을 건너뛰는 이유: 씬이 켜지는 프레임에는 모든 Awake/Start 가 몰려 있고,
    ///   동료 배치 복원 같은 다른 오브젝트의 Start 도 끝난 뒤에 봐야 '배치된 동료' 를 정확히 셀 수 있습니다.
    /// </summary>
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

    // ──────────────────────────────────────────────
    //  총알 풀
    // ──────────────────────────────────────────────

    private GameObject CreateBullet()
    {
        // 부모 없이 생성 → 월드 좌표계 기준으로 위치가 적용됨
        GameObject obj = Instantiate(bulletPrefab);
        obj.SetActive(false);
        bulletUsage.Created++;
        return obj;
    }

    /// <summary>
    /// 풀에서 Bullet을 꺼내 반환합니다. (비활성 상태)
    /// 호출자가 위치·Init 설정 후 직접 SetActive(true) 해야 합니다.
    ///
    /// ═══ ★ 이번에 고친 부분 ══════════════════════════════════════════
    ///
    /// 기존 코드:
    ///     if (pool.Count == 0) pool.Enqueue(CreateBullet());
    ///     return pool.Dequeue();
    ///
    /// 문제: 총알은 DontDestroyOnLoad 없이 Instantiate되므로 **씬이 바뀌면 파괴됩니다.**
    /// 그런데 이 매니저가 씬을 넘어 살아남는 구조라면, 큐 안에는 파괴된 총알의
    /// 참조가 그대로 남습니다. 그걸 그냥 Dequeue해서 돌려주면 호출한 쪽에서
    /// MissingReferenceException이 터지거나, 조용히 발사가 씹힙니다.
    ///
    /// 각성 스테이지(EvolveScene)를 왕복하는 구조라 정확히 이 경로를 밟고,
    /// 각성으로 한 번에 여러 발이 나가기 시작하면 훨씬 자주 걸립니다.
    ///
    /// 범용 풀 GetInactive()에는 이 가드가 이미 있습니다. 같은 클래스 안에서
    /// 한쪽에만 방어가 있다면, 대개는 빠진 쪽이 버그입니다.
    ///
    /// ★ 유니티에서 파괴된 오브젝트는 C#의 진짜 null이 아니라 "파괴됨"을 기억하는
    ///   특수 상태입니다. 그래도 == null 비교는 유니티가 오버로딩해 둬서
    ///   true가 나오므로 이 검사가 동작합니다.
    /// ══════════════════════════════════════════════════════════════════
    /// </summary>
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

    // ══════════════════════════════════════════════
    //  프리팹별 범용 풀 (Enemy / Boss 등)
    // ══════════════════════════════════════════════

    // 프리팹 → 대기 중인 인스턴스 큐
    //
    // ─── 왜 Dictionary인가? (학습 포인트) ────────────────────────────
    // 적 프리팹이 여러 종류(슬라임/박쥐/보스...)가 되면 큐 하나로는 부족합니다.
    // 슬라임을 돌려달라고 했는데 박쥐가 나오면 안 되니까요.
    // Dictionary<열쇠, 값>은 "프리팹을 열쇠로 넣으면 그 프리팹 전용 큐가 나오는"
    // 사물함 같은 자료구조입니다. 종류가 몇 개로 늘어나도 코드는 그대로예요.
    // ──────────────────────────────────────────────────────────────
    private readonly Dictionary<GameObject, Queue<GameObject>> prefabPools
        = new Dictionary<GameObject, Queue<GameObject>>();

    // 인스턴스 → 원본 프리팹 (반환 시 어느 풀로 돌려보낼지 판단)
    //
    // 씬에 놓인 인스턴스는 자기가 어느 프리팹에서 태어났는지 스스로 모릅니다.
    // 그래서 만들 때 "출생 기록"을 여기 남겨두고, Return할 때 조회합니다.
    private readonly Dictionary<GameObject, GameObject> instanceToPrefab
        = new Dictionary<GameObject, GameObject>();

    /// <summary>
    /// 프리팹 풀에서 오브젝트를 꺼냅니다. (비활성 상태로 반환)
    /// 호출자가 초기화 후 직접 SetActive(true) 해야 합니다.
    ///
    /// ★ 비활성 상태로 돌려주는 이유:
    ///   SetActive(true)가 되는 순간 OnEnable()이 실행되고,
    ///   Enemy.OnEnable()은 자신을 Enemy.Active 목록에 등록합니다.
    ///   체력·경로가 세팅되기 전에 등록되면, 그 한 프레임 동안
    ///   "체력 0짜리 적"이 타워의 조준 대상이 될 수 있어요.
    ///   그래서 "꺼낸다 → 초기화한다 → 켠다" 순서를 강제하는 설계입니다.
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

    /// <summary>
    /// 미리 생성해 첫 스폰 시 렉을 방지합니다.
    ///
    /// ★ 기존 코드는 호출할 때마다 무조건 count개를 새로 만들어
    ///   스테이지가 바뀔 때마다 인스턴스가 무한히 불어났습니다.
    ///   "이미 대기 중인 개수를 빼고, 부족한 만큼만" 채우도록 바꾼 상태입니다.
    ///   이런 함수를 멱등(idempotent)하다고 합니다.
    /// </summary>
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

    /// <summary>
    /// Prewarm 과 같지만, 한 프레임에 perFrame 개씩 나눠서 만듭니다.
    ///
    /// ★ [프레임 드랍 수정] 스테이지가 바뀌어 적 프리팹이 달라지면 Prewarm 이 그 자리에서 12개를
    ///   한꺼번에 Instantiate 했습니다. 전투 중 스테이지 전환 순간마다 한 프레임이 크게 멈칫합니다.
    ///   첫 스폰까지는 몇 초 여유가 있으므로 여러 프레임에 나눠 만들면 끊김이 사라집니다.
    ///   (그사이 풀이 비면 GetInactive 가 알아서 하나 만들어 주므로 스폰이 막히지는 않습니다)
    /// </summary>
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

        // 먼저 한 프레임 쉰 뒤 만듭니다. StartCoroutine 은 첫 yield 까지 그 자리에서 바로 실행되므로,
        // 만들고 나서 쉬면 첫 묶음이 부른 쪽 프레임(스테이지 전환·씬 시작)에 그대로 얹힙니다.
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
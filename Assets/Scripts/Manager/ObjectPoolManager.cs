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
            if (obj != null) return obj;
        }

        // 쓸 만한 게 없으면 새로 만든다 (풀은 "최대치"가 아니라 "재활용 창고")
        return CreateBullet();
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
        {
            GameObject obj = Instantiate(prefab);
            obj.SetActive(false);
            instanceToPrefab[obj] = prefab;
            queue.Enqueue(obj);
        }
    }

    /// <summary>
    /// 씬을 다시 로드하기 전 등에 호출해 풀 기록을 비웁니다. (선택)
    ///
    /// instanceToPrefab은 인스턴스가 파괴돼도 항목이 남기 때문에,
    /// 씬 전환이 잦으면 딕셔너리가 계속 커집니다.
    /// </summary>
    public void ClearAllPools()
    {
        prefabPools.Clear();
        instanceToPrefab.Clear();
    }
}
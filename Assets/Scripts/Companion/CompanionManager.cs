using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance;

    [Header("동료 설정")]
    [SerializeField] private int maxCompanions = 6;

    [Header("배치 타일맵 (내부 배치 구역)")]
    [SerializeField] private Tilemap placeableTilemap;

    [Header("세이브 복원용 DB (모든 CompanionData)")]
    [SerializeField] private CompanionDatabase database;

    private List<CompanionData> ownedCompanionData = new List<CompanionData>();
    private List<Companion>     ownedCompanions    = new List<Companion>();

    // 셀 좌표 → 배치된 동료 (점유 관리, 라이브 뷰)
    private readonly Dictionary<Vector3Int, Companion> occupied = new Dictionary<Vector3Int, Companion>();

    // 배치 의도 (소유 인덱스 → 셀). 씬을 넘어 유지되며 세이브에 직렬화됨.
    private readonly Dictionary<int, Vector3Int> placementByIndex = new Dictionary<int, Vector3Int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (SaveManager.Instance != null && SaveManager.Instance.HasSave())
        {
            ApplyFrom(SaveManager.Instance.Current);
            CompanionFragment.Instance?.ApplyFrom(SaveManager.Instance.Current);  // ★ 조각도 같은 시점에 복원
        }
    }

    void Start()
    {
       
    }

    public void BindPlaceableTilemap(Tilemap tilemap) => placeableTilemap = tilemap;

    // ──────────────────────────────────────────────
    //  세이브 연동
    // ──────────────────────────────────────────────

    /// <summary>보유 동료 id 목록 + 배치를 SaveData에 기록.</summary>
    public void CaptureTo(SaveData d)
    {
        // 보유 목록 (기존 그대로)
        d.ownedCompanionIds.Clear();
        foreach (var data in ownedCompanionData)
        {
            if (data == null) continue;
            if (string.IsNullOrEmpty(data.id))
            {
                Debug.LogWarning($"[CompanionManager] {data.companionName}의 id가 비어 저장 누락됨");
                continue;
            }
            d.ownedCompanionIds.Add(data.id);
        }

        // ── 배치 ──
        // 배치 가능한 씬이고 실제로 배치된 동료가 있을 때만 스냅샷 갱신.
        // occupied가 비어 있으면(로드 직후·회수 상태 등) 기존 배치를 덮어쓰지 않고 보존.
        if (placeableTilemap != null && occupied.Count > 0)
        {
            SavePlacementSnapshot();

            d.companionPlacements.Clear();
            foreach (var kv in placementByIndex)
            {
                d.companionPlacements.Add(new CompanionPlacement
                {
                    ownedIndex = kv.Key,
                    cellX = kv.Value.x,
                    cellY = kv.Value.y,
                    cellZ = kv.Value.z
                });
            }
        }
        // else: 기존 d.companionPlacements 그대로 유지 (병합 저장)
    }

    /// <summary>
    /// SaveData의 보유 id 목록을 DB로 되찾아 데이터 목록을 복원하고, 배치 의도도 복원.
    /// (실제 오브젝트 생성·배치는 게임 씬의 RestoreIntoScene이 담당)
    /// </summary>
    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;
        if (database == null)
        {
            Debug.LogError("[CompanionManager] CompanionDatabase가 연결되지 않아 동료를 복원할 수 없습니다.");
            return;
        }
        
        // 이미 이 세이브대로 복원돼 오브젝트가 살아있으면 다시 비우지 않는다 (중복 ApplyFrom 방지)
        if (ownedCompanions.Count > 0)
        {
            Debug.Log("[CompanionManager] 이미 복원됨 — ApplyFrom 건너뜀");
            return;
        }

        ownedCompanionData.Clear();
        ownedCompanions.Clear();   // 오브젝트는 RestoreIntoScene에서 재생성
        occupied.Clear();

        foreach (string id in d.ownedCompanionIds)
        {
            CompanionData data = database.GetById(id);
            if (data != null) ownedCompanionData.Add(data);
            else Debug.LogWarning($"[CompanionManager] id '{id}'에 해당하는 CompanionData를 DB에서 찾지 못함");
        }

        // 저장된 배치 의도 복원
        placementByIndex.Clear();
        foreach (var p in d.companionPlacements)
            placementByIndex[p.ownedIndex] = new Vector3Int(p.cellX, p.cellY, p.cellZ);

        Debug.Log($"[CompanionManager] 세이브 복원 — 동료 {ownedCompanionData.Count}명 / 배치 {placementByIndex.Count}개");
    }

    // ──────────────────────────────────────────────
    //  동료 획득
    // ──────────────────────────────────────────────
    public bool AddCompanion(CompanionData data)
    {
        if (ownedCompanions.Count >= maxCompanions)
        {
            Debug.Log("[CompanionManager] 동료 슬롯이 가득 찼습니다.");
            return false;
        }

        // 이미 보유(복원 포함)한 동료면 획득 처리하지 않음
        if (data != null && ownedCompanionData.Exists(c => c != null && c.id == data.id))
        {
            Debug.Log($"[CompanionManager] {data.companionName}은(는) 이미 보유 중 — 획득 무시");
            return false;
        }

        Companion companion = SpawnCompanionObject(data);
        if (companion == null) return false;

        ownedCompanionData.Add(data);
        ownedCompanions.Add(companion);

        SaveManager.Instance?.Save();
        Debug.Log($"[CompanionManager] {data.companionName} 획득");
        return true;
    }

    private Companion SpawnCompanionObject(CompanionData data)
    {
        if (data == null)        { Debug.LogError("[CompanionManager] CompanionData가 null입니다."); return null; }
        if (data.prefab == null) { Debug.LogError($"[CompanionManager] {data.companionName}의 prefab이 null입니다."); return null; }

        GameObject obj       = Instantiate(data.prefab);
        Companion  companion = obj.GetComponent<Companion>();
        if (companion == null)
        {
            Debug.LogError($"[CompanionManager] {data.prefab.name}에 Companion 컴포넌트가 없습니다.");
            Destroy(obj);
            return null;
        }

        companion.Init(data);
        obj.SetActive(false);
        return companion;
    }

    // ──────────────────────────────────────────────
    //  씬 전환 — 배치 스냅샷 / 복원
    // ──────────────────────────────────────────────
    /// <summary>현재 occupied를 placementByIndex로 반영 (배치 가능한 씬에서 호출).</summary>
    public void SavePlacementSnapshot()
    {
        placementByIndex.Clear();
        foreach (var kv in occupied)
        {
            int idx = ownedCompanions.IndexOf(kv.Value);
            if (idx >= 0) placementByIndex[idx] = kv.Key;
        }
        Debug.Log($"[CompanionManager] 배치 스냅샷 — {placementByIndex.Count}개");
    }

    /// <summary>
    /// 새 씬에서 타일맵 연결 후 호출.
    /// 동료 오브젝트를 데이터로 재생성하고 배치 의도(placementByIndex)대로 같은 셀에 재배치.
    /// 생성 실패가 있어도 인덱스가 어긋나지 않게 placementByIndex를 새 인덱스로 다시 만든다.
    /// </summary>
    public void RestoreIntoScene(Tilemap tilemap)
    { 
        // ── 보강: Start의 ApplyFrom이 순서 때문에 skip됐을 수 있으니, 여기서 보장 ──
        if (ownedCompanionData.Count == 0
            && SaveManager.Instance != null
            && SaveManager.Instance.Current != null
            && SaveManager.Instance.Current.ownedCompanionIds.Count > 0)
        {
            ApplyFrom(SaveManager.Instance.Current);
        }

        // ★ 조각 복원도 여기서 보장 (이 시점엔 CompanionFragment.Instance가 확실히 존재)
        CompanionFragment.Instance?.ApplyFrom(SaveManager.Instance?.Current);

        BindPlaceableTilemap(tilemap);
        occupied.Clear();

        // 기존 배치 의도 보관 (원본 인덱스 기준)
        Dictionary<int, Vector3Int> intended = new Dictionary<int, Vector3Int>(placementByIndex);

        List<CompanionData> dataList = new List<CompanionData>(ownedCompanionData);
        ownedCompanions.Clear();
        ownedCompanionData.Clear();
        placementByIndex.Clear();   // 정확한 새 인덱스로 다시 채움

        for (int i = 0; i < dataList.Count; i++)
        {
            Companion companion = SpawnCompanionObject(dataList[i]);
            if (companion == null) continue;

            ownedCompanionData.Add(dataList[i]);
            ownedCompanions.Add(companion);
            int newIndex = ownedCompanions.Count - 1;

            if (intended.TryGetValue(i, out Vector3Int cell))
            {
                if (PlaceCompanion(companion, cell))
                {
                    placementByIndex[newIndex] = cell;
                }
            }
        }
    }

    // ──────────────────────────────────────────────
    //  배치 / 회수 (셀 좌표 기반)
    // ──────────────────────────────────────────────
    public bool PlaceCompanion(Companion companion, Vector3Int cell)
    {
        if (placeableTilemap == null)
        {
            Debug.LogError("[CompanionManager] 배치 타일맵이 설정되지 않았습니다.");
            return false;
        }
        if (!ownedCompanions.Contains(companion))
        {
            Debug.LogWarning("[CompanionManager] 보유하지 않은 동료입니다.");
            return false;
        }
        if (!placeableTilemap.HasTile(cell))
        {
            Debug.Log("[CompanionManager] 배치 가능 구역이 아닙니다.");
            return false;
        }
        if (occupied.ContainsKey(cell))
        {
            Debug.Log($"[CompanionManager] {cell} 셀에 이미 동료가 있습니다.");
            return false;
        }

        RetrieveCompanion(companion);                 // 다른 셀에 있던 동료면 먼저 회수
        occupied[cell] = companion;
        companion.Place(placeableTilemap.GetCellCenterWorld(cell)); // 셀 중앙으로 스냅
        return true;
    }

    public void RetrieveCompanion(Companion companion)
    {
        Vector3Int? found = null;
        foreach (var kv in occupied)
            if (kv.Value == companion) { found = kv.Key; break; }

        if (found.HasValue)
        {
            occupied.Remove(found.Value);
            companion.Retrieve();
        }
    }

    // ──────────────────────────────────────────────
    //  배치 컨트롤러가 쓰는 조회 헬퍼
    // ──────────────────────────────────────────────
    public bool TryGetCell(Vector3 worldPos, out Vector3Int cell)
    {
        cell = default;
        if (placeableTilemap == null) return false;
        cell = placeableTilemap.WorldToCell(worldPos);
        return true;
    }

    public bool CanPlaceAt(Vector3Int cell)
        => placeableTilemap != null
        && placeableTilemap.HasTile(cell)
        && !occupied.ContainsKey(cell);

    public Vector3 GetCellCenter(Vector3Int cell) => placeableTilemap.GetCellCenterWorld(cell);

    // ──────────────────────────────────────────────
    //  조회
    // ──────────────────────────────────────────────
    public List<Companion>     GetOwnedCompanions()    => ownedCompanions;
    public List<CompanionData> GetOwnedCompanionData() => ownedCompanionData;
    public bool                IsFull                  => ownedCompanions.Count >= maxCompanions;
}
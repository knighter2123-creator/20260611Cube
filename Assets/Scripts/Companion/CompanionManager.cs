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

    private List<CompanionData> ownedCompanionData = new List<CompanionData>();
    private List<Companion>     ownedCompanions    = new List<Companion>();

    // 셀 좌표 → 배치된 동료 (점유 관리)
    private readonly Dictionary<Vector3Int, Companion> occupied = new Dictionary<Vector3Int, Companion>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // 매니저가 씬을 넘어 유지되면, StageScene 진입 시 이 함수로 타일맵을 다시 연결하세요.
    public void BindPlaceableTilemap(Tilemap tilemap) => placeableTilemap = tilemap;

    // ──────────────────────────────────────────────
    //  동료 획득 (변경 없음)
    // ──────────────────────────────────────────────
    public bool AddCompanion(CompanionData data)
    {
        if (ownedCompanions.Count >= maxCompanions)
        {
            Debug.Log("[CompanionManager] 동료 슬롯이 가득 찼습니다.");
            return false;
        }
        if (data == null)       { Debug.LogError("[CompanionManager] CompanionData가 null입니다."); return false; }
        if (data.prefab == null){ Debug.LogError($"[CompanionManager] {data.companionName}의 prefab이 null입니다."); return false; }

        GameObject obj      = Instantiate(data.prefab);
        Companion  companion = obj.GetComponent<Companion>();
        if (companion == null)
        {
            Debug.LogError($"[CompanionManager] {data.prefab.name}에 Companion 컴포넌트가 없습니다.");
            Destroy(obj);
            return false;
        }

        companion.Init(data);
        obj.SetActive(false);

        ownedCompanionData.Add(data);
        ownedCompanions.Add(companion);

        Debug.Log($"[CompanionManager] {data.companionName} 획득");
        return true;
    }

    // ──────────────────────────────────────────────
    //  배치 / 회수 (셀 좌표 기반으로 변경)
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

    public void RemoveCompanion(Companion companion)
    {
        if (!ownedCompanions.Contains(companion)) return;

        RetrieveCompanion(companion);                 // occupied에서도 제거
        ownedCompanions.Remove(companion);
        ownedCompanionData?.Remove(companion.Data);

        Destroy(companion.gameObject);
        Debug.Log($"[CompanionManager] {companion.CompanionName} 제거");
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
    //  조회 (변경 없음)
    // ──────────────────────────────────────────────
    public List<Companion>     GetOwnedCompanions()    => ownedCompanions;
    public List<CompanionData> GetOwnedCompanionData() => ownedCompanionData;
    public bool                IsFull                  => ownedCompanions.Count >= maxCompanions;
}
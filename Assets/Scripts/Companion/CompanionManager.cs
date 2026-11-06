using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// ★ partial — 시너지 계산 · 조회는 CompanionManager.Synergy.cs 에 있습니다.
public partial class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance;

    [Header("동료 설정")]
    [SerializeField] private int maxCompanions = 6;

    [Header("배치 타일맵 (내부 배치 구역)")]
    [SerializeField] private Tilemap placeableTilemap;

    [Header("세이브 복원용 DB (모든 CompanionData)")]
    [SerializeField] private CompanionDatabase database;

    private readonly List<CompanionData> ownedCompanionData = new List<CompanionData>();
    private readonly List<Companion>     ownedCompanions    = new List<Companion>();

    // 셀 좌표 → 배치된 동료 (점유 관리, 라이브 뷰)
    private readonly Dictionary<Vector3Int, Companion> occupied = new Dictionary<Vector3Int, Companion>();

    // 배치 의도 (소유 인덱스 → 셀). 씬을 넘어 유지되며 세이브에 직렬화됨.
    private readonly Dictionary<int, Vector3Int> placementByIndex = new Dictionary<int, Vector3Int>();

    // 현재 타일맵에 RestoreIntoScene 이 끝났는가. true 일 때만 occupied 가 '진짜 배치 상태' 입니다.
    private bool restoredIntoScene;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);   // ✅ GachaSystem과 동일하게 스스로 지속

        // CompanionFragment 와 같은 규칙 — 자기 데이터는 자기가 불러온다.
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[CompanionManager] SaveManager 가 아직 준비되지 않아 동료를 복원하지 못했습니다. " +
                           "Project Settings → Script Execution Order 에서 SaveManager 를 -100 으로 지정하세요.");
            return;
        }

        if (SaveManager.Instance.HasSave())
            ApplyFrom(SaveManager.Instance.Current);
    }

    public void BindPlaceableTilemap(Tilemap tilemap)
    {
        // 다른 타일맵으로 바뀌면 그 씬에는 아직 복원 전입니다 (occupied 가 현재 씬을 반영하지 않음).
        if (placeableTilemap != tilemap) restoredIntoScene = false;
        placeableTilemap = tilemap;
    }

    // ── 세이브 연동 ──

    /// <summary>보유 동료 id 목록 + 배치를 SaveData에 기록.</summary>
    public void CaptureTo(SaveData d)
    {
        if (d == null) return;

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

        // ── 배치 — 이 씬에 복원이 끝난 뒤에만 스냅샷 갱신 (개수 > 0 기준이면 '전부 회수'와 '복원 전'을 구분 못 해 회수한 동료가 부활) ──
        if (placeableTilemap != null && restoredIntoScene)
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

    /// <summary>보유 id 를 DB 로 데이터 복원 + 배치 의도 복원 (오브젝트 생성·배치는 게임 씬의 RestoreIntoScene).</summary>
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
            GameLog.Info("[CompanionManager] 이미 복원됨 — ApplyFrom 건너뜀");
            return;
        }

        ownedCompanionData.Clear();
        ownedCompanions.Clear();   // 오브젝트는 RestoreIntoScene에서 재생성
        occupied.Clear();

        if (d.ownedCompanionIds != null)
        {
            foreach (string id in d.ownedCompanionIds)
            {
                CompanionData data = database.GetById(id);
                if (data != null) ownedCompanionData.Add(data);
                else Debug.LogWarning($"[CompanionManager] id '{id}'에 해당하는 CompanionData를 DB에서 찾지 못함");
            }
        }

        // 저장된 배치 의도 복원
        placementByIndex.Clear();
        if (d.companionPlacements != null)
        {
            foreach (var p in d.companionPlacements)
                placementByIndex[p.ownedIndex] = new Vector3Int(p.cellX, p.cellY, p.cellZ);
        }

        GameLog.Info($"[CompanionManager] 세이브 복원 — 동료 {ownedCompanionData.Count}명 / 배치 {placementByIndex.Count}개");

        // ★ 시너지 — 위에서 occupied 를 비웠으므로 결과도 비웁니다 (실제 배치는 RestoreIntoScene 에서 다시 계산)
        RecalculateSynergy();
    }

    // ── 동료 획득 ──
    public bool AddCompanion(CompanionData data)
    {
        if (data == null) return false;

        // 이미 보유(복원 포함)한 동료면 신규 획득 아님 → 조각 처리로 넘김
        if (IsOwned(data.id))
        {
            GameLog.Info($"[CompanionManager] {data.companionName}은(는) 이미 보유 중 — 신규 획득 아님");
            return false;
        }

        // ✅ 보유(도감) 등록은 슬롯 제한과 무관하게 항상 수행 (중복 판정의 기준)
        ownedCompanionData.Add(data);
        GameLog.Info($"[CompanionManager] {data.companionName} 신규 보유 등록");

        // 오브젝트는 항상 생성(6명 제한은 PlaceCompanion 이 배치 수로 검사) — 안 만들면 7번째 동료가 배치 불가, 데이터 순서대로라 인덱스 짝 유지
        Companion companion = SpawnCompanionObject(data);
        if (companion != null)
            ownedCompanions.Add(companion);

        SaveManager.Instance?.Save();
        return true;
    }

    // ── 진화 — 교체 / 제거 (목록 순서 = 배치 기억이라 교체는 같은 인덱스, 제거는 뒤 인덱스를 당겨 맞춤) ──

    /// <summary>보유 동료를 다른 동료로 교체(진화, 대상 미보유) — 같은 목록 자리라 배치돼 있었으면 같은 칸에 섬.</summary>
    public bool ReplaceCompanion(CompanionData from, CompanionData to)
    {
        if (from == null || to == null) return false;
        if (IsOwned(to.id))
        {
            Debug.LogWarning($"[CompanionManager] {to.companionName}은(는) 이미 보유 중이라 교체할 수 없습니다.");
            return false;
        }

        int index = IndexOfOwned(from.id);
        if (index < 0) return false;

        ownedCompanionData[index] = to;

        // 오브젝트는 RestoreIntoScene 전이면 아직 없을 수 있습니다 (그때는 데이터만 바꾸면 복원 때 새 동료로 생성됨).
        if (index < ownedCompanions.Count)
        {
            Companion old = ownedCompanions[index];
            Vector3Int? cell = CellOf(old);

            RetrieveInternal(old);
            if (old != null) Destroy(old.gameObject);

            Companion next = SpawnCompanionObject(to);
            ownedCompanions[index] = next;

            if (next != null && cell.HasValue) PlaceInternal(next, cell.Value);
        }

        GameLog.Info($"[CompanionManager] {from.companionName} → {to.companionName} 교체 (자리 {index})");

        // ★ 시너지 — 진화로 계열이 바뀔 수 있습니다. 회수·배치를 다 끝낸 뒤 한 번만 계산합니다.
        RecalculateSynergy();
        return true;
    }

    /// <summary>보유 동료 하나를 목록에서 뺍니다 (진화 — 대상을 이미 가진 경우). 배치돼 있었다면 회수됩니다.</summary>
    public bool RemoveCompanion(CompanionData data)
    {
        if (data == null) return false;

        int index = IndexOfOwned(data.id);
        if (index < 0) return false;

        if (index < ownedCompanions.Count)
        {
            Companion obj = ownedCompanions[index];
            RetrieveInternal(obj);
            if (obj != null) Destroy(obj.gameObject);
            ownedCompanions.RemoveAt(index);
        }
        ownedCompanionData.RemoveAt(index);

        // 배치 기억을 새 인덱스에 맞춥니다.
        if (restoredIntoScene)
        {
            SavePlacementSnapshot();   // 지금 맵(occupied)이 정답 — 그대로 다시 찍음
        }
        else
        {
            // 맵에 복원 전이면 기억만 손봅니다: 뺀 자리는 지우고, 뒤쪽은 한 칸씩 당김
            var shifted = new Dictionary<int, Vector3Int>();
            foreach (var kv in placementByIndex)
            {
                if (kv.Key == index) continue;
                shifted[kv.Key > index ? kv.Key - 1 : kv.Key] = kv.Value;
            }
            placementByIndex.Clear();
            foreach (var kv in shifted) placementByIndex[kv.Key] = kv.Value;
        }

        GameLog.Info($"[CompanionManager] {data.companionName} 보유 목록에서 제거 (자리 {index})");

        // ★ 시너지 — 배치돼 있던 동료였다면 인원 수가 줄었습니다.
        RecalculateSynergy();
        return true;
    }

    private int IndexOfOwned(string id)
    {
        for (int i = 0; i < ownedCompanionData.Count; i++)
            if (ownedCompanionData[i] != null && ownedCompanionData[i].id == id) return i;
        return -1;
    }

    private Vector3Int? CellOf(Companion companion)
    {
        if (companion == null) return null;
        foreach (var kv in occupied)
            if (kv.Value == companion) return kv.Key;
        return null;
    }

    private Companion SpawnCompanionObject(CompanionData data)
    {
        if (data == null)        { Debug.LogError("[CompanionManager] CompanionData가 null입니다."); return null; }
        if (data.prefab == null) { Debug.LogError($"[CompanionManager] {data.companionName}의 prefab이 null입니다."); return null; }

        GameObject obj       = Instantiate(data.prefab, transform, true);   // 기존 SetParent(transform) 과 같은 월드 기준
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

    // ── 씬 전환 — 배치 스냅샷 / 복원 ──
    /// <summary>현재 occupied를 placementByIndex로 반영 (배치 가능한 씬에서 호출).</summary>
    public void SavePlacementSnapshot()
    {
        placementByIndex.Clear();
        foreach (var kv in occupied)
        {
            int idx = ownedCompanions.IndexOf(kv.Value);
            if (idx >= 0) placementByIndex[idx] = kv.Key;
        }
    }

    /// <summary>새 씬에서 타일맵 연결 후: 동료 오브젝트를 데이터로 재생성하고 배치 의도대로 같은 셀에 재배치 (생성 실패에도 인덱스가 어긋나지 않게 다시 만듦).</summary>
    public void RestoreIntoScene(Tilemap tilemap)
    {
        GameLog.Info($"[CompanionManager] RestoreIntoScene — 데이터 {ownedCompanionData.Count}명, 오브젝트 {ownedCompanions.Count}개");

        BindPlaceableTilemap(tilemap);

        // 이전 씬의 배치 점유 정보만 초기화 (오브젝트는 유지)
        occupied.Clear();

        // 오브젝트가 없으면(최초 실행/앱 재시작 후 복원) 데이터로부터 생성
        if (ownedCompanions.Count == 0 && ownedCompanionData.Count > 0)
        {
            List<CompanionData> dataList = new List<CompanionData>(ownedCompanionData);
            ownedCompanionData.Clear();

            foreach (var data in dataList)
            {
                Companion companion = SpawnCompanionObject(data);
                if (companion == null) continue;
                ownedCompanionData.Add(data);
                ownedCompanions.Add(companion);
            }
        }

        // 모든 유지된 오브젝트를 일단 회수 상태로 (이전 씬 타일맵 위치 해제)
        foreach (var companion in ownedCompanions)
            if (companion != null) companion.Retrieve();

        // 배치 의도(placementByIndex)대로 현재 씬 타일맵에 재배치
        foreach (var kv in placementByIndex)
        {
            int idx = kv.Key;
            Vector3Int cell = kv.Value;
            if (idx < 0 || idx >= ownedCompanions.Count) continue;

            Companion companion = ownedCompanions[idx];
            if (companion != null)
                PlaceInternal(companion, cell);
        }

        restoredIntoScene = true;

        // ★ 시너지 — 동료마다 계산하지 않고, 전부 재배치한 뒤 한 번만 계산합니다.
        RecalculateSynergy();
    }

    // ── 배치 / 회수 (셀 좌표) — 공개 메서드는 시너지 재계산까지, 내부 메서드는 배치만 (여러 명을 옮기는 곳은 끝에서 한 번만 계산) ──
    public bool PlaceCompanion(Companion companion, Vector3Int cell)
    {
        if (!PlaceInternal(companion, cell)) return false;

        RecalculateSynergy();
        return true;
    }

    public void RetrieveCompanion(Companion companion)
    {
        if (RetrieveInternal(companion))
            RecalculateSynergy();
    }

    private bool PlaceInternal(Companion companion, Vector3Int cell)
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
            GameLog.Info("[CompanionManager] 배치 가능 구역이 아닙니다.");
            return false;
        }
        if (occupied.ContainsKey(cell))
        {
            GameLog.Info($"[CompanionManager] {cell} 셀에 이미 동료가 있습니다.");
            return false;
        }

        // 최대 배치 수의 최종 관문은 데이터를 가진 이쪽 (컨트롤러 검사는 알림용), 옮기기는 수가 안 늘어 통과
        if (!companion.IsPlaced && occupied.Count >= maxCompanions)
        {
            GameLog.Info($"[CompanionManager] 최대 배치 수({maxCompanions}) 도달 — 배치하지 않습니다.");
            return false;
        }

        RetrieveInternal(companion);                  // 다른 셀에 있던 동료면 먼저 회수
        occupied[cell] = companion;
        companion.Place(placeableTilemap.GetCellCenterWorld(cell)); // 셀 중앙으로 스냅
        return true;
    }

    /// <summary>배치돼 있던 동료를 회수합니다. 실제로 회수했으면 true (배치돼 있지 않았으면 false).</summary>
    private bool RetrieveInternal(Companion companion)
    {
        if (companion == null) return false;

        Vector3Int? found = CellOf(companion);
        if (!found.HasValue) return false;

        occupied.Remove(found.Value);
        companion.Retrieve();
        return true;
    }

    // ── 배치 컨트롤러가 쓰는 조회 헬퍼 ──
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

    // ── 조회 — 읽기 전용 (바깥에서 Add/Remove 하면 오브젝트·데이터 인덱스 짝이 어긋남) ──
    public IReadOnlyList<Companion>     GetOwnedCompanions()    => ownedCompanions;
    public IReadOnlyList<CompanionData> GetOwnedCompanionData() => ownedCompanionData;
    public int                          MaxCompanions           => maxCompanions;   // 배치 가능한 최대 수 (읽기 전용)

    /// <summary>이 id 의 동료를 보유(도감 등록)했는가.</summary>
    public bool IsOwned(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (CompanionData c in ownedCompanionData)
            if (c != null && c.id == id) return true;
        return false;
    }

    /// <summary>보유 동료 오브젝트를 id 로 찾음 (에셋 참조가 달라도 id 가 같으면 같은 동료).</summary>
    public Companion FindOwnedCompanion(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Companion c in ownedCompanions)
            if (c != null && c.Id == id) return c;
        return null;
    }

    /// <summary>이 id 의 동료가 지금 맵에 배치돼 있는가.</summary>
    public bool IsPlaced(string id)
    {
        Companion c = FindOwnedCompanion(id);
        return c != null && c.IsPlaced;
    }

    // ★ [수정] '오브젝트 수'가 아니라 '배치 수' 기준. (오브젝트는 이제 보유한 만큼 전부 만들어지므로)
    public bool                IsFull                  => occupied.Count >= maxCompanions;

    // ★ [추가] 배치 컨트롤러 / 씬 바인더가 쓰는 조회
    public int                 PlacedCount             => occupied.Count;
    public Tilemap             PlaceableTilemap        => placeableTilemap;
    public bool                HasPlaceableTilemap     => placeableTilemap != null;   // 파괴된 타일맵도 false (유니티식 null 검사)
}
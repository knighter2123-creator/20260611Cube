using UnityEngine;

public class CompanionDebugSpawner : MonoBehaviour
{
    [Header("디버그용 동료 데이터")]
    [SerializeField] private CompanionData[] companionDataList;

    [Header("디버그 배치 셀 좌표 (companionDataList와 인덱스 대응)")]
    [SerializeField] private Vector3Int[] debugCells;   // 예: (0,0,0), (1,0,0) ...

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) SpawnCompanion(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SpawnCompanion(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SpawnCompanion(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SpawnCompanion(3);
        if (Input.GetKeyDown(KeyCode.Alpha5)) SpawnCompanion(4);
        if (Input.GetKeyDown(KeyCode.Alpha6)) SpawnCompanion(5);
    }

    private void SpawnCompanion(int index)
    {
        if (companionDataList == null || index >= companionDataList.Length)
        {
            Debug.LogWarning($"[Debug] companionDataList에 인덱스 {index} 데이터가 없습니다.");
            return;
        }
        if (debugCells == null || index >= debugCells.Length)
        {
            Debug.LogWarning($"[Debug] debugCells에 인덱스 {index} 셀이 없습니다.");
            return;
        }
        if (CompanionManager.Instance == null)
        {
            Debug.LogWarning("[Debug] CompanionManager가 씬에 없습니다.");
            return;
        }

        CompanionData data = companionDataList[index];
        if (data == null)
        {
            Debug.LogWarning($"[Debug] {index}번 CompanionData가 null입니다.");
            return;
        }

        Vector3Int cell = debugCells[index];

        // 해당 셀에 이미 같은 동료가 배치돼 있으면 토글로 회수+제거
        Companion existing = CompanionManager.Instance.GetOwnedCompanions()
            .Find(c => c.Data == data && c.IsPlaced);
        if (existing != null)
        {
            CompanionManager.Instance.RemoveCompanion(existing); // 내부에서 회수까지 처리
            Debug.Log($"[Debug] {existing.CompanionName} 소환 해제");
            return;
        }

        if (!CompanionManager.Instance.AddCompanion(data)) return;

        var companions  = CompanionManager.Instance.GetOwnedCompanions();
        Companion spawned = companions[companions.Count - 1];

        bool placed = CompanionManager.Instance.PlaceCompanion(spawned, cell);
        if (!placed)
        {
            CompanionManager.Instance.RemoveCompanion(spawned); // ← 비활성 잔존 제거
            Debug.LogWarning($"[Debug] {cell} 배치 실패 — 타일 없음/점유됨. debugCells[{index}] 확인");
            return;
        }

        Debug.Log($"[Debug] {data.companionName} → {cell} 배치 완료 / 스킬: {data.ownedSkill?.skillName ?? "없음"}");
    }
}
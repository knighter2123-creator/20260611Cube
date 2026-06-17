using UnityEngine;

public class CompanionDebugSpawner : MonoBehaviour
{
    [Header("디버그용 동료 데이터")]
    [SerializeField] private CompanionData[] companionDataList;

    // ❌ debugSkill 제거 (CompanionData.ownedSkill에서 자동 장착)

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

        // 이미 배치된 동료가 있으면 회수 후 보유 목록에서도 제거
        Companion existing = CompanionManager.Instance.GetSlotOccupant(index);
        if (existing != null)
        {
            CompanionManager.Instance.RetrieveCompanion(existing);
            CompanionManager.Instance.RemoveCompanion(existing);
            Debug.Log($"[Debug] 슬롯 {index} — {existing.CompanionName} 소환 해제");
            return;
        }

        bool added = CompanionManager.Instance.AddCompanion(data);
        if (!added) return;

        var companions = CompanionManager.Instance.GetOwnedCompanions();
        Companion newCompanion = companions[companions.Count - 1];

        bool placed = CompanionManager.Instance.PlaceCompanion(newCompanion, index);
        if (!placed) return;

        Debug.Log($"[Debug] {data.companionName} → 슬롯 {index} 배치 완료 / 스킬: {data.ownedSkill?.skillName ?? "없음"}");
    }
}
using UnityEngine;

public class CompanionDebugSpawner : MonoBehaviour
{
    [Header("디버그용 동료 데이터")]
    [SerializeField] private CompanionData[] companionDataList; // Inspector에서 순서대로 등록

    [Header("디버그용 스킬")]
    [SerializeField] private ActiveSkill debugSkill;            // 자동으로 장착할 스킬

    void Update()
    {
        // Alpha1 ~ Alpha6 → 동료 1~6 소환
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

        // 이미 해당 슬롯에 동료가 있으면 스킵
        Companion existing = CompanionManager.Instance.GetSlotOccupant(index);
        if (existing != null)
        {
            Debug.Log($"[Debug] 슬롯 {index}에 이미 {existing.CompanionName}이 배치되어 있습니다.");
            return;
        }

        // 동료 획득 + 슬롯에 바로 배치
        bool added = CompanionManager.Instance.AddCompanion(data);
        if (!added) return;

        // 방금 추가된 동료 가져오기
        var companions = CompanionManager.Instance.GetOwnedCompanions();
        Companion newCompanion = companions[companions.Count - 1];

        // 슬롯에 배치
        bool placed = CompanionManager.Instance.PlaceCompanion(newCompanion, index);
        if (!placed) return;

        // 디버그 스킬 자동 장착
        if (debugSkill != null)
            CompanionManager.Instance.EquipSkillToCompanion(newCompanion, debugSkill);

        Debug.Log($"[Debug] {data.companionName} → 슬롯 {index} 배치 완료");
    }
}
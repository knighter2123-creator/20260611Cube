using System.Collections.Generic;
using UnityEngine;

public partial class MissionManager : MonoBehaviour
{
    [Header("마스터 미션 (타입별 전체 완료 보상)")]
    [SerializeField] private List<MasterMissionData> masterMissions = new List<MasterMissionData>();
    
    // 마스터 수령 여부: masterId -> claimed
    private readonly Dictionary<string, bool> masterClaimedMap = new Dictionary<string, bool>();
    
    public MasterMissionData GetMasterMission(MissionType type)
    {
        foreach (var mm in masterMissions)
            if (mm != null && mm.missionType == type) return mm;
        return null;
    }
    public MissionState GetMasterState(MissionType type)
    {
        var mm = GetMasterMission(type);
        if (mm == null) return MissionState.InProgress;

        masterClaimedMap.TryGetValue(mm.id, out bool claimed);
        if (claimed) return MissionState.Claimed;

        // 개별 미션을 전부 수령했으면 마스터 수령 가능
        if (CountTotal(type) > 0 && CountCompleted(type) >= CountTotal(type))
            return MissionState.Claimable;

        return MissionState.InProgress;
    }

    public bool TryClaimMaster(MissionType type)
    {
        if (!initialized) EnsureInitialized();

        var mm = GetMasterMission(type);
        if (mm == null) return false;
        if (GetMasterState(type) != MissionState.Claimable) return false;

        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning("[MissionManager] CurrencyManager 없음 — 마스터 보상 취소");
            return false;
        }

        CurrencyManager.Instance.AddGem(mm.gemReward);
        masterClaimedMap[mm.id] = true;

        SaveNow();
        OnMissionUpdated?.Invoke();
        return true;
    }
}

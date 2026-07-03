using UnityEngine;

public partial class MissionManager : MonoBehaviour
{
    // ---------------- 보상 수령 ----------------
    // 외부용: 단건 수령 (즉시 저장 + 이벤트)
    public bool TryClaim(string missionId)
    {
        if (!initialized) EnsureInitialized();
        if (!TryClaimInternal(missionId)) return false;

        SaveNow();
        OnMissionUpdated?.Invoke();
        return true;
    }

// 내부용: 지급만 수행 (저장/이벤트 없음) — 단건·일괄 공용
    private bool TryClaimInternal(string missionId)
    {
        var m = GetMissionData(missionId);
        if (m == null) return false;
        if (!progressMap.TryGetValue(missionId, out var p)) return false;

        if (p.claimed) return false;                        // 중복 수령 방지
        if (p.currentCount < m.requiredCount) return false; // 미달성

        if (CurrencyManager.Instance == null)
        {
            Debug.LogWarning("[MissionManager] CurrencyManager 없음 — 보상 수령 취소");
            return false;
        }

        CurrencyManager.Instance.AddGem(m.gemReward);
        p.claimed = true;
        return true;
    }
    // 특정 타입에서 "수령 가능(완료했지만 미수령)" 미션 개수 → 탭 뱃지용
    public int GetClaimableCount(MissionType type)
    {
        int count = 0;
        foreach (var m in allMissions)
        {
            if (m == null || m.missionType != type) continue;
            if (GetState(m.id) == MissionState.Claimable) count++;
        }
        return count;
    }

// 현재 탭의 완료 미션 일괄 수령 → "모두 수령" 버튼용
// 반환: 실제로 수령된 미션 수
    public int ClaimAll(MissionType type)
    {
        if (!initialized) EnsureInitialized();

        int claimed = 0;
        foreach (var m in allMissions)
        {
            if (m == null || m.missionType != type) continue;
            if (GetState(m.id) != MissionState.Claimable) continue;
            if (TryClaimInternal(m.id)) claimed++;
        }

        if (claimed > 0)
        {
            SaveNow();               // 저장 1회로 묶음
            OnMissionUpdated?.Invoke();
        }
        return claimed;
    }
}

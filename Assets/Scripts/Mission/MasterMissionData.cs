using UnityEngine;

[CreateAssetMenu(fileName = "MasterMissionData", menuName = "Game/Master Mission Data")]
public class MasterMissionData : ScriptableObject
{
    public string id;                  // 예: "master_daily", "master_weekly"
    public MissionType missionType;    // 어느 타입의 개별 미션을 집계하는지
    public int gemReward;              // 전부 완료 시 추가 보상 (사진의 5,000 등)
    [TextArea] public string description; // "일일 미션 클리어"
}
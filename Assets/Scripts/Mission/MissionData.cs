using UnityEngine;

[CreateAssetMenu(fileName = "MissionData", menuName = "Game/Mission Data")]
public class MissionData : ScriptableObject
{
    public string id;
    public MissionType missionType;
    public MissionConditionType conditionType;
    public int requiredCount;

    [Header("보상 (현재 젬 고정)")]
    public int gemReward;
    // 확장 예정: 보상 종류가 늘면 여기에 필드 추가
    // (예: public int goldReward; 또는 List<RewardEntry> rewards)

    [TextArea] public string description;
}
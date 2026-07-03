using System;

[Serializable]
public class MissionProgress
{
    public string missionId;   // 어떤 미션인지 (id 기준)
    public int currentCount;   // 현재 진행 수치
    public bool claimed;       // 보상 수령 여부

    public MissionProgress() { }               // JsonUtility 역직렬화용

    public MissionProgress(string id)
    {
        missionId = id;
        currentCount = 0;
        claimed = false;
    }
}
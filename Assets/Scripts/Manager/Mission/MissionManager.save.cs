using System;
using System.Collections.Generic;
using UnityEngine;

// 세이브 페이로드 — 기존 SaveData 안에 필드로 넣어서 사용
[Serializable]
public class MissionSaveData
{
    public long lastDailyResetTicks;
    public long lastWeeklyResetTicks;
    public List<MissionProgress> progressList = new List<MissionProgress>();
    public List<string> claimedMasterIds = new List<string>(); // 추가: 수령한 마스터 id
}

public partial class MissionManager
{
    // 기존 세이브 진입점에 연결 (예: SaveManager.Instance.Save())
    public void SaveNow()
    {
        // TODO: 프로젝트의 실제 저장 호출로 교체
        SaveManager.Instance?.Save();
    }

    public void CaptureTo(SaveData data)
    {
        if (!initialized || progressMap.Count == 0) return;

        if (data.missionData == null) data.missionData = new MissionSaveData();
        var md = data.missionData;

        md.lastDailyResetTicks = lastDailyResetTicks;
        md.lastWeeklyResetTicks = lastWeeklyResetTicks;

        md.progressList.Clear();
        foreach (var kv in progressMap)
            md.progressList.Add(kv.Value);

        // 마스터: claimed=true 인 id만 저장
        md.claimedMasterIds.Clear();
        foreach (var kv in masterClaimedMap)
            if (kv.Value) md.claimedMasterIds.Add(kv.Key);
    }

    public void ApplyFrom(SaveData data)
    {
        initialized = false;
        progressMap.Clear();
        masterClaimedMap.Clear();

        var md = data != null ? data.missionData : null;
        if (md != null)
        {
            lastDailyResetTicks = md.lastDailyResetTicks;
            lastWeeklyResetTicks = md.lastWeeklyResetTicks;

            if (md.progressList != null)
                foreach (var p in md.progressList)
                {
                    if (p == null || string.IsNullOrEmpty(p.missionId)) continue;
                    progressMap[p.missionId] = p;
                }

            if (md.claimedMasterIds != null)
                foreach (var id in md.claimedMasterIds)
                    if (!string.IsNullOrEmpty(id)) masterClaimedMap[id] = true;
        }

        EnsureInitialized();
    }
}
using System;
using UnityEngine;

public partial class MissionManager : MonoBehaviour
{
    // ---------------- 초기화(리셋) 판정 ----------------

    private void CheckResets()
    {
        DateTime now = DateTime.Now;

        long dailyStart = GetDailyPeriodStart(now).Ticks;
        if (lastDailyResetTicks < dailyStart)
        {
            ResetMissions(MissionType.Daily);
            lastDailyResetTicks = dailyStart;
        }

        long weeklyStart = GetWeeklyPeriodStart(now).Ticks;
        if (lastWeeklyResetTicks < weeklyStart)
        {
            ResetMissions(MissionType.Weekly);
            lastWeeklyResetTicks = weeklyStart;
        }
    }

    private void ResetMissions(MissionType type)
    {
        foreach (var m in allMissions)
        {
            if (m == null || m.missionType != type) continue;
            if (progressMap.TryGetValue(m.id, out var p))
            {
                p.currentCount = 0;
                p.claimed = false;
            }
        }

        // 해당 타입의 마스터 보상도 리셋
        var mm = GetMasterMission(type);
        if (mm != null) masterClaimedMap[mm.id] = false;

        Debug.Log($"[MissionManager] {type} 미션 초기화 완료");
    }

    // 가장 최근의 오전 6시 (그 전이면 어제 6시)
    private DateTime GetDailyPeriodStart(DateTime now)
    {
        DateTime sixAm = new DateTime(now.Year, now.Month, now.Day, ResetHour, 0, 0);
        return now < sixAm ? sixAm.AddDays(-1) : sixAm;
    }

    // 현재 기간이 속한 "가장 최근 월요일 6시"
    private DateTime GetWeeklyPeriodStart(DateTime now)
    {
        DateTime dailyStart = GetDailyPeriodStart(now); // 이미 6시 경계
        // DayOfWeek: 일=0, 월=1 ... 토=6
        int daysSinceMonday = ((int)dailyStart.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return dailyStart.AddDays(-daysSinceMonday);
    }

}

// LevelUpManager 의 경험치 / 레벨업 partial.

using System;

public partial class LevelUpManager
{
    private const int MAX_PLAYER_LEVEL = 999;

    /// <summary>Enemy/Boss 사망 시 호출. 경험치 지급 + 레벨업 처리.</summary>
    public void AddExp(int amount)
    {
        if (!IsReady) return;

        stat.Experience += amount;
        OnExpChanged?.Invoke(stat.Experience);

        // 레벨업 (초과 경험치 이월)
        while (stat.Experience >= stat.MaxExperience && stat.Level < MAX_PLAYER_LEVEL)
        {
            stat.Experience -= stat.MaxExperience;
            stat.Level++;
            stat.MaxExperience = CalculateMaxExp(stat.Level);

            OnLevelUp?.Invoke(stat.Level);
            OnExpChanged?.Invoke(stat.Experience);
        }
    }

    /// <summary>레벨에 따른 필요 경험치. 100 → 115 → 132 ... (1.15배 증가)</summary>
    private static long CalculateMaxExp(int level)
        => (long)Math.Max(1.0, Math.Round(100.0 * Math.Pow(1.15, level - 1))); // 0 방지 가드
}

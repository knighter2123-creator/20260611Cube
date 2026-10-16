// LevelUpManager 의 경험치 / 레벨업 partial.

using System;

public partial class LevelUpManager
{
    private const int MAX_PLAYER_LEVEL = 999;

    /// <summary>Enemy/Boss 사망 시 호출. 경험치 지급 + 레벨업 처리.</summary>
    public void AddExp(long amount)
    {
        if (!IsReady || amount <= 0) return;   // 음수(오버플로된 값)로 경험치가 깎이지 않게

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

    /// <summary>
    /// 레벨에 따른 필요 경험치. 100 × L^1.5 → 100, 283, 520 ... (Lv.100 = 10만, Lv.999 ≈ 316만)
    /// 지수는 획득 경험치 배율(StageFormula.ExpGrowthPower)과 같은 값을 써서,
    /// 획득량과 필요량이 같은 속도로 커지고 스테이지당 약 1레벨 페이스가 끝까지 유지됩니다.
    /// (이전: 100 × 1.15^(L-1) — 후반에 레벨이 멈추고 281레벨부터 long 오버플로)
    /// </summary>
    private static long CalculateMaxExp(int level)
        => (long)Math.Max(1.0, Math.Round(100.0 * Math.Pow(level, StageFormula.ExpGrowthPower))); // 0 방지 가드
}

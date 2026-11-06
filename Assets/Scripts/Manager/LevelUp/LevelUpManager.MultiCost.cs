// N회 강화 누적 비용(UpgradeUI 표시용) — 닫힌 식 대신 CalculateCost 를 N번 더함: 공식을 두 곳에 적으면 비용 곡선 변경 때 화면과 결제가 어긋남 (최대 100번이라 비용 무시)

public partial class LevelUpManager
{
    /// <summary>times 회 강화의 총 비용 — 상한에 걸리면 실제 가능한 횟수만 계산해 buyableCount 로 돌려줌 (3 남음 + ×100 → 3회분).</summary>
    public long GetUpgradeCostMultiple(StatType type, int times, out int buyableCount)
    {
        buyableCount = 0;

        // IsReady 검사는 필수입니다. 검사하지 않으면 UI가 "공짜"라고 표시하고 버튼도 눌리게 됩니다.
        if (!IsReady || times <= 0) return 0L;

        int currentLv = GetUpgradeLevelValue(type);
        int n         = ClampToRemaining(currentLv, times);
        if (n <= 0) return 0L;

        UpgradeConfig config = GetConfig(type);

        // 합계는 long 으로 받습니다. int 로 모으면 21억을 넘는 순간 음수로 뒤집혀 "공짜"처럼 보입니다.
        long total = 0L;
        for (int k = 0; k < n; k++)
            total += CalculateCost(config, currentLv + k);

        buyableCount = n;
        return total;
    }

    /// <summary>buyableCount 가 필요 없을 때 쓰는 간편 버전.</summary>
    public long GetUpgradeCostMultiple(StatType type, int times)
        => GetUpgradeCostMultiple(type, times, out _);

}

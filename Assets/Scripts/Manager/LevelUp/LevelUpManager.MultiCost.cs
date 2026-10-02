// LevelUpManager 의 "N회 강화 누적 비용" partial.
// UpgradeUI 가 ×1/×10/×100 버튼에 표시할 총 비용을 여기서 계산합니다.
//
// ★ 1회 비용 공식(CalculateCost)은 LevelUpManager.stat.cs 한 곳에만 존재합니다.
//   누적 비용도 등차수열 닫힌 식을 따로 적지 않고 CalculateCost 를 N번 더합니다.
//   닫힌 식은 "1회 비용 = baseCost + costPerLevel × 레벨" 을 한 번 더 적어둔 것이라,
//   비용 곡선을 바꾸면 화면 금액과 실제 결제 금액이 조용히 어긋납니다.
//   최대 ×100 이라 더하기 100번 — 성능 차이는 체감되지 않습니다.

public partial class LevelUpManager
{
    /// <summary>
    /// 현재 강화 레벨부터 times 회 강화했을 때의 총 비용.
    /// 상한(MAX_UPGRADE_LEVEL)에 걸리면 실제로 살 수 있는 횟수만 계산하고,
    /// 그 횟수를 buyableCount 로 돌려줍니다.
    /// (예: 상한까지 3레벨 남았는데 times = 100 이면 → buyableCount = 3, 비용도 3회분)
    /// </summary>
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

    /// <summary>
    /// 보유 골드로 최대 몇 번까지 강화할 수 있는지.
    /// ("살 수 있는 만큼 구매" 버튼을 넣게 되면 이걸 쓰면 됩니다.)
    /// </summary>
    public int GetAffordableUpgradeCount(StatType type, long gold, int limit = MAX_UPGRADE_LEVEL)
    {
        if (!IsReady || gold <= 0 || limit <= 0) return 0;

        int currentLv = GetUpgradeLevelValue(type);
        int max       = ClampToRemaining(currentLv, limit);
        if (max <= 0) return 0;

        UpgradeConfig config = GetConfig(type);

        int  count = 0;
        long spent = 0L;

        // "합이 gold 를 넘지 않는 최대 n" 은 하나씩 더해봐야 정확하게 구해집니다. (최대 5000회)
        for (int i = 0; i < max; i++)
        {
            long next = spent + CalculateCost(config, currentLv + i);
            if (next > gold) break;
            spent = next;
            count++;
        }

        return count;
    }
}

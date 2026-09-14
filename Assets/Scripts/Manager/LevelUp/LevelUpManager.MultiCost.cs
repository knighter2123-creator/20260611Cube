// LevelUpManager 의 "N회 강화 누적 비용" partial.
// UpgradeUI 가 ×1/×10/×100 버튼에 표시할 총 비용을 여기서 계산합니다.
//
// ★ 비용 공식(CalculateCost)은 LevelUpManager.stat.cs 한 곳에만 존재합니다.
//   UI가 공식을 복사해 가면, 밸런스를 조정할 때 화면에 뜨는 금액과
//   실제로 빠져나가는 금액이 어긋납니다. 그래서 계산은 매니저가 책임집니다.

using UnityEngine;

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

        // ★ IsReady 검사는 필수입니다.
        //   stat 주입 전에는 GetUpgradeCost 가 0을 돌려주므로,
        //   검사하지 않으면 UI가 "공짜"라고 표시하고 버튼도 눌리게 됩니다.
        if (!IsReady || times <= 0) return 0L;

        int currentLv = GetUpgradeLevelValue(type);
        int remain    = MAX_UPGRADE_LEVEL - currentLv;
        if (remain <= 0) return 0L;

        int n = Mathf.Min(times, remain);
        var (config, _) = GetConfigAndLevel(type);

        // ── 등차수열의 합 ────────────────────────────────
        // 1회 비용 = baseCost + costPerLevel × 레벨   (레벨이 1 오를 때마다 costPerLevel 씩 증가)
        // 따라서 currentLv 부터 n회분의 합은
        //   Σ(k=0..n-1) [ baseCost + costPerLevel × (currentLv + k) ]
        //   = baseCost × n + costPerLevel × ( n × currentLv + n(n-1)/2 )
        //
        // ★ for 루프로 100번 더해도 되지만, 닫힌 식이면 배수가 아무리 커져도 비용이 일정합니다.
        //   그리고 등차수열이라 Mathf.Pow 같은 float 오차가 끼어들 여지가 없습니다.
        //   n(n-1) 은 항상 짝수라 /2 에서 나머지가 버려질 걱정도 없습니다.
        //
        // ★ 중간 계산을 long 으로 올린 이유:
        //   int 로 두면 n × currentLv 단계에서 21억을 넘는 순간 음수로 뒤집혀
        //   "비용이 마이너스 = 공짜"처럼 보입니다. 지금 설정값에선 안 넘지만,
        //   costPerLevel 이나 상한을 올리는 순간 조용히 터지는 종류의 버그입니다.
        long total = (long)config.baseCost * n
                   + (long)config.costPerLevel * ((long)n * currentLv + (long)n * (n - 1) / 2);

        buyableCount = n;
        return total;
    }

    /// <summary>buyableCount 가 필요 없을 때 쓰는 간편 버전.</summary>
    public long GetUpgradeCostMultiple(StatType type, int times)
        => GetUpgradeCostMultiple(type, times, out _);

    /// <summary>
    /// 보유 골드로 최대 몇 번까지 강화할 수 있는지. (지금 UI는 안 쓰지만,
    /// 나중에 "살 수 있는 만큼 구매" 버튼을 넣게 되면 이걸 쓰면 됩니다.)
    /// </summary>
    public int GetAffordableUpgradeCount(StatType type, long gold, int limit = MAX_UPGRADE_LEVEL)
    {
        if (!IsReady || gold <= 0 || limit <= 0) return 0;

        int currentLv = GetUpgradeLevelValue(type);
        int remain    = MAX_UPGRADE_LEVEL - currentLv;
        if (remain <= 0) return 0;

        var (config, _) = GetConfigAndLevel(type);

        int  count = 0;
        long spent = 0L;
        int  max   = Mathf.Min(limit, remain);

        // ★ 여기는 닫힌 식 대신 루프입니다.
        //   "합이 gold 를 넘지 않는 최대 n" 은 2차 부등식이라 닫힌 식이 오히려 부정확해집니다
        //   (제곱근 반올림에서 1회 차이가 납니다). 최대 5000회라 루프가 안전합니다.
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

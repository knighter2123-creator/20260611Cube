// LevelUpManager 의 "N회 강화 누적 비용" partial.
// UpgradeUI 가 ×1/×10/×100 버튼에 표시할 총 비용을 여기서 계산합니다.
//
// ★ 비용 공식(CalculateCost)은 LevelUpManager.stat.cs 한 곳에만 존재합니다.
//   UI가 공식을 복사해 가면, 밸런스를 조정할 때 화면에 뜨는 금액과
//   실제로 빠져나가는 금액이 어긋납니다. 그래서 계산은 매니저가 책임집니다.
//
// ═══ ★ 이번 수정 — 위 원칙을 이 파일 스스로 어기고 있던 부분 정리 ═══════════
//
// [1] 누적 비용을 '등차수열 공식'이 아니라 CalculateCost 를 N번 더해서 구합니다.
//
//   예전 코드는 이렇게 계산했습니다.
//       total = baseCost × n + costPerLevel × (n × lv + n(n−1)/2)
//
//   수학적으로는 정확합니다. 문제는 이 식이 **"1회 비용 = baseCost + costPerLevel × 레벨"
//   이라는 사실을 한 번 더 적어둔 것**이라는 점입니다. 즉 비용 공식이 두 벌이었습니다.
//
//   나중에 비용 곡선을 바꾸면(예: 후반에 비용이 가파르게 오르는 지수 곡선)
//   CalculateCost 는 새 공식으로 결제하는데, 여기는 옛 등차수열로 합계를 보여줍니다.
//     → 화면엔 "10만" 인데 실제로는 30만이 빠져나감
//     → 또는 버튼은 켜져 있는데 눌러도 골드가 모자라 실패
//   에러 없이 숫자만 조용히 틀리는, 가장 찾기 어려운 종류입니다.
//
//   CalculateCost 를 더하기만 하면 비용 공식이 어떻게 바뀌든 자동으로 맞습니다.
//
//   [성능] 강화창의 최대 배수는 ×100 이라 한 번에 최대 100번 더하기입니다.
//         4개 행 × 초당 최대 10번 갱신이어도 초당 수천 번의 정수 덧셈이라
//         **체감 가능한 비용이 아닙니다.** 닫힌 식이 빠르긴 하지만, 여기선 그 차이보다
//         "공식이 한 곳에만 있다"는 안전함이 훨씬 값집니다.
//
// [2] 상한 계산(남은 레벨)을 stat.cs 의 ClampToRemaining() 으로 통일했습니다.
// ══════════════════════════════════════════════════════════════════════

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
        int n         = ClampToRemaining(currentLv, times);
        if (n <= 0) return 0L;

        var (config, _) = GetConfigAndLevel(type);

        // ★ 합계는 long 으로 받습니다.
        //   1회 비용(int)은 작아도, 100회를 더하면 int 범위를 넘을 수 있습니다.
        //   long 변수에 int 를 더하면 C# 이 자동으로 long 덧셈을 하므로 안전합니다.
        //   (반대로 int 변수에 모으면 21억을 넘는 순간 음수로 뒤집혀 "공짜"처럼 보입니다)
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
    /// 보유 골드로 최대 몇 번까지 강화할 수 있는지. (지금 UI는 안 쓰지만,
    /// 나중에 "살 수 있는 만큼 구매" 버튼을 넣게 되면 이걸 쓰면 됩니다.)
    /// </summary>
    public int GetAffordableUpgradeCount(StatType type, long gold, int limit = MAX_UPGRADE_LEVEL)
    {
        if (!IsReady || gold <= 0 || limit <= 0) return 0;

        int currentLv = GetUpgradeLevelValue(type);
        int max       = ClampToRemaining(currentLv, limit);
        if (max <= 0) return 0;

        var (config, _) = GetConfigAndLevel(type);

        int  count = 0;
        long spent = 0L;

        // ★ 여기도 CalculateCost 를 하나씩 더합니다. (위와 같은 이유)
        //   "합이 gold 를 넘지 않는 최대 n" 은 어차피 하나씩 더해봐야 정확하게 구해집니다.
        //   최대 5000회라 루프가 안전합니다.
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
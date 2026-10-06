using Manager.currency;
using UnityEngine;

/// <summary>
/// 일일 던전 1종(골드 던전 / 소환권 던전)의 설정값.
/// 던전마다 에셋을 하나씩 만들어서 사용합니다. 난이도는 에셋을 나누지 않고 공식으로 계산합니다.
///
///   보상      = baseReward + rewardPerLevel × (난이도 - 1)
///               골드 던전   : 500 / 500  → 500, 1000, 1500 …
///               소환권 던전 :   1 /   1  →   1,    2,    3 …
///   보스 체력 = 프리팹 체력 × hpMultiplier × hpGrowthPerLevel^(난이도 - 1)
///   보스 방어 = 프리팹 방어 × defenceMultiplier × defenceGrowthPerLevel^(난이도 - 1)
///
/// ─── 왜 난이도마다 에셋을 만들지 않는가? ──────────────────────────────
/// 진화 스테이지는 티어마다 보상 종류·보스가 달라서 에셋을 나눴지만,
/// 일일 던전은 "같은 보스가 숫자만 커지는" 구조입니다. 규칙이 공식 하나로 표현되면
/// 에셋 10개를 만드는 것보다 공식 하나를 두는 쪽이 밸런스 조정이 훨씬 쉽습니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "DailyDungeonData", menuName = "Stage/Daily Dungeon Data")]
public class DailyDungeonData : ScriptableObject
{
    [Header("식별자 (세이브 기록용 — 던전마다 고유하게, 바꾸지 마세요)")]
    public string id = "dungeon_gold";

    [Header("표시 이름")]
    public string displayName = "골드 던전";

    [Header("입장")]
    [Tooltip("하루(오전 6시 기준)에 입장할 수 있는 횟수")]
    [Min(1)] public int dailyEntries = 3;

    [Tooltip("최대 난이도")]
    [Min(1)] public int maxLevel = 10;

    [Header("입장 횟수 추가 (하루 기준, 오전 6시 초기화)")]
    [Tooltip("횟수 추가에 쓰는 재화 (Gold / Gem / GachaTicket)")]
    public CurrencyType extraEntryCostType = CurrencyType.Gem;

    [Tooltip("n번째 추가의 비용. 배열 길이 = 하루 최대 추가 횟수")]
    public int[] extraEntryCosts = { 300, 500, 700, 1000, 1500 };

    /// <summary>하루에 추가할 수 있는 최대 횟수.</summary>
    public int MaxExtraEntries => extraEntryCosts != null ? extraEntryCosts.Length : 0;

    /// <summary>오늘 이미 purchased 번 추가했을 때 다음 추가 비용. 더 추가할 수 없으면 -1.</summary>
    public int ExtraEntryCost(int purchased)
        => purchased >= 0 && purchased < MaxExtraEntries ? Mathf.Max(0, extraEntryCosts[purchased]) : -1;

    /// <summary>"보석 300" 같은 비용 문구.</summary>
    public string DescribeCost(int amount)
    {
        switch (extraEntryCostType)
        {
            case CurrencyType.Gold:        return $"골드 {NumberFormat.Comma(amount)}";
            case CurrencyType.Gem:         return $"보석 {NumberFormat.Comma(amount)}";
            case CurrencyType.GachaTicket: return $"소환권 {amount}장";
            default:                       return $"{extraEntryCostType} {amount}";
        }
    }

    [Header("보상")]
    [Tooltip("지급할 재화 (Gold / GachaTicket)")]
    public CurrencyType rewardType = CurrencyType.Gold;

    [Tooltip("난이도 1의 보상")]
    [Min(0)] public int baseReward = 500;

    [Tooltip("난이도가 1 오를 때마다 늘어나는 보상")]
    [Min(0)] public int rewardPerLevel = 500;

    [Header("보스")]
    [Tooltip("DailyDungeonBoss 컴포넌트가 붙은 프리팹. 비우면 DailyDungeonManager 의 fallbackBossPrefab 사용")]
    public DailyDungeonBoss bossPrefab;

    [Tooltip("제한시간(초)")]
    [Min(1f)] public float timeLimit = 60f;

    [Tooltip("난이도 1의 체력 배율 (프리팹 기본 체력 × 이 값)")]
    public float hpMultiplier = 10f;
    [Tooltip("난이도가 1 오를 때마다 체력에 곱해지는 값")]
    public float hpGrowthPerLevel = 1.5f;

    [Tooltip("난이도 1의 방어력 배율 (프리팹 기본 방어력 × 이 값)")]
    public float defenceMultiplier = 2f;
    [Tooltip("난이도가 1 오를 때마다 방어력에 곱해지는 값")]
    public float defenceGrowthPerLevel = 1.2f;

    public int ClampLevel(int level) => Mathf.Clamp(level, 1, maxLevel);

    public int RewardFor(int level) => baseReward + rewardPerLevel * (ClampLevel(level) - 1);

    public float HpMultiplierFor(int level)
        => hpMultiplier * Mathf.Pow(hpGrowthPerLevel, ClampLevel(level) - 1);

    public float DefenceMultiplierFor(int level)
        => defenceMultiplier * Mathf.Pow(defenceGrowthPerLevel, ClampLevel(level) - 1);

    /// <summary>"골드 1,500" / "소환권 3장" 같은 보상 문구.</summary>
    public string DescribeReward(int level)
    {
        int amount = RewardFor(level);
        switch (rewardType)
        {
            case CurrencyType.Gold:        return $"골드 {NumberFormat.Comma(amount)}";
            case CurrencyType.Gem:         return $"보석 {NumberFormat.Comma(amount)}";
            case CurrencyType.GachaTicket: return $"소환권 {amount}장";
            default:                       return $"{rewardType} {amount}";
        }
    }
}

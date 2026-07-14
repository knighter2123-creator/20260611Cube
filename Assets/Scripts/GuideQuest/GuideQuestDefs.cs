using System;
using Manager.currency;   // ★ CurrencyType

/// <summary>가이드 퀘스트 종류</summary>
public enum GuideQuestType
{
    EnemyKill,        // 적 처치      → 골드
    StageClear,       // 스테이지 클리어 → 젬
    StatUpgrade,      // 스탯 강화     → 골드
    SummonCompanion,  // 동료 소환     → 젬
    LevelUp           // 레벨업       → 젬
}

/// <summary>단계별로 생성되는 퀘스트 1건 (런타임 전용, 저장하지 않음)</summary>
public class GuideQuest
{
    public int step = 1;
    public GuideQuestType type;
    public LevelUpManager.StatType statType;
    public int targetChapter;
    public int targetStage;
    public long requiredCount;

    // ★ 보상 (재화 종류 + 수량)
    public CurrencyType rewardType;
    public int rewardAmount;

    public string Title => $"가이드 퀘스트 {step}";

    /// <summary>퀘스트 종류 → 보상 재화 종류 (고정 규칙)</summary>
    public static CurrencyType GetRewardType(GuideQuestType type)
    {
        switch (type)
        {
            // 골드 보상
            case GuideQuestType.EnemyKill:
            case GuideQuestType.StatUpgrade:
                return CurrencyType.Gold;

            // 젬 보상
            case GuideQuestType.StageClear:
            case GuideQuestType.SummonCompanion:
            case GuideQuestType.LevelUp:
            default:
                return CurrencyType.Gem;
        }
    }

    /// <summary>UI 표기용 재화 이름</summary>
    public string RewardTypeName => rewardType == CurrencyType.Gold ? "골드" : "보석";

    public string Description
    {
        get
        {
            switch (type)
            {
                case GuideQuestType.EnemyKill:
                    return $"적 {requiredCount}마리 처치";
                case GuideQuestType.StageClear:
                    return $"스테이지 {targetChapter}-{targetStage} 클리어";
                case GuideQuestType.StatUpgrade:
                    return $"{StatName(statType)} {requiredCount}회 강화";
                case GuideQuestType.SummonCompanion:
                    return $"동료 {requiredCount}회 소환";
                case GuideQuestType.LevelUp:
                    return $"레벨 {requiredCount} 달성";
                default:
                    return string.Empty;
            }
        }
    }

    public static string StatName(LevelUpManager.StatType type)
    {
        switch (type)
        {
            case LevelUpManager.StatType.Damage:     return "공격력";
            case LevelUpManager.StatType.CritDamage: return "치명타 공격력";
            case LevelUpManager.StatType.Attackspd:  return "공격 속도";
            case LevelUpManager.StatType.CritChance: return "치명타 확률";
            default: return "-";
        }
    }
}
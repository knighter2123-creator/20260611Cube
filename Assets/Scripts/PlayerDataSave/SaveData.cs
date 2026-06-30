using System;
using System.Collections.Generic;

/// <summary>동료 1명의 배치 정보 (보유 목록 인덱스 → 셀 좌표).</summary>
[Serializable]
public class CompanionPlacement
{
    public int ownedIndex;   // ownedCompanionIds 목록에서의 인덱스
    public int cellX;
    public int cellY;
    public int cellZ;
}

[Serializable]
public class FragmentEntry
{
    public string companionId;
    public int    count;
}

/// <summary>
/// 디스크에 저장되는 플레이어 진행 데이터 (JsonUtility 직렬화용).
/// 필드를 추가하면 자동으로 저장/로드 대상에 포함됩니다.
/// 호환성을 위해 기존 필드 이름은 함부로 바꾸지 마세요.
/// </summary>
[Serializable]
public class SaveData
{
    public int saveVersion = 1;

    // ── 레벨 / 경험치 ──
    public int level         = 1;
    public int experience    = 0;
    public int maxExperience = 100;

    // ── 강화 레벨 ──
    public int upgradeDamage     = 0;
    public int upgradeAttackSpd  = 0;
    public int upgradeCritChance = 0;
    public int upgradeCritDamage = 0;

    // ── 강화로 누적된 실제 전투 스탯 ──
    // ※ PlayerStat의 실제 타입에 맞추세요. baseDamage가 int면 int로 바꿔야 합니다.
    public int baseDamage         = 0;
    public float critical           = 0f;
    public float criticalMultiplier = 0f;

    // ── 스테이지 진행도 ──
    public int currentWorld = 1;
    public int currentStage = 1;

    // ── 영구 버프 ──
    // 누적 데미지 배율. 1.0 = 버프 없음, AddPermanentDamageBuff(0.3) → 1.3
    public float damageMultiplier = 1f;

    // ── 진화 보상 1회 지급 플래그 (지급 완료된 티어 id 목록) ──
    public List<string> claimedEvolveRewards = new List<string>();

    // ── 동료 보유 목록 (CompanionData.id 목록) ──
    public List<string> ownedCompanionIds = new List<string>();
    public List<FragmentEntry> companionFragments = new List<FragmentEntry>();

    // ── 동료 배치 (보유 인덱스 → 셀) ──
    public List<CompanionPlacement> companionPlacements = new List<CompanionPlacement>();

    public int gold = 0;
    public int gem  = 0;
    
    // ── 기타 ──
    public string playerName = "";
    public long lastExitTime = 0;
    public long lastIdleClaimTime = 0;   // 마지막 정산 시각 (DateTime.ToBinary())
}
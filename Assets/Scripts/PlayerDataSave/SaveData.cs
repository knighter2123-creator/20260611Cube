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

/// <summary>동료 1명의 성급(★). 목록에 없는 동료는 1성입니다.</summary>
[Serializable]
public class StarEntry
{
    public string companionId;
    public int    star;
}

/// <summary>던전 1종의 기록. 오늘 입장한 횟수 + 클리어한 최고 난이도.</summary>
[Serializable]
public class DailyDungeonRecord
{
    public string dungeonId;
    public int    usedToday;        // 오늘(리셋 이후) 입장한 횟수
    public int    purchasedToday;   // 오늘(리셋 이후) 재화로 추가한 입장 횟수
    public int    highestCleared;   // 클리어한 최고 난이도 (0 = 아직 없음)
}

[Serializable]
public class DailyDungeonSaveData
{
    public long lastResetTicks;     // 마지막으로 입장 횟수를 초기화한 기간의 시작 시각 (DateTime.Ticks)
    public List<DailyDungeonRecord> records = new List<DailyDungeonRecord>();
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
    public bool nicknamePromptShown;   // 닉네임 자동 팝업을 이미 띄웠는가 (기본 false)
    
    // ── 레벨 / 경험치 ──
    public int level         = 1;
    public long experience    = 0;
    public long maxExperience = 100;

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
    public float attackSpd = 1f;

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
    public List<StarEntry>     companionStars     = new List<StarEntry>();   // 2성 이상만 기록 (없으면 1성)

    // ── 동료 배치 (보유 인덱스 → 셀) ──
    public List<CompanionPlacement> companionPlacements = new List<CompanionPlacement>();

    public int gold = 0;
    public int gem  = 0;

    // ── 소환권 (GachaTicket 이 직접 읽고 씀) ──
    // 이 필드가 없던 예전 세이브는 0 장으로 읽힙니다 (JsonUtility 의 기본값) — 따로 이관할 것이 없습니다.
    public int gachaTicket = 0;

    // ── 기타 ──
    public string playerName = "";
    public long lastExitTime = 0;
    public long lastIdleClaimTime = 0;   // 마지막 정산 시각 (DateTime.ToBinary())
    public MissionSaveData missionData = new MissionSaveData();
    public GuideQuestSaveData guideQuest = new GuideQuestSaveData();

    // ── 일일 던전 (DailyDungeonProgress 가 직접 읽고 씀) ──
    public DailyDungeonSaveData dailyDungeon = new DailyDungeonSaveData();

    // ── 튜토리얼 ──
    // ★ 새 세이브(신규 유저)는 false → MainScene 에서 자동 팝업.
    //   이 필드가 생기기 전의 구버전 세이브(기존 유저)는 SaveManager.Load() 에서 true 로 바꿔 준다.
    //   (JsonUtility 는 JSON 에 없는 필드를 초기값(false)으로 두기 때문에, 그냥 두면 기존 유저에게도 뜬다)
    public bool tutorialDone = false;
}
using UnityEngine;

/// <summary>진화 스테이지 한 티어(레벨 30/50/70/100/200) 설정 에셋 — 보스 프리팹도 여기 둬서 티어 추가 시 에셋 하나만 만들면 됨(함께 바뀌는 건 함께).</summary>
[CreateAssetMenu(fileName = "EvolveStageData", menuName = "Stage/Evolve Stage Data")]
public class EvolveStageData : ScriptableObject
{
    [Header("식별자 (보상 1회 지급 체크용 — 티어마다 고유하게)")]
    public string id = "evolve_30";

    [Header("표시 이름")]
    public string displayName = "진화 스테이지 (Lv.30)";

    [Header("입장 조건")]
    [Tooltip("입장 가능한 최소 플레이어 레벨 (30 / 50 / 70 / 100 / 200)")]
    public int requiredLevel = 30;

    [Header("보스 프리팹")]
    [Tooltip("이 티어에서 등장할 보스 프리팹. 비워두면(None) " +
             "EvolveStageManager의 fallbackBossPrefab을 사용합니다.\n" +
             "※ EvolveBoss 컴포넌트가 붙은 프리팹만 넣을 수 있습니다.")]
    public EvolveBoss bossPrefab;

    [Header("보스 스탯 배율  ※ 일반 스테이지 보스의 약 2~3배로 설정")]
    [Tooltip("Enemy 기본 체력 × 이 값.  (일반 보스 HP 배율 × 2~3 권장)")]
    public float bossHpMultiplier = 15f;
    [Tooltip("Enemy 기본 방어력 × 이 값. (일반 보스 방어 배율 1.5 × 2~3 ≒ 3~4.5)")]
    public float bossDefenceMultiplier = 4f;

    [Header("클리어 보상 (영구 버프)")]
    [Tooltip("플레이어 베이스 대미지 영구 증가율.  0.3 = +30%")]
    public float damageBuffPercent = 0.3f;
}
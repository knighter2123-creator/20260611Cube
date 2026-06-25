using UnityEngine;

/// <summary>
/// 진화 스테이지 한 티어의 설정값.
/// 티어(레벨 30/50/70/100/200)마다 에셋을 하나씩 만들어서 사용합니다.
/// </summary>
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

    [Header("보스 스탯 배율  ※ 일반 스테이지 보스의 약 2~3배로 설정")]
    [Tooltip("Enemy 기본 체력 × 이 값.  (일반 보스 HP 배율 × 2~3 권장)")]
    public float bossHpMultiplier = 15f;
    [Tooltip("Enemy 기본 방어력 × 이 값. (일반 보스 방어 배율 1.5 × 2~3 ≒ 3~4.5)")]
    public float bossDefenceMultiplier = 4f;

    [Header("클리어 보상 (영구 버프)")]
    [Tooltip("플레이어 베이스 대미지 영구 증가율.  0.3 = +30%")]
    public float damageBuffPercent = 0.3f;
}
using UnityEngine;

/// <summary>
/// 진화 스테이지 한 티어의 설정값.
/// 티어(레벨 30/50/70/100/200)마다 에셋을 하나씩 만들어서 사용합니다.
///
/// ★ 이번 수정: bossPrefab 필드 추가 — 티어마다 다른 보스 프리팹을 쓸 수 있습니다.
///
/// ─── 왜 여기에 프리팹을 넣는가? (학습 포인트) ──────────────────────────
/// 이 SO에는 이미 "이 티어는 체력 몇 배, 보상 몇 %"가 들어 있습니다.
/// 즉 "티어를 구분 짓는 모든 정보"가 모이는 자리예요.
/// 겉모습(프리팹)도 티어를 구분 짓는 정보이므로 같은 곳에 두는 게 자연스럽습니다.
///
/// 반대로 EvolveStageManager(씬 오브젝트)에 티어별 프리팹 배열을 두면,
/// 티어를 하나 추가할 때마다 SO도 만들고 씬도 열어서 배열도 늘려야 합니다.
/// 고쳐야 할 곳이 두 군데로 갈라지면 반드시 한쪽을 빼먹게 돼요.
/// "함께 바뀌는 것은 함께 둔다" — 응집도(cohesion)라고 부르는 개념입니다.
/// ────────────────────────────────────────────────────────────────────
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
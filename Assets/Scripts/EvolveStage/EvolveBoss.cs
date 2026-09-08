using UnityEngine;

/// <summary>
/// 진화 스테이지 전용 보스.
///   - 스탯 배율 / 보상%는 입장한 티어(EvolveStageData)에서 읽음 (없으면 SerializeField fallback)
///   - 전리품(Gold/Gem/Exp) 없음 → 대신 플레이어 베이스 대미지 영구 버프
///   - 보상은 티어당 1회만 지급 (SaveData에 기록 — DeleteSave 시 함께 초기화)
///   - 처치 보고는 StageManager가 아니라 EvolveStageManager로
///
/// ★ 이번 수정
///   ① InitStats() → InitForEvolveStage() 로 교체
///      InitStats는 아무도 호출하지 않는 죽은 코드였습니다. 즉 bossHpMultiplier(15배)가
///      한 번도 적용되지 않은 채, 프리팹 원본 체력 그대로 등장하고 있었습니다.
///      이제 EvolveStageManager.SpawnBoss()가 명시적으로 호출합니다.
///
///   ② Die() 전체 override 제거 → GrantRewards() / ReportKill() 훅만 override
///      부모 Die()가 콜라이더 정리·디버프 정리·사망 연출까지 처리해 줍니다.
///
///   ③ 배율 계산을 누적(*=)에서 대입(=)으로 변경
///      진화 보스는 Instantiate로 새로 만들어져서 당장 문제는 없었지만,
///      나중에 재시도 기능 등이 붙으면 바로 터지는 코드였습니다.
///
/// ─── Destroy를 안 해도 되는 이유 (학습 포인트) ─────────────────────────
/// 부모 Die()는 마지막에 ReturnToPool()을 부르고, 그 안의
/// ObjectPoolManager.Return()은 "풀에서 나온 오브젝트가 아니면 Destroy"합니다.
/// 진화 보스는 Instantiate로 직접 만든 오브젝트라 풀 기록에 없으므로
/// 자동으로 Destroy됩니다. 자식이 분기를 신경 쓸 필요가 없어요.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
public class EvolveBoss : Enemy
{
    [Header("배율 fallback (티어 데이터가 없을 때만 사용)")]
    [SerializeField] private float bossHpMultiplier      = 15f;
    [SerializeField] private float bossDefenceMultiplier = 4f;

    [Header("보상 fallback (티어 데이터가 없을 때만 사용)")]
    [SerializeField] private float damageBuffPercent = 0.3f; // +30%

    private bool health50Trigger = false; // 50% 미만
    private bool health20Trigger = false; // 20% 미만

    /// <summary>
    /// 입장 시 선택된 티어 데이터. 없으면 null.
    ///
    /// `private EvolveStageData Data => ...` 는 읽기 전용 프로퍼티입니다.
    /// 필드처럼 쓰지만 접근할 때마다 EvolveStageContext를 새로 조회하므로,
    /// 값이 바뀌어도 항상 최신 상태를 봅니다.
    /// </summary>
    private EvolveStageData Data => EvolveStageContext.SelectedData;

    /// <summary>
    /// ★ EvolveStageManager.SpawnBoss()가 Instantiate 직후 호출합니다.
    ///
    /// 왜 Awake/Start가 아니라 외부에서 부르는가?
    ///   Awake는 Instantiate 도중에 실행되는데, 그 시점에 티어 데이터가
    ///   준비돼 있으리라는 보장이 없습니다(씬 로드 순서에 따라 달라짐).
    ///   "부를 준비가 된 쪽이 명시적으로 부른다"가 훨씬 안전하고,
    ///   나중에 코드를 읽을 때 초기화 시점이 눈에 보인다는 장점도 큽니다.
    ///
    /// baseMaxHealth / baseDefence 는 부모 Enemy.Awake()에서 프리팹 원본값으로
    /// 이미 채워져 있습니다. Instantiate가 끝난 시점에는 Awake가 실행된 뒤이므로
    /// 안심하고 사용할 수 있습니다.
    /// </summary>
    public void InitForEvolveStage()
    {
        float hpMult  = Data != null ? Data.bossHpMultiplier      : bossHpMultiplier;
        float defMult = Data != null ? Data.bossDefenceMultiplier : bossDefenceMultiplier;

        statMult      = 1f;                          // 진화 보스는 스테이지 배율을 쓰지 않음
        maxHealth     = baseMaxHealth * hpMult;      // ★ 누적(*=)이 아니라 대입(=)
        defence       = baseDefence   * defMult;
        currentHealth = maxHealth;

        health50Trigger = false;
        health20Trigger = false;
        isDead          = false;
    }

    /// <summary>
    /// 혹시 외부에서 실수로 호출하더라도 스탯이 망가지지 않도록 막아둡니다.
    /// 진화 보스의 난이도는 티어 데이터만으로 결정되기 때문입니다.
    /// </summary>
    public override void ApplyStatMultiplier(float mult)
    {
        InitForEvolveStage();
    }

    public override void TakeDamage(float damage, bool isCritical)
    {
        base.TakeDamage(damage, isCritical);
        if (isDead) return;
        HealthPhase();
    }

    /// <summary>
    /// 체력 구간에 따라 방어력이 오르는 페이즈 연출.
    ///
    /// health50Trigger 같은 bool 플래그를 쓰는 이유:
    /// TakeDamage는 초당 수십 번 불립니다. 플래그가 없으면 체력이 50% 아래인 동안
    /// 매 타격마다 방어력이 +5씩 계속 올라가 버려요.
    /// "한 번만 실행되어야 하는 코드"에는 이런 1회성 플래그가 필수입니다.
    /// </summary>
    private void HealthPhase()
    {
        float healthPer = (currentHealth / maxHealth) * 100f;

        if (healthPer < 50f && !health50Trigger)
        {
            health50Trigger = true;
            defence += 5f;
        }
        if (healthPer < 20f && !health20Trigger)
        {
            health20Trigger = true;
            defence += 7f;
        }
    }

    /// <summary>훅 ① — 전리품 없음. 영구 베이스 대미지 버프를 티어당 1회만 지급.</summary>
    protected override void GrantRewards()
    {
        // base를 부르지 않습니다 — 골드/경험치를 주지 않는 것이 이 보스의 설계입니다.

        float buff = Data != null ? Data.damageBuffPercent : damageBuffPercent;
        string id  = Data != null ? Data.id : name;

        if (SaveManager.Instance != null && SaveManager.Instance.IsEvolveRewardClaimed(id))
        {
            Debug.Log($"[EvolveBoss] 이미 보상 지급된 티어({id}) — 버프 미지급");
            return;
        }

        PlayerBuffManager.Instance?.AddPermanentDamageBuff(buff);   // 내부에서 Save() 호출
        SaveManager.Instance?.MarkEvolveRewardClaimed(id);          // 플래그 기록 + Save()

        Debug.Log($"[EvolveBoss] 영구 대미지 버프 +{buff * 100f}% 지급 ({id})");
    }

    /// <summary>훅 ② — 일반 StageManager가 아니라 EvolveStageManager로 보고</summary>
    protected override void ReportKill()
    {
        // base를 부르지 않습니다 — StageManager.ReportEnemyKill로 가면
        // 진화 스테이지 처치가 일반 스테이지 킬 카운트에 잘못 더해집니다.
        EvolveStageManager.Instance?.ReportBossKill();
    }

    // ★ Die()는 override하지 않습니다. 부모가 전부 처리합니다.
    //
    // ⚠️ 다만 부모 Die()는 사망 연출(초록색 페이드)이 끝난 뒤 오브젝트를 정리하는데,
    //   ReportKill() → EvolveStageManager.ReportBossKill() → 씬 전환이
    //   먼저 일어나므로 연출이 잘려 보일 수 있습니다.
    //   진화 보스 프리팹의 Enemy 컴포넌트에서 [Use Death Effect] 체크를 해제하거나,
    //   EvolveStageManager 쪽에서 씬 전환을 1초쯤 지연시키면 깔끔해집니다.
}
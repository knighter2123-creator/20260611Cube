using UnityEngine;

/// <summary>
/// 임시 버프의 종류. 매니저가 종류별로 따로 모아서 배율을 곱합니다.
/// 새 종류를 추가하려면 여기에 한 줄 추가하고 AugmentManager 의 배율 프로퍼티만 늘리면 됩니다.
/// </summary>
public enum AugmentBuffKind
{
    SpawnDelay,     // 적 생성 주기 (작을수록 빨리 나옴 → 이 카드는 값을 낮추는 게 아니라 '주기'를 줄여 빨리 나오게 함)
    EnemyDefense    // 적 방어력
}

// ─────────────────────────────────────────────────────────────
//  지속시간이 있는 임시 카드
//  "일정 시간 감소" 계열. 시간이 지나면 알아서 원래대로 돌아옵니다.
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 임시 배율 카드들의 공통 부모.
///
/// [왜 '배율'로 다루는가]
/// 값을 직접 빼면(방어력 -50) 적이 강해질수록 효과가 무의미해지고,
/// 방어력이 음수가 되는 사고도 생깁니다.
/// 배율(×0.5)로 다루면 어느 구간에서든 체감이 일정하고 음수 걱정이 없습니다.
/// 프로젝트의 TargetMove 가 baseSpeed × spawnSpeedMult × slowMultiplier 로
/// 배율 레이어를 쌓는 것과 똑같은 방식입니다.
/// </summary>
public abstract class AugmentTempBuff : AugmentCard
{
    [Header("효과")]
    [Tooltip("0.7 = 30% 감소 (기존 값에 0.7을 곱함). 1이면 아무 변화 없음")]
    [Range(0.05f, 1f)]
    [SerializeField] protected float multiplier = 0.7f;

    [Tooltip("효과 지속 시간(초). 0 이하면 스테이지가 끝날 때까지 유지")]
    [SerializeField] protected float duration = 30f;

    public override bool IsPermanent => false;

    protected abstract AugmentBuffKind Kind { get; }

    protected override string GetValueText()
    {
        // 0.7 → "30%" 로 보여줍니다. 플레이어에게는 '감소량'이 직관적입니다.
        float reducePercent = (1f - multiplier) * 100f;
        return $"{reducePercent:0.#}%";
    }

    /// <summary>설명문에 지속시간을 자동으로 덧붙여 줍니다.</summary>
    public override string GetDescription()
    {
        string body = base.GetDescription();
        if (duration > 0f) return $"{body}\n<size=80%><color=#9AA4B2>({duration:0}초 동안)</color></size>";
        return $"{body}\n<size=80%><color=#9AA4B2>(이번 스테이지 동안)</color></size>";
    }

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        // 임시 버프는 저장하지 않습니다. 게임을 껐다 켰으면 이미 만료된 것으로 봅니다.
        if (isRestore) return;

        manager.AddTempBuff(Kind, multiplier, duration);
    }
}


/// <summary>
/// 적 생성 주기 감소 = 적이 더 빨리 몰려나옴.
///
/// [주의] 이건 사실 '이득'입니다. 적이 빨리 나오면 그만큼 빨리 잡아서
/// 골드·경험치 획득 속도가 올라가니까요. 방치형에서 흔한 '파밍 가속' 카드입니다.
/// 다만 화면에 적이 너무 많아지면 프레임이 떨어지므로,
/// EnemyRespawn 의 maxTotalSpawn 이 상한선 역할을 계속 해줘야 합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/적 생성 주기 감소", fileName = "Augment_SpawnRate")]
public class AugmentSpawnRate : AugmentTempBuff
{
    protected override AugmentBuffKind Kind => AugmentBuffKind.SpawnDelay;
}


/// <summary>
/// 적 방어력 감소 = 우리 대미지가 더 잘 들어감.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/적 방어력 감소", fileName = "Augment_DefenseDown")]
public class AugmentDefenseDown : AugmentTempBuff
{
    protected override AugmentBuffKind Kind => AugmentBuffKind.EnemyDefense;
}

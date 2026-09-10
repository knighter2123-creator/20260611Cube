using UnityEngine;

/// <summary>
/// 지속시간이 있는 임시 배율 카드들의 공통 부모.
///
/// abstract 이므로 이 클래스로는 에셋을 만들 수 없습니다.
/// 자식인 AugmentSpawnRate / AugmentDefenseDown 으로 만드세요.
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
    [SerializeField] protected float duration = 45f;

    public override bool IsPermanent => false;

    /// <summary>어느 배율에 얹을지. 자식이 정합니다.</summary>
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

        if (duration > 0f)
            return $"{body}\n<size=80%><color=#9AA4B2>({duration:0}초 동안)</color></size>";

        return $"{body}\n<size=80%><color=#9AA4B2>(이번 스테이지 동안)</color></size>";
    }

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        // 임시 버프는 저장하지 않습니다. 게임을 껐다 켰으면 이미 만료된 것으로 봅니다.
        if (isRestore) return;

        manager.AddTempBuff(Kind, multiplier, duration);
    }
}

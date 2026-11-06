using UnityEngine;

/// <summary>지속시간 있는 임시 배율 카드 부모(에셋은 자식으로, 저장 안 함) — 고정값을 빼면 후반에 무의미하고 음수가 될 수 있어 배율(×0.5)로 다룸.</summary>
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

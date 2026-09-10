using UnityEngine;

/// <summary>
/// 즉시 보상 카드들의 공통 부모. (골드 / 젬 / 경험치)
///
/// abstract 이므로 이 클래스로는 에셋을 만들 수 없습니다.
/// 자식인 AugmentGold / AugmentGem / AugmentExp 로 만드세요.
///
/// [중복 코드를 줄이는 방법]
/// 세 카드는 "얼마를 줄지 계산하는 방식"이 완전히 같고 "어디에 주는지"만 다릅니다.
/// 그래서 계산 로직은 여기 한 번만 쓰고, 지급 부분만 자식이 채우게 했습니다.
/// 나중에 '재료 지급' 카드를 추가할 때도 클래스 하나만 만들면 됩니다.
/// </summary>
public abstract class AugmentReward : AugmentCard
{
    [Header("지급량")]
    [Tooltip("기본 지급량")]
    [SerializeField] protected double baseAmount = 1000;

    [Tooltip("체크하면 스테이지가 올라갈수록 지급량이 같이 커집니다 (후반에도 쓸모 있게)")]
    [SerializeField] protected bool scaleWithStage = true;

    [Tooltip("스테이지 배율에 곱해줄 계수. 1보다 작게 두면 성장을 완만하게 누를 수 있습니다")]
    [SerializeField] protected float stageScale = 1f;

    // 즉시 보상은 "지금 한 번" 이므로 영구가 아닙니다.
    public override bool IsPermanent => false;

    protected override string GetValueText() => FormatNumber(CalcAmount());

    /// <summary>실제 지급량 계산. 스테이지 배율 반영 여부를 여기서 처리합니다.</summary>
    protected double CalcAmount()
    {
        double v = baseAmount;

        if (scaleWithStage)
        {
            // 현재 스테이지의 스탯 배율(적 체력/보상에 곱해지는 값)을 그대로 재활용합니다.
            // 이러면 밸런스 곡선을 따로 관리하지 않아도 자동으로 따라옵니다.
            v *= AugmentBridge.GetStageMultiplier() * stageScale;
        }

        return v;
    }

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        // ★ 중요: 저장 복구 중이면 절대 다시 주면 안 됩니다.
        //   안 그러면 게임을 켤 때마다 골드가 또 들어오는 무한 재화 버그가 됩니다.
        if (isRestore) return;

        Grant(CalcAmount());
    }

    /// <summary>자식이 "어느 재화에 넣을지"만 구현합니다.</summary>
    protected abstract void Grant(double amount);

    /// <summary>1234567 → "1.23M" 처럼 짧게 표기. 방치형은 숫자가 커지므로 필수입니다.</summary>
    public static string FormatNumber(double v)
    {
        if (v >= 1e12) return (v / 1e12).ToString("0.##") + "T";
        if (v >= 1e9)  return (v / 1e9 ).ToString("0.##") + "B";
        if (v >= 1e6)  return (v / 1e6 ).ToString("0.##") + "M";
        if (v >= 1e3)  return (v / 1e3 ).ToString("0.##") + "K";
        return v.ToString("0");
    }
}

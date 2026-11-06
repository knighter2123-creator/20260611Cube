using UnityEngine;

/// <summary>즉시 보상 카드 부모(골드/젬/경험치, 에셋은 자식으로) — 지급량 계산(× 스테이지 배율)은 여기 한 번, 어디에 줄지만 자식이 구현.</summary>
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
            // 적 스탯 배율을 재활용 — 밸런스 곡선을 따로 관리하지 않아도 따라옴
            v *= StageMultiplier() * stageScale;
        }

        return v;
    }

    /// <summary>지급량에 곱할 스테이지 배율. 기본은 적 스탯 배율, 경험치 카드는 경험치 전용 배율로 덮어씁니다.</summary>
    protected virtual double StageMultiplier() => AugmentBridge.GetStageMultiplier();

    public override void Apply(AugmentManager manager, bool isRestore)
    {
        // ⚠ 복구 중엔 다시 주지 않음 (켤 때마다 들어오는 무한 재화 버그 방지)
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

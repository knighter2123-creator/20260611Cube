using System;
using System.Collections.Generic;

/// <summary>
/// 시너지 1종의 현재 상태 (HUD 표시용).
/// </summary>
public readonly struct SynergyState
{
    public readonly SynergyDefinition Definition;
    public readonly int               Count;          // 조건을 만족한 인원 수
    public readonly SynergyTier       ActiveTier;     // 적용 중인 단계 (없으면 null)
    public readonly int               NextRequired;   // 다음 단계 인원 수 (최고 단계면 -1)

    public SynergyState(SynergyDefinition def, int count, SynergyTier active, int nextRequired)
    {
        Definition   = def;
        Count        = count;
        ActiveTier   = active;
        NextRequired = nextRequired;
    }

    public bool IsActive => ActiveTier != null;

    /// <summary>"3/4" 의 분모. 다음 단계가 있으면 그 인원 수, 최고 단계면 지금 단계 인원 수.</summary>
    public int DisplayGoal => NextRequired > 0 ? NextRequired : (ActiveTier != null ? ActiveTier.requiredCount : Count);
}

/// <summary>
/// 시너지 계산 결과. **만들어진 뒤에는 바뀌지 않습니다** (SynergyCalculator 만 만듭니다).
///
/// ★ 왜 불변(immutable)인가
///   스킬 · 스탯창 · HUD 가 같은 결과를 읽습니다. 누가 중간에 값을 고칠 수 있으면
///   "HUD 는 +15% 인데 실제는 +30%" 같은 어긋남이 생깁니다. 바뀌려면 새로 계산해서 통째로 교체합니다.
/// </summary>
public sealed class SynergyResult
{
    // SynergyStat 값을 인덱스로 쓰는 배열. 매 프레임 불리는 GetCooldown 에서도 할당 · 사전 조회가 없습니다.
    // ★ 선언 순서 주의 — static 필드는 '적힌 순서대로' 초기화됩니다.
    //   아래 Empty 보다 위에 있어야 Empty 가 길이 0 이 아닌 제대로 된 배열을 받습니다.
    internal static readonly int StatCount = Enum.GetValues(typeof(SynergyStat)).Length;

    public static readonly SynergyResult Empty = new SynergyResult(new float[StatCount], Array.Empty<SynergyState>());

    private readonly float[]        stats;
    private readonly SynergyState[] states;

    internal SynergyResult(float[] stats, SynergyState[] states)
    {
        this.stats  = stats;
        this.states = states;
    }

    /// <summary>이 능력치의 합계 보너스 (0 = 없음).</summary>
    public float Get(SynergyStat stat)
    {
        int i = (int)stat;
        return (uint)i < (uint)stats.Length ? stats[i] : 0f;
    }

    /// <summary>모든 시너지의 상태 (정의의 sortOrder 순).</summary>
    public IReadOnlyList<SynergyState> States => states;

    /// <summary>켜진 시너지가 하나라도 있는가.</summary>
    public bool AnyActive
    {
        get
        {
            foreach (SynergyState s in states)
                if (s.IsActive) return true;
            return false;
        }
    }
}

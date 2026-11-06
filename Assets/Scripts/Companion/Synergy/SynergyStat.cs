/// <summary>시너지 능력치 종류 — ⚠ 에셋에 숫자로 저장되니 새 항목은 맨 뒤에, 값은 비율(0.15 = +15%) 또는 SlowRate·ArmorBreakRate 만 %p 가산.</summary>
public enum SynergyStat
{
    SkillDamage    = 0,   // 동료 스킬 피해 (플레이어 공격력 몫 포함 최종 피해에 곱함)
    Cooldown       = 1,   // 동료 스킬 재사용 대기 감소 (0.1 = 10% 짧아짐)
    DebuffDuration = 2,   // 기절 · 둔화 · 방어력 감소 · 독 지속시간
    SlowRate       = 3,   // 둔화율 %p
    ArmorBreakRate = 4,   // 방어력 감소율 %p
    DotDamage      = 5,   // 독 피해
    PlayerDamage   = 6,   // 플레이어 공격력 (PlayerStat.FinalDamage 레이어)
}

/// <summary>시너지 효과 → 화면 문구. HUD 와 도감이 같은 문장을 쓰도록 한 곳에 둡니다.</summary>
public static class SynergyStatText
{
    /// <summary>예) "동료 스킬 피해 +15%", "둔화율 +10%p"</summary>
    public static string Describe(SynergyStat stat, float value)
    {
        string pct  = Percent(value);
        string pctP = Percent(value) + "p";

        return stat switch
        {
            SynergyStat.SkillDamage    => $"동료 스킬 피해 {pct}",
            // 재사용 대기는 '줄어드는' 쪽이 이득이라 부호를 뒤집어 보여줍니다 (0.1 → -10%)
            SynergyStat.Cooldown       => $"동료 재사용 대기 {Percent(-value)}",
            SynergyStat.DebuffDuration => $"디버프 지속시간 {pct}",
            SynergyStat.SlowRate       => $"둔화율 {pctP}",
            SynergyStat.ArmorBreakRate => $"방어력 감소율 {pctP}",
            SynergyStat.DotDamage      => $"독 피해 {pct}",
            SynergyStat.PlayerDamage   => $"플레이어 공격력 {pct}",
            _                          => $"{stat} {pct}"
        };
    }

    // "0.#" 형식(0.125 → +12.5%), 마이너스는 폰트에 없을 수 있어 일반 하이픈
    private static string Percent(float rate)
        => (rate < 0f ? "-" : "+") + (UnityEngine.Mathf.Abs(rate) * 100f).ToString("0.#") + "%";
}

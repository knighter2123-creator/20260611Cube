using System;
using UnityEngine;

/// <summary>PlayerStat → 스탯창 문자열(합계·출처별 세부 내역) — UI 부품을 몰라서 스탯창을 다른 방식으로 다시 만들어도 같은 문장 보장, 상태가 없어 static.</summary>
public static class PlayerStatusText
{
    // ── 라벨 — 두 UI 버전이 같은 이름을 쓰도록 여기에 모읍니다 ──
    public const string TITLE              = "스탯";
    public const string LABEL_DAMAGE       = "공격력";
    public const string LABEL_CRIT_DAMAGE  = "치명타 공격력";
    public const string LABEL_ATTACK_SPEED = "공격 속도";
    public const string LABEL_CRIT_CHANCE  = "치명타 확률";

    /// <summary>값을 아직 읽을 수 없을 때 쓰는 표시. 0 을 보여주면 거짓 정보가 됩니다.</summary>
    public const string UNKNOWN = "-";

    // ── 글리프 — 한글 폰트(주아체 등)에 없는 ·× 는 □ 로 보여 대체 문자를 한곳에 (되돌리려면 Font Asset Creator 에 ·×✕−… 를 추가해 재생성) ──

    /// <summary>세부 내역의 구분자. 폰트에 가운뎃점(·)이 있으면 "  ·  " 가 더 예쁩니다.</summary>
    public const string SEPARATOR = "  |  ";

    /// <summary>배율 기호. 폰트에 곱셈기호(×)가 있으면 "×" 로 바꾸세요.</summary>
    public const string MULTIPLY = "x";

    // ── 표시 옵션 ──

    /// <summary>표시 옵션 묶음 — [Serializable] 이라 UI 인스펙터에 그대로 노출 (씬에 올릴 필요 없음).</summary>
    [Serializable]
    public class Style
    {
        [Tooltip("기여분이 0 인 항목도 내역에 표시합니다. 끄면 줄이 짧아지지만 값에 따라 길이가 달라집니다")]
        public bool showEmptyContributions = true;

        [Tooltip("세부 내역 글자 색 (연한 회색 권장)")]
        public Color detailColor = new Color(0.65f, 0.68f, 0.75f);

        [Tooltip("증강(이번 판 한정) 기여분을 강조할 색")]
        public Color augmentColor = new Color(1f, 0.82f, 0.35f);

        // 각성(영구)은 증강(이번 판)과 다른 색 — 계속 남는 수치인지 구분되게
        [Tooltip("각성(영구) 기여분을 강조할 색")]
        public Color awakenColor = new Color(0.55f, 0.85f, 1f);

        // 시너지는 '동료 배치에 따라' 켜졌다 꺼지는 값이라 증강 · 각성과 또 다른 색을 씁니다.
        [Tooltip("동료 시너지 기여분을 강조할 색")]
        public Color synergyColor = new Color(0.55f, 1f, 0.55f);
    }

    // 호출부가 Style 을 넘기지 않아도 동작하게 기본값을 하나 들고 있습니다.
    private static readonly Style Fallback = new Style();

    // ── 레벨 / 경험치 ──
    public static string Level(PlayerStat s) => s == null ? $"Lv. {UNKNOWN}" : $"Lv. {s.Level}";

    public static string Exp(PlayerStat s)
        => s == null ? UNKNOWN : NumberFormat.Ratio(s.Experience, s.MaxExperience);

    /// <summary>경험치 게이지 fillAmount (0~1).</summary>
    public static float ExpRatio(PlayerStat s)
    {
        // MaxExperience 가 0 이면 0 나누기입니다. 방어적으로 막습니다.
        if (s == null || s.MaxExperience <= 0) return 0f;
        return Mathf.Clamp01((float)s.Experience / s.MaxExperience);
    }

    // ── 공격력 ──
    public static string DamageTotal(PlayerStat s)
        => s == null ? UNKNOWN : NumberFormat.Short(s.FinalDamage);

    public static string DamageDetail(PlayerStat s, Style style = null)
    {
        if (s == null) return string.Empty;
        Style st = style ?? Fallback;

        // 기본 20  |  강화 Lv.96 +480  |  보너스 x1.45 (+225)  |  각성 x1.30 (+218)
        string d = Part(st, $"기본 {NumberFormat.Short(s.PureBaseDamage)}");

        d += Sep(Part(st,
            $"강화 Lv.{s.UpgradeLevelDamage} {NumberFormat.Signed(s.UpgradeDamageBonus)}",
            s.UpgradeDamageBonus != 0));

        d += Sep(Colored(st, st.augmentColor,
            $"보너스 {MULTIPLY}{s.AugmentAttackMultiplier:0.00} ({NumberFormat.Signed(s.AugmentDamageBonus)})",
            !Mathf.Approximately(s.AugmentAttackMultiplier, 1f)));

        // 각성 영구 버프 — x1.00 이면 showEmptyContributions 가 꺼져 있을 때 숨김
        d += Sep(Colored(st, st.awakenColor,
            $"각성 {MULTIPLY}{s.PermanentDamageMultiplier:0.00} ({NumberFormat.Signed(s.AwakenDamageBonus)})",
            !Mathf.Approximately(s.PermanentDamageMultiplier, 1f)));

        // ★ 동료 시너지 (조화 등). 각성과 같은 이유로 x1.00 이면 기본적으로 숨깁니다.
        d += Sep(Colored(st, st.synergyColor,
            $"시너지 {MULTIPLY}{s.SynergyDamageMultiplier:0.00} ({NumberFormat.Signed(s.SynergyDamageBonus)})",
            !Mathf.Approximately(s.SynergyDamageMultiplier, 1f)));

        return d;
    }

    // ── 치명타 공격력 — 합계는 체감되는 실제 대미지(FinalDamage 기반이라 모든 배율 자동 반영), 배수는 내역 맨 앞 ──

    public static string CritDamageTotal(PlayerStat s)
        => s == null ? UNKNOWN : NumberFormat.Short(s.FinalCriticalDamage);

    public static string CritDamageDetail(PlayerStat s, Style style = null)
    {
        if (s == null) return string.Empty;
        Style st = style ?? Fallback;

        string d = Part(st, $"배수 {MULTIPLY}{s.FinalCriticalMultiplier:0.00}");

        d += Sep(Part(st, $"기본 {MULTIPLY}{s.PureBaseCritMultiplier:0.00}"));

        d += Sep(Part(st,
            $"강화 Lv.{s.UpgradeLevelCritDamage} {Signed2(s.UpgradeCritDamageBonus)}",
            s.UpgradeCritDamageBonus > 0f));

        d += Sep(Colored(st, st.augmentColor,
            $"보너스 {Signed2(s.AugmentCritDamageBonus)}",
            s.AugmentCritDamageBonus > 0f));

        return d;
    }

    // ── 공격 속도 — 내부 ms 는 줄수록 빨라 약해진 걸로 오해하므로 초당 공격 횟수로 표시 ──

    public static string AttackSpeedTotal(PlayerStat s)
    {
        if (s == null) return UNKNOWN;

        string t = $"{s.FinalAttacksPerSecond:0.##}회/초";
        if (s.IsAttackSpeedCapped) t += "  (한계)";
        return t;
    }

    public static string AttackSpeedDetail(PlayerStat s, Style style = null)
    {
        if (s == null) return string.Empty;
        Style st = style ?? Fallback;

        string d = Part(st, $"쿨타임 {s.FinalAttackCooldown:0.000}초");

        d += Sep(Part(st, $"기본 {s.PureBaseAttackSpd:0}ms"));

        d += Sep(Part(st,
            $"강화 Lv.{s.UpgradeLevelAttackSpd} -{s.UpgradeAttackSpdReduction:0}ms",
            s.UpgradeAttackSpdReduction > 0f));

        d += Sep(Colored(st, st.augmentColor,
            $"보너스 -{s.AugmentAttackSpdReduction:0}ms",
            s.AugmentAttackSpdReduction > 0f));

        return d;
    }

    // ── 치명타 확률 ──
    public static string CritChanceTotal(PlayerStat s)
        => s == null ? UNKNOWN : $"{s.FinalCritical:0.##}%";

    public static string CritChanceDetail(PlayerStat s, Style style = null)
    {
        if (s == null) return string.Empty;
        Style st = style ?? Fallback;

        string d = Part(st, $"기본 {s.PureBaseCritical:0.##}%");

        d += Sep(Part(st,
            $"강화 Lv.{s.UpgradeLevelCritChance} {Signed2(s.UpgradeCritChanceBonus)}%p",
            s.UpgradeCritChanceBonus > 0f));

        d += Sep(Colored(st, st.augmentColor,
            $"보너스 {Signed2(s.AugmentCritChanceBonus)}%p",
            s.AugmentCritChanceBonus > 0f));

        if (s.FinalCritical >= PlayerStat.MAX_CRITICAL)
            d += Sep("(최대)");

        return d;
    }

    // ── 문자열 조립 — 조각 만들기(Part)와 구분자 붙이기(Sep)를 나눠 Detail 함수에서 if 를 없앰 ──

    /// <summary>조건이 참일 때만(또는 옵션이 켜져 있을 때만) 문자열을 남깁니다.</summary>
    private static string Part(Style st, string text, bool visible = true)
        => (visible || st.showEmptyContributions) ? text : string.Empty;

    /// <summary>비어 있지 않은 조각 앞에만 구분자를 붙입니다.</summary>
    private static string Sep(string text)
        => string.IsNullOrEmpty(text) ? string.Empty : SEPARATOR + text;

    /// <summary>TMP 리치 텍스트 color 태그로 한 줄 안 부분 색상 (Rich Text 필요).</summary>
    private static string Colored(Style st, Color color, string text, bool visible = true)
    {
        string body = Part(st, text, visible);
        if (string.IsNullOrEmpty(body)) return string.Empty;

        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{body}</color>";
    }

    /// <summary>소수 둘째 자리까지, 부호를 항상 붙여서. (0.05 → "+0.05")</summary>
    private static string Signed2(float value)
        => (value < 0f ? "-" : "+") + Mathf.Abs(value).ToString("0.##");
}
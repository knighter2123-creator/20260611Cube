using System;
using UnityEngine;

/// <summary>
/// PlayerStat → 화면에 띄울 문자열. **UI 부품을 전혀 모릅니다.**
///
/// ─────────────────────────────────────────────────────────────
/// [왜 이 클래스를 따로 만들었나 — 이번 작업의 학습 포인트]
///
/// 스탯창을 두 가지 방식으로 만들게 되었습니다.
///   · PlayerStatusUI      — 프리팹에 TMP 를 배치하고 인스펙터로 연결
///   · PlayerStatusCodeUI  — 스크립트가 캔버스부터 전부 코드로 생성
///
/// 두 버전 모두 "공격력 725, 기본 20 · 강화 Lv.96 +480 · 증강 ×1.45" 라는
/// **똑같은 문장**을 만들어야 합니다. 화면에 놓는 방법만 다르죠.
///
/// 그 문장 조립을 각 UI 안에 복사해 두면 이런 일이 벌어집니다.
///   · "%p 를 %로 바꾸자" → 고칠 곳이 두 군데
///   · 한 곳만 고치면 두 버전이 다른 값을 보여줌 → 어느 쪽이 맞는지 알 수 없음
///
/// 그래서 판단 기준을 다시 적용했습니다.
///
///   이 코드가 TMP_Text 나 Button 이라는 부품을 몰라도 되는가?
///   몰라도 된다면 UI 밖으로 빼는 게 맞습니다.
///
/// AugmentUIFactory 를 뺀 것, NumberFormat 을 뺀 것과 정확히 같은 판단입니다.
/// 다만 방향이 반대입니다 —
///   AugmentUIFactory : "도메인을 모르는 범용 부품" 을 아래로 뺐고
///   PlayerStatusText : "부품을 모르는 도메인 규칙" 을 위로 뺐습니다
/// UI 코드는 그 사이에 얇게 남습니다. 이게 UI 를 두 벌 만들 수 있는 이유입니다.
///
/// static 클래스인 이유: 상태가 없습니다. 같은 PlayerStat 을 넣으면 항상 같은 문자열이 나옵니다.
/// ─────────────────────────────────────────────────────────────
/// </summary>
public static class PlayerStatusText
{
    // ══════════════════════════════════════════════════════════
    //  라벨 — 두 UI 버전이 같은 이름을 쓰도록 여기에 모읍니다
    // ══════════════════════════════════════════════════════════
    public const string TITLE              = "스탯";
    public const string LABEL_DAMAGE       = "공격력";
    public const string LABEL_CRIT_DAMAGE  = "치명타 공격력";
    public const string LABEL_ATTACK_SPEED = "공격 속도";
    public const string LABEL_CRIT_CHANCE  = "치명타 확률";

    /// <summary>값을 아직 읽을 수 없을 때 쓰는 표시. 0 을 보여주면 거짓 정보가 됩니다.</summary>
    public const string UNKNOWN = "-";

    // ══════════════════════════════════════════════════════════
    //  ★ 글리프 — 폰트에 없는 문자를 여기서 한 번에 바꿉니다
    // ══════════════════════════════════════════════════════════
    //
    // [왜 상수로 빼는가 — 겪으신 문제가 바로 이것입니다]
    //
    // TMP 는 폰트 에셋에 없는 문자를 만나면 □ 로 그리고 이런 경고를 냅니다.
    //
    //   The character with Unicode value · was not found in the
    //   [BMJUA_ttf SDF] font asset or any potential fallbacks.
    //
    // 게임용 한글 폰트(배민 주아체 같은)는 한글·숫자·기본 문장부호만 담고 있어서
    // 가운뎃점(·) 이나 곱셈기호(×) 같은 기호가 빠져 있는 경우가 많습니다.
    //
    // 문제는 그 문자가 코드 여기저기에 흩어져 있으면 **전부 찾아 고치기 어렵다**는 점입니다.
    // 실제로 · 는 Sep() 안의 딱 한 줄에만 있는데, 화면에는 한 줄에 세 번씩 나옵니다.
    // 그래서 UI 파일만 뒤지면 영원히 안 보입니다.
    //
    // 이렇게 이름 붙여 한곳에 모아두면 폰트를 바꿀 때마다 여기만 보면 됩니다.
    // "같은 의미의 값이 여러 곳에 흩어지면 반드시 하나는 어긋난다" 의 또 다른 사례예요.
    //
    // [원래 기호로 되돌리고 싶다면]
    // 폰트 에셋에 글리프를 추가하는 쪽이 근본 해결입니다.
    //   Window → TextMeshPro → Font Asset Creator
    //   → Character Set 을 "Custom Characters" 로 두고 아래를 붙여넣어 재생성
    //        ·×✕−…™©
    // 또는 TMP Settings 의 Default Font Asset 에 한글+기호가 다 있는 폰트를
    // Fallback 으로 등록해도 됩니다. (Fallback 은 렌더링 비용이 조금 더 듭니다)

    /// <summary>세부 내역의 구분자. 폰트에 가운뎃점(·)이 있으면 "  ·  " 가 더 예쁩니다.</summary>
    public const string SEPARATOR = "  |  ";

    /// <summary>배율 기호. 폰트에 곱셈기호(×)가 있으면 "×" 로 바꾸세요.</summary>
    public const string MULTIPLY = "x";

    /// <summary>닫기 버튼 라벨. 폰트에 ✕ 가 있으면 "✕" 가 더 예쁩니다.</summary>
    public const string CLOSE_LABEL = "X";

    // ══════════════════════════════════════════════════════════
    //  표시 옵션
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// 두 UI 버전이 인스펙터에 그대로 노출할 수 있도록 [Serializable] 로 묶었습니다.
    /// MonoBehaviour 가 아니라 데이터 묶음이라 씬에 올릴 필요가 없습니다.
    /// </summary>
    [Serializable]
    public class Style
    {
        [Tooltip("기여분이 0 인 항목도 내역에 표시합니다. 끄면 줄이 짧아지지만 값에 따라 길이가 달라집니다")]
        public bool showEmptyContributions = true;

        [Tooltip("세부 내역 글자 색 (연한 회색 권장)")]
        public Color detailColor = new Color(0.65f, 0.68f, 0.75f);

        [Tooltip("보너스 기여분을 강조할 색")]
        public Color augmentColor = new Color(1f, 0.82f, 0.35f);
    }

    // 호출부가 Style 을 넘기지 않아도 동작하게 기본값을 하나 들고 있습니다.
    private static readonly Style Fallback = new Style();

    // ══════════════════════════════════════════════════════════
    //  레벨 / 경험치
    // ══════════════════════════════════════════════════════════
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

    // ══════════════════════════════════════════════════════════
    //  공격력
    // ══════════════════════════════════════════════════════════
    public static string DamageTotal(PlayerStat s)
        => s == null ? UNKNOWN : NumberFormat.Short(s.FinalDamage);

    public static string DamageDetail(PlayerStat s, Style style = null)
    {
        if (s == null) return string.Empty;
        Style st = style ?? Fallback;

        // 기본 20  |  강화 Lv.96 +480  |  증강 x1.45 (+225)
        string d = Part(st, $"기본 {NumberFormat.Short(s.PureBaseDamage)}");

        d += Sep(Part(st,
            $"강화 Lv.{s.UpgradeLevelDamage} {NumberFormat.Signed(s.UpgradeDamageBonus)}",
            s.UpgradeDamageBonus != 0));

        d += Sep(Colored(st,
            $"보너스 {MULTIPLY}{s.AugmentAttackMultiplier:0.00} ({NumberFormat.Signed(s.AugmentDamageBonus)})",
            !Mathf.Approximately(s.AugmentAttackMultiplier, 1f)));

        return d;
    }

    // ══════════════════════════════════════════════════════════
    //  치명타 공격력
    // ══════════════════════════════════════════════════════════
    //
    // 배수(×2.45)보다 "터지면 1,776 이 들어간다" 가 체감에 직결되므로
    // 합계는 실제 대미지, 내역 맨 앞에 배수를 둡니다.
    // 배수를 크게 보여주고 싶으면 아래 한 줄만 바꾸면 두 UI 버전이 동시에 바뀝니다.

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

        d += Sep(Colored(st,
            $"보너스 {Signed2(s.AugmentCritDamageBonus)}",
            s.AugmentCritDamageBonus > 0f));

        return d;
    }

    // ══════════════════════════════════════════════════════════
    //  공격 속도
    // ══════════════════════════════════════════════════════════
    //
    // ★ 내부 값(AttackSpd)은 "공격 주기 ms" 라서 작을수록 빠릅니다.
    //   그대로 보여주면 강화했는데 숫자가 줄어드는 걸 보고 약해졌다고 오해합니다.
    //   그래서 초당 공격 횟수로 뒤집습니다.

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

        d += Sep(Colored(st,
            $"보너스 -{s.AugmentAttackSpdReduction:0}ms",
            s.AugmentAttackSpdReduction > 0f));

        return d;
    }

    // ══════════════════════════════════════════════════════════
    //  치명타 확률
    // ══════════════════════════════════════════════════════════
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

        d += Sep(Colored(st,
            $"보너스 {Signed2(s.AugmentCritChanceBonus)}%p",
            s.AugmentCritChanceBonus > 0f));

        if (s.FinalCritical >= PlayerStat.MAX_CRITICAL)
            d += Sep("(최대)");

        return d;
    }

    // ══════════════════════════════════════════════════════════
    //  문자열 조립 도우미
    // ══════════════════════════════════════════════════════════
    //
    // 세부 내역은 "· 로 이어붙인 조각들" 입니다.
    // 기여분이 0 인 조각을 숨기는 옵션이 있어서 조각은 있을 수도 없을 수도 있습니다.
    // "조각을 만드는 함수" 와 "구분자를 붙이는 함수" 를 나누면
    // 위쪽 Detail 함수들에서 if 문이 전부 사라집니다.

    /// <summary>조건이 참일 때만(또는 옵션이 켜져 있을 때만) 문자열을 남깁니다.</summary>
    private static string Part(Style st, string text, bool visible = true)
        => (visible || st.showEmptyContributions) ? text : string.Empty;

    /// <summary>비어 있지 않은 조각 앞에만 구분자를 붙입니다.</summary>
    private static string Sep(string text)
        => string.IsNullOrEmpty(text) ? string.Empty : SEPARATOR + text;

    /// <summary>
    /// TMP 리치 텍스트로 색을 입힙니다.
    /// &lt;color=#RRGGBB&gt; 태그를 지원하므로 텍스트를 여러 개로 쪼개지 않고
    /// 한 줄 안에서 부분 색상을 줄 수 있습니다. (Rich Text 가 켜져 있어야 합니다)
    /// </summary>
    private static string Colored(Style st, string text, bool visible = true)
    {
        string body = Part(st, text, visible);
        if (string.IsNullOrEmpty(body)) return string.Empty;

        return $"<color=#{ColorUtility.ToHtmlStringRGB(st.augmentColor)}>{body}</color>";
    }

    /// <summary>소수 둘째 자리까지, 부호를 항상 붙여서. (0.05 → "+0.05")</summary>
    private static string Signed2(float value)
        => (value < 0f ? "-" : "+") + Mathf.Abs(value).ToString("0.##");
}
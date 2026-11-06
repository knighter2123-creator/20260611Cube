using System.Collections.Generic;
using UnityEngine;

/// <summary>동료 계열(같은 계열 여러 명이면 시너지) — ⚠ 에셋에 숫자로 저장되니 새 계열은 맨 뒤에 (중간에 끼우면 한 칸씩 밀림).</summary>
public enum SynergyTag
{
    None    = 0,   // 계열 없음 / CompanionData 에서는 '스킬로 자동 판정'
    Dealer  = 1,   // 딜러 — 공격, 폭발
    Control = 2,   // 제어 — 기절, 둔화
    Debuff  = 3,   // 약화 — 독, 방어력 감소
}

/// <summary>계열 판정·표시 이름·색 — HUD·도감·목록이 같은 규칙을 쓰도록 한 곳에.</summary>
public static class SynergyTags
{
    /// <summary>None 을 뺀 실제 계열 목록. '조화'(모든 계열) 시너지가 이 목록을 돕니다.</summary>
    public static readonly SynergyTag[] All = { SynergyTag.Dealer, SynergyTag.Control, SynergyTag.Debuff };

    /// <summary>이 동료의 계열 — synergyTag 가 None 이면 고유 스킬 종류로 자동 판정 (기존 에셋을 하나하나 안 고쳐도 되게, 예외는 에셋에서 직접 지정).</summary>
    public static SynergyTag Of(CompanionData data)
    {
        // ScriptableObject 라 '?.' 대신 유니티식 null 검사
        if (data == null) return SynergyTag.None;
        if (data.synergyTag != SynergyTag.None) return data.synergyTag;
        return FromSkill(data.ownedSkill);
    }

    public static SynergyTag FromSkill(ActiveSkill skill)
    {
        if (skill == null) return SynergyTag.None;

        return skill switch
        {
            SkillAttack     => SynergyTag.Dealer,
            SkillExplosion  => SynergyTag.Dealer,
            SkillStun       => SynergyTag.Control,
            SkillSlow       => SynergyTag.Control,
            SkillPoison     => SynergyTag.Debuff,
            SkillArmorBreak => SynergyTag.Debuff,
            _               => SynergyTag.None   // 새 스킬을 만들면 여기에 한 줄 추가하세요
        };
    }

    public static string GetName(SynergyTag tag)
    {
        return tag switch
        {
            SynergyTag.Dealer  => "딜러",
            SynergyTag.Control => "제어",
            SynergyTag.Debuff  => "약화",
            SynergyTag.None    => "없음",
            _                  => tag.ToString()
        };
    }

    /// <summary>'조화'(모든 계열) 시너지 색.</summary>
    public static readonly Color HarmonyColor = new Color(0.90f, 0.66f, 0.12f);   // 금색

    /// <summary>계열 색 — 어두운 HUD 와 흰 카드 양쪽에서 읽히게 중간 밝기.</summary>
    public static Color GetColor(SynergyTag tag)
    {
        return tag switch
        {
            SynergyTag.Dealer  => new Color(0.92f, 0.32f, 0.26f),   // 빨강
            SynergyTag.Control => new Color(0.25f, 0.55f, 0.95f),   // 파랑
            SynergyTag.Debuff  => new Color(0.38f, 0.70f, 0.22f),   // 초록
            _                  => new Color(0.55f, 0.55f, 0.55f)    // 회색
        };
    }

    /// <summary>TMP 리치 텍스트용 "계열 이름에 색 입히기". 계열마다 결과가 같아 한 번만 만들고 재사용합니다.</summary>
    public static string GetColoredName(SynergyTag tag)
    {
        if (ColoredNames.TryGetValue(tag, out string cached)) return cached;

        string s = $"<color=#{ColorUtility.ToHtmlStringRGB(GetColor(tag))}>{GetName(tag)}</color>";
        ColoredNames[tag] = s;
        return s;
    }

    private static readonly Dictionary<SynergyTag, string> ColoredNames = new();
}

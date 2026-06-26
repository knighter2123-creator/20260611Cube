using UnityEngine;

[CreateAssetMenu(fileName = "CompanionData", menuName = "Companion/CompanionData")]
public class CompanionData : ScriptableObject
{
    [Header("저장용 고유 id  ※ 에셋마다 겹치지 않게! (예: comp_knight)")]
    public string id = "";

    [Header("기본 정보")]
    public string companionName = "동료";
    public Sprite icon;
    public GameObject prefab;

    [Header("탐지 범위")]
    public float detectRange = 4f;

    [Header("등급")]
    public CompanionGrade grade = CompanionGrade.Normal;

    [Header("고유 스킬 (동료가 처음부터 보유)")]
    public ActiveSkill ownedSkill; // ✅ Inspector에서 스킬 ScriptableObject 연결

#if UNITY_EDITOR
    // id를 비워두면 에셋 파일명으로 자동 채움 (수동 지정도 가능)
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id))
            id = name;
    }
#endif
}

public enum CompanionGrade
{
    Normal,
    Rare,
    Epic,
    Legendary
}
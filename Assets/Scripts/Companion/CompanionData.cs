using UnityEngine;

[CreateAssetMenu(fileName = "CompanionData", menuName = "Companion/CompanionData")]
public class CompanionData : ScriptableObject
{
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
}

public enum CompanionGrade
{
    Normal,
    Rare,
    Epic,
    Legendary
}
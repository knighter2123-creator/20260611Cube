using UnityEngine;

[CreateAssetMenu(fileName = "CompanionData", menuName = "Companion/CompanionData")]
public class CompanionData : ScriptableObject
{
    [Header("기본 정보")]
    public string companionName = "동료";
    public Sprite icon;
    public GameObject prefab;           // 씬에 배치될 프리팹

    [Header("탐지 범위")]
    public float detectRange = 8f;

    [Header("등급")]
    public CompanionGrade grade = CompanionGrade.Normal;
}

public enum CompanionGrade
{
    Normal,
    Rare,
    Epic,
    Legendary
}
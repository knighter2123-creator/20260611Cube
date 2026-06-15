using UnityEngine;

/// <summary>
/// 모든 액티브 스킬의 베이스. 상속받아 Execute()를 구현하세요.
/// </summary>
public abstract class ActiveSkill : ScriptableObject
{
    [Header("스킬 기본 정보")]
    public string skillName    = "스킬";
    public float  cooldown     = 5f;
    public float  damage       = 30f;
    public Sprite icon;                 // UI 아이콘

    /// <summary>동료가 스킬을 사용할 때 호출됩니다.</summary>
    public abstract void Execute(Enemy target, Companion caster);
}
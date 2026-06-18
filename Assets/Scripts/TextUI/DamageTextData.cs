using UnityEngine;

[CreateAssetMenu(fileName = "DamageTextData", menuName = "Game/Damage Text Data")]
public class DamageTextData : ScriptableObject
{
    [Header("기본 설정")]
    public float lifetime = 1.2f;
    public float riseHeight = 1.5f;
    public AnimationCurve riseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve fadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    [Header("폰트 크기")]
    public float normalFontSize = 4f;
    public float criticalFontSize = 6f;

    [Header("색상")]
    public Color normalColor = Color.white;
    public Color criticalColor = new Color(1f, 0.4f, 0.1f);
    public Color healColor = new Color(0.2f, 1f, 0.4f);
    public Color dotColor = new Color(1f, 0.8f, 0.1f);

    [Header("오프셋 (적 머리 위 위치)")]
    public Vector3 spawnOffset = new Vector3(0f, 2f, 0f);

    [Header("랜덤 흔들림")]
    public float randomXRange = 0.3f;
}

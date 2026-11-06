using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>스킬 수치 하나를 등급별로 다르게 주는 칸 — 체크 끔: 모든 등급이 원래 숫자(일반 값), 켬: 희귀/영웅/전설은 여기 값 (원래 필드 타입을 바꾸면 에셋 값이 날아가 옆에 추가, OnValidate 수정을 위해 class).</summary>
[Serializable]
public class GradeFloat
{
    [Tooltip("체크하면 희귀/영웅/전설이 아래 값을 씁니다. 끄면 모든 등급이 기본값(일반) 사용")]
    public bool  enabled;
    public float rare;
    public float epic;
    public float legendary;

    /// <summary>등급에 맞는 값 (normalValue = 에셋의 원래 숫자 = 일반 값).</summary>
    public float Get(float normalValue, CompanionGrade grade)
    {
        if (!enabled) return normalValue;

        switch (grade)
        {
            case CompanionGrade.Rare:      return rare;
            case CompanionGrade.Epic:      return epic;
            case CompanionGrade.Legendary: return legendary;
            default:                       return normalValue;   // Normal (그리고 나중에 추가될 등급의 안전한 기본값)
        }
    }

    /// <summary>[에디터] 체크를 막 켰는데 세 칸이 모두 0 이면 일반 값으로 채움 (켜는 순간 0 이 되어 약해지지 않게).</summary>
    public void FillIfEmpty(float normalValue)
    {
        if (!enabled) return;
        if (rare != 0f || epic != 0f || legendary != 0f) return;

        rare = epic = legendary = normalValue;
    }

    /// <summary>[에디터 점검] 켜져 있는데 0 인 칸이 있는가 — 쿨타임 등 0 이면 실수인 값 경고용 (빈 칸이면 하한 0.1초로 스킬 난사).</summary>
    public bool HasZeroSlot()
        => enabled && (rare == 0f || epic == 0f || legendary == 0f);
}

#if UNITY_EDITOR
/// <summary>GradeFloat 를 인스펙터 한 줄로 그림(선택, 지워도 동작 동일) — UnityEditor 는 빌드에 없어 #if UNITY_EDITOR 로 감쌈.</summary>
[CustomPropertyDrawer(typeof(GradeFloat))]
public class GradeFloatDrawer : PropertyDrawer
{
    private static readonly string[] FieldNames = { "rare", "epic", "legendary" };
    private static readonly string[] Labels     = { "희귀", "영웅", "전설" };

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // BeginProperty/EndProperty 로 감싸야 프리팹 오버라이드 굵은 글씨·우클릭 메뉴가 정상 동작합니다.
        EditorGUI.BeginProperty(position, label, property);

        // 왼쪽 이름표를 그리고, 남은 영역(r)을 받습니다.
        Rect r = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // 들여쓰기·이름표 폭은 전역 설정이라, 바꿨으면 반드시 원래대로 돌려놔야 다른 칸이 안 깨집니다.
        int   oldIndent     = EditorGUI.indentLevel;
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUI.indentLevel = 0;

        SerializedProperty enabled = property.FindPropertyRelative("enabled");

        const float toggleW = 18f;
        EditorGUI.PropertyField(new Rect(r.x, r.y, toggleW, r.height), enabled, GUIContent.none);

        // 체크가 꺼져 있으면 세 칸을 회색(수정 불가)으로 — 안 쓰이는 값이라는 걸 눈으로 보여줍니다.
        using (new EditorGUI.DisabledScope(!enabled.boolValue))
        {
            float x    = r.x + toggleW + 4f;
            float cell = (r.xMax - x) / 3f;
            EditorGUIUtility.labelWidth = 28f;

            for (int i = 0; i < FieldNames.Length; i++)
            {
                Rect c = new Rect(x + cell * i, r.y, cell - 4f, r.height);
                EditorGUI.PropertyField(c, property.FindPropertyRelative(FieldNames[i]), new GUIContent(Labels[i]));
            }
        }

        EditorGUIUtility.labelWidth = oldLabelWidth;
        EditorGUI.indentLevel       = oldIndent;
        EditorGUI.EndProperty();
    }

    // 한 줄 높이
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;
}
#endif

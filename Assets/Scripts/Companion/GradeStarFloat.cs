using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>스킬 수치를 등급 × 성급 표로 주는 칸 — 체크 끔: 기존 값(원래 숫자 + 등급별), 켬: 표의 [등급, 성급] 값 (처음 켜면 각 줄이 그 등급의 지금 값으로 채워져 2~5성만 올려 적으면 됨).</summary>
[Serializable]
public class GradeStarFloat
{
    [Tooltip("체크하면 아래 표의 [등급, 성급] 값을 씁니다. 끄면 성급과 무관하게 기존 값")]
    public bool enabled;

    // 줄 = 등급, 칸 = 성급(1성 ~ MAX_STAR성). 길이는 OnValidate 가 MAX_STAR 에 맞춥니다.
    public float[] normal    = new float[CompanionStar.MAX_STAR];
    public float[] rare      = new float[CompanionStar.MAX_STAR];
    public float[] epic      = new float[CompanionStar.MAX_STAR];
    public float[] legendary = new float[CompanionStar.MAX_STAR];

    /// <summary>[등급, 성급] 값 (표가 꺼졌거나 칸이 없으면 fallback, 성급은 1~MAX_STAR 로 자름).</summary>
    public float Get(float fallback, CompanionGrade grade, int star)
    {
        if (!enabled) return fallback;

        float[] row = Row(grade);
        int i = Mathf.Clamp(star, CompanionStar.MIN_STAR, CompanionStar.MAX_STAR) - 1;
        return row != null && i < row.Length ? row[i] : fallback;
    }

    private float[] Row(CompanionGrade grade) => grade switch
    {
        CompanionGrade.Rare      => rare,
        CompanionGrade.Epic      => epic,
        CompanionGrade.Legendary => legendary,
        _                        => normal
    };

    /// <summary>[에디터] 줄 길이를 MAX_STAR 에 맞추고, 막 켰는데 줄이 모두 0 이면 그 등급의 지금 값으로 채움.</summary>
    public void FillIfEmpty(Func<CompanionGrade, float> gradeValue)
    {
        normal    = Resize(normal);
        rare      = Resize(rare);
        epic      = Resize(epic);
        legendary = Resize(legendary);

        if (!enabled) return;

        FillRowIfZero(normal,    gradeValue(CompanionGrade.Normal));
        FillRowIfZero(rare,      gradeValue(CompanionGrade.Rare));
        FillRowIfZero(epic,      gradeValue(CompanionGrade.Epic));
        FillRowIfZero(legendary, gradeValue(CompanionGrade.Legendary));
    }

    /// <summary>[에디터 점검용] 체크가 켜져 있는데 0 인 칸이 있는지 (쿨타임처럼 0 이면 거의 확실히 실수인 값).</summary>
    public bool HasZeroSlot()
        => enabled && (HasZero(normal) || HasZero(rare) || HasZero(epic) || HasZero(legendary));

    private static float[] Resize(float[] row)
    {
        if (row != null && row.Length == CompanionStar.MAX_STAR) return row;

        var next = new float[CompanionStar.MAX_STAR];
        if (row != null) Array.Copy(row, next, Mathf.Min(row.Length, next.Length));
        return next;
    }

    private static void FillRowIfZero(float[] row, float value)
    {
        foreach (float v in row) if (v != 0f) return;
        for (int i = 0; i < row.Length; i++) row[i] = value;
    }

    private static bool HasZero(float[] row)
    {
        if (row == null) return true;
        foreach (float v in row) if (v == 0f) return true;
        return false;
    }
}

#if UNITY_EDITOR
/// <summary>GradeStarFloat 를 인스펙터에 등급×성급 표로 그림.</summary>
[CustomPropertyDrawer(typeof(GradeStarFloat))]
public class GradeStarFloatDrawer : PropertyDrawer
{
    private static readonly string[] RowNames  = { "normal", "rare", "epic", "legendary" };
    private static readonly string[] RowLabels = { "일반", "희귀", "영웅", "전설" };

    private const float RowLabelW = 36f;
    private const float Gap       = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float step = line + Gap;

        // 1줄: 이름표 + 체크
        Rect head = new Rect(position.x, position.y, position.width, line);
        Rect r    = EditorGUI.PrefixLabel(head, GUIUtility.GetControlID(FocusType.Passive), label);

        int oldIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        SerializedProperty enabled = property.FindPropertyRelative("enabled");
        EditorGUI.PropertyField(new Rect(r.x, r.y, 18f, line), enabled, GUIContent.none);

        // 표는 체크가 켜져 있을 때만 그립니다 (꺼져 있으면 쓰이지 않는 값이라 접어 둠)
        if (enabled.boolValue)
        {
            float x     = position.x + EditorGUIUtility.labelWidth * 0.35f;
            float width = position.xMax - x;
            float cell  = (width - RowLabelW) / CompanionStar.MAX_STAR;

            // 2줄: 성급 머리글
            float y = position.y + step;
            for (int s = 0; s < CompanionStar.MAX_STAR; s++)
                EditorGUI.LabelField(new Rect(x + RowLabelW + cell * s, y, cell - Gap, line), $"{s + 1}성",
                                     EditorStyles.centeredGreyMiniLabel);

            // 3~6줄: 등급별 값
            for (int g = 0; g < RowNames.Length; g++)
            {
                y += step;
                EditorGUI.LabelField(new Rect(x, y, RowLabelW, line), RowLabels[g]);

                SerializedProperty row = property.FindPropertyRelative(RowNames[g]);
                if (row.arraySize != CompanionStar.MAX_STAR) row.arraySize = CompanionStar.MAX_STAR;

                for (int s = 0; s < CompanionStar.MAX_STAR; s++)
                    EditorGUI.PropertyField(new Rect(x + RowLabelW + cell * s, y, cell - Gap, line),
                                            row.GetArrayElementAtIndex(s), GUIContent.none);
            }
        }

        EditorGUI.indentLevel = oldIndent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float step  = EditorGUIUtility.singleLineHeight + Gap;
        bool  shown = property.FindPropertyRelative("enabled").boolValue;
        return shown ? step * (2 + RowNames.Length) - Gap : EditorGUIUtility.singleLineHeight;
    }
}
#endif

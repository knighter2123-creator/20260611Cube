using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 스킬 수치 하나를 '동료 등급별'로 다르게 줄 수 있게 해 주는 칸. (신규)
///
/// ★ 사용 방식
///   스킬 에셋에는 원래 있던 숫자(예: damage = 20)가 그대로 남습니다. 그 숫자가 '일반 등급 값'입니다.
///   그 옆에 이 칸(damageByGrade)을 두고,
///     - 체크 끔  → 모든 등급이 원래 숫자를 씀 (지금과 완전히 같음)
///     - 체크 켬  → 희귀 / 영웅 / 전설은 여기에 적은 값을 씀. 일반은 여전히 원래 숫자
///
/// ★ 왜 원래 float 필드(damage)를 이 타입으로 '바꾸지' 않았나
///   유니티는 필드 타입이 바뀌면 저장돼 있던 값을 읽지 못해 기본값으로 초기화합니다.
///   (int → long 으로 바꿨다가 세이브 값이 0 이 됐던 것과 같은 함정)
///   damage 를 GradeFloat 로 바꾸면 모든 스킬 에셋의 피해 값이 조용히 날아갑니다.
///   그래서 원래 필드는 그대로 두고, 등급별 값만 '옆에 추가'했습니다.
///
/// ★ 왜 struct 가 아니라 class 인가
///   OnValidate 에서 FillIfEmpty() 로 안의 값을 고쳐 써야 하는데,
///   struct 는 복사본이 넘어가서 원본이 안 바뀌는 실수를 하기 쉽습니다. class 는 참조라 안전합니다.
/// </summary>
[Serializable]
public class GradeFloat
{
    [Tooltip("체크하면 희귀/영웅/전설이 아래 값을 씁니다. 끄면 모든 등급이 기본값(일반) 사용")]
    public bool  enabled;
    public float rare;
    public float epic;
    public float legendary;

    /// <summary>
    /// 등급에 맞는 값을 돌려줍니다.
    /// normalValue = 스킬 에셋의 원래 숫자(일반 등급 값).
    /// </summary>
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

    /// <summary>
    /// [에디터 편의] 체크를 막 켰는데 세 칸이 전부 0 이면 일반 값으로 채워 줍니다.
    /// 체크하는 순간 '전설 피해 0' 이 되어 테스트 중 동료가 갑자기 약해지는 걸 막습니다.
    /// (세 칸 중 하나라도 값을 넣었다면 건드리지 않습니다)
    /// </summary>
    public void FillIfEmpty(float normalValue)
    {
        if (!enabled) return;
        if (rare != 0f || epic != 0f || legendary != 0f) return;

        rare = epic = legendary = normalValue;
    }

    /// <summary>
    /// [에디터 점검용] 체크가 켜져 있는데 0 인 칸이 있는지.
    /// 쿨타임·독 간격처럼 '0 이면 거의 확실히 실수' 인 값에서 경고를 띄우는 데 씁니다.
    /// (예: 영웅 칸만 비워두면 영웅 동료 쿨타임이 하한 0.1초가 되어 스킬을 난사합니다)
    /// </summary>
    public bool HasZeroSlot()
        => enabled && (rare == 0f || epic == 0f || legendary == 0f);
}

#if UNITY_EDITOR
/// <summary>
/// 인스펙터에서 GradeFloat 를 한 줄로 그려 줍니다. (선택 — 이 부분을 지워도 동작은 같고, 접이식으로 보일 뿐)
///
///   Damage By Grade   [✓]  희귀 [30]  영웅 [45]  전설 [70]
///
/// ★ #if UNITY_EDITOR 로 감싼 이유
///   UnityEditor 네임스페이스는 빌드에 포함되지 않습니다. 감싸지 않으면 모바일 빌드가 컴파일 에러로 실패합니다.
///   (보통은 Editor 폴더에 따로 두지만, 파일 하나로 끝내려고 여기에 같이 넣었습니다)
/// </summary>
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

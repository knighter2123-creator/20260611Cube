using System;
using UnityEngine;

/// <summary>튜토리얼 단계의 강조 대상 종류.</summary>
public enum TutorialTargetType
{
    None,   // 강조 없이 화면 전체를 어둡게 + 가운데 설명 (시작/마무리 인사 등)
    UI,     // 캔버스 위의 UI (스테이지 카운트, 메뉴 버튼 등) → RectTransform
    World,  // 월드 오브젝트 (플레이어 타워, 몬스터 생성 지점 등) → Transform

    // 플레이어 사거리 범위 (연결 불필요)
    // ⚠ 새 값은 맨 뒤에만 추가 — enum 은 숫자로 저장돼 중간 삽입 시 기존 단계 타입이 밀림
    PlayerRange,
}

/// <summary>튜토리얼 한 단계 데이터 — 씬 오브젝트를 참조해야 해서 SO 대신 [Serializable] 로 TutorialManager 리스트에 둠.</summary>
[Serializable]
public class TutorialStep
{
    [Tooltip("인스펙터에서 구분하기 위한 이름 (화면에는 안 나옵니다)")]
    public string label;

    [TextArea(2, 5)]
    public string message;

    public TutorialTargetType targetType = TutorialTargetType.None;

    [Tooltip("targetType = UI 일 때 강조할 RectTransform")]
    public RectTransform uiTarget;

    [Tooltip("targetType = World 일 때 강조할 Transform")]
    public Transform worldTarget;

    [Tooltip("World 타겟: 켜면 SpriteRenderer 크기에 맞춰 강조합니다.\n" +
             "자식에 사거리 원 같은 큰 스프라이트가 있어 구멍이 너무 커지면 끄고 World Size 를 쓰세요.")]
    public bool useRendererBounds = true;

    [Tooltip("World 타겟에 Renderer 가 없거나 useRendererBounds 를 껐을 때 쓰는 월드 크기(유닛)")]
    public Vector2 worldSize = new Vector2(1.5f, 1.5f);

    [Tooltip("강조 구멍 바깥 여백 (캔버스 단위)")]
    public float padding = 20f;
}
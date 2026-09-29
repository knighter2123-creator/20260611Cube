using System;
using UnityEngine;

/// <summary>
/// 튜토리얼 한 단계가 "무엇을 강조할지"의 종류.
/// </summary>
public enum TutorialTargetType
{
    None,   // 강조 없이 화면 전체를 어둡게 + 가운데 설명 (시작/마무리 인사 등)
    UI,     // 캔버스 위의 UI (스테이지 카운트, 메뉴 버튼 등) → RectTransform
    World,  // 월드 오브젝트 (플레이어 타워, 몬스터 생성 지점 등) → Transform

    // ★ 추가 — 플레이어 사거리(Player.Instance.stat.attackRange) 범위. 연결할 것 없음.
    //   반드시 맨 뒤에 추가해야 합니다: 유니티는 enum 을 "이름"이 아니라 "숫자(0,1,2…)"로 저장하기 때문에
    //   중간에 끼워 넣으면 이미 설정해 둔 단계들의 타입이 한 칸씩 밀립니다.
    PlayerRange,
}

/// <summary>
/// 튜토리얼 한 단계의 데이터.
///
/// ★ 왜 ScriptableObject 가 아니라 [Serializable] 클래스인가?
///   이 프로젝트는 데이터를 보통 ScriptableObject 로 빼지만,
///   튜토리얼 단계는 "씬 안의 오브젝트(타워, HUD 버튼)"를 직접 참조해야 합니다.
///   ScriptableObject(에셋)는 씬 오브젝트를 참조할 수 없으므로(저장해도 빠짐),
///   씬에 있는 TutorialManager 의 인스펙터 리스트에 두는 게 맞습니다.
/// </summary>
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
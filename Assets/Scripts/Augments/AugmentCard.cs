using UnityEngine;

/// <summary>
/// 증강 카드 등급. UI 테두리 색과 기본 추첨 가중치를 결정합니다.
/// </summary>
public enum AugmentRarity
{
    Common,     // 흰색  — 무난한 소폭 상승
    Rare,       // 파랑  — 눈에 띄는 상승
    Epic,       // 보라  — 판을 바꾸는 카드
    Legendary   // 금색  — 아주 드물게
}

/// <summary>
/// 모든 증강 카드의 부모 클래스.
///
/// [왜 ScriptableObject 인가]
/// 카드 한 종류 = 에셋 파일 한 개가 됩니다.
/// "공격력 +15%" 와 "공격력 +30%" 를 만들고 싶으면 스크립트를 고치는 게 아니라
/// 같은 스크립트로 에셋을 2개 만들고 인스펙터 값만 다르게 넣으면 됩니다.
/// 프로젝트의 ActiveSkill / WorldEnemySet 과 완전히 같은 설계 방식입니다.
///
/// [abstract 인 이유]
/// AugmentCard 자체는 "효과가 뭔지" 모릅니다. 그건 자식 클래스가 정합니다.
/// 덕분에 AugmentManager 는 카드 종류를 하나도 모른 채 card.Apply() 한 줄만 부르면 됩니다.
/// 나중에 카드를 30종으로 늘려도 매니저 코드는 그대로입니다. (개방-폐쇄 원칙)
/// </summary>
public abstract class AugmentCard : ScriptableObject
{
    [Header("식별")]
    [Tooltip("저장 파일에 기록되는 고유 ID. 한 번 정하면 바꾸지 마세요. (바꾸면 저장된 증강이 사라집니다)")]
    [SerializeField] private string id = "";

    [Header("표시")]
    [SerializeField] private string displayName = "이름 없음";

    [Tooltip("{0} 자리에 수치가 자동으로 들어갑니다. 예) 공격력이 {0} 증가합니다")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "설명";

    [SerializeField] private Sprite icon;
    [SerializeField] private AugmentRarity rarity = AugmentRarity.Common;

    [Header("추첨")]
    [Tooltip("클수록 자주 등장. 0이면 절대 등장하지 않음")]
    [Min(0f)]
    [SerializeField] private float weight = 100f;

    [Tooltip("영구 카드가 최대 몇 번까지 중복으로 나올 수 있는지. 0 = 무제한")]
    [Min(0)]
    [SerializeField] private int maxStack = 0;

    // ── 외부에서 읽기만 가능한 프로퍼티 ────────────────────────────
    // 필드를 public 으로 열지 않고 프로퍼티로 감싸는 이유:
    // 다른 스크립트가 실수로 값을 덮어쓰는 사고를 원천 차단하기 위해서입니다.
    public string        Id          => string.IsNullOrEmpty(id) ? name : id;
    public string        DisplayName => displayName;
    public Sprite        Icon        => icon;
    public AugmentRarity Rarity      => rarity;
    public float         Weight      => weight;
    public int           MaxStack    => maxStack;

    /// <summary>
    /// true = 한 번 고르면 계속 남는 카드 (저장 대상)
    /// false = 그 자리에서 끝나는 카드 (골드 지급) 또는 지속시간이 있는 임시 버프
    /// 기본값은 자식 클래스가 override 로 정합니다.
    /// </summary>
    public abstract bool IsPermanent { get; }

    /// <summary>
    /// 카드에 표시할 최종 설명문. description 의 {0} 에 수치가 채워집니다.
    /// </summary>
    public virtual string GetDescription()
    {
        // string.Format 은 {0} 이 없으면 그냥 원문을 돌려주므로 안전합니다.
        return string.Format(description, GetValueText());
    }

    /// <summary>설명문의 {0} 자리에 들어갈 문자열. 자식이 각자 형식을 정합니다.</summary>
    protected abstract string GetValueText();

    /// <summary>
    /// 플레이어가 이 카드를 골랐을 때 실제로 벌어지는 일.
    /// 영구 카드는 manager 에 스택을 쌓고, 즉시 카드는 여기서 보상을 지급합니다.
    /// </summary>
    /// <param name="manager">상태를 보관하는 허브</param>
    /// <param name="isRestore">
    /// 저장 파일에서 복구 중인지 여부.
    /// true 일 때 골드 지급 같은 "1회성 보상"을 또 주면 무한 골드 버그가 되므로,
    /// 즉시형 카드는 이 값이 true 면 아무것도 하지 않아야 합니다.
    /// </param>
    public abstract void Apply(AugmentManager manager, bool isRestore);

    /// <summary>
    /// 영구 카드가 "지금 이 순간 스탯에 얼마를 기여하는지" 반영하는 함수.
    ///
    /// [왜 Apply 와 따로 두는가]
    /// 영구 배율은 게임을 껐다 켜면 다시 계산해야 하고, 스택이 3개면 3번 반영돼야 합니다.
    /// 그런데 Apply 안에서 배율을 직접 더하면 "스택을 쌓는 일"과 "배율을 더하는 일"이 섞여
    /// 복구할 때 스택이 중복으로 쌓이는 버그가 납니다.
    ///
    /// 그래서 역할을 나눴습니다.
    ///   Apply               → 스택을 1 올린다 (한 번만)
    ///   ContributePermanent → 스택 수만큼 반복 호출되며 배율을 더한다 (매번 처음부터 다시)
    ///
    /// 즉시형·임시형 카드는 이 함수를 그대로 비워두면 됩니다.
    /// </summary>
    public virtual void ContributePermanent(AugmentManager manager) { }

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 에셋을 만들거나 값을 고칠 때 자동 호출됩니다.
    /// id 를 비워두면 파일 이름으로 한 번 채워줘서 실수를 줄입니다.
    /// </summary>
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
#endif
}

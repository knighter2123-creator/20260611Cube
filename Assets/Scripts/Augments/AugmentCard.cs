using UnityEngine;

/// <summary>증강 카드 등급 — UI 테두리 색과 기본 추첨 가중치를 결정.</summary>
public enum AugmentRarity
{
    Common,     // 흰색  — 무난한 소폭 상승
    Rare,       // 파랑  — 눈에 띄는 상승
    Epic,       // 보라  — 판을 바꾸는 카드
    Legendary   // 금색  — 아주 드물게
}

/// <summary>증강 카드 부모(ScriptableObject — 카드 한 종류 = 에셋 하나, 수치만 다르면 에셋만 추가) — 효과는 자식이 Apply 로 정해 매니저는 카드 종류를 몰라도 됨.</summary>
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

    // ── 읽기 전용 프로퍼티 (다른 스크립트가 실수로 덮어쓰지 못하게) ──
    public string        Id          => string.IsNullOrEmpty(id) ? name : id;
    public string        DisplayName => displayName;
    public Sprite        Icon        => icon;
    public AugmentRarity Rarity      => rarity;
    public float         Weight      => weight;
    public int           MaxStack    => maxStack;

    /// <summary>true = 계속 남는 카드(저장 대상), false = 즉시 보상이나 임시 버프 — 자식이 정함.</summary>
    public abstract bool IsPermanent { get; }

    /// <summary>카드 설명문 — description 의 {0} 에 수치를 채움.</summary>
    public virtual string GetDescription()
    {
        // 인스펙터 입력의 잘못된 중괄호({1}, '{')는 FormatException → 카드 UI 생성 중이면 창이 열린 채 굳으므로 받아냄
        try
        {
            return string.Format(description ?? string.Empty, GetValueText());
        }
        catch (System.FormatException)
        {
            Debug.LogWarning($"[AugmentCard] '{name}' 설명문의 중괄호 형식이 잘못됐습니다. " +
                             "수치 자리는 {0} 만 쓸 수 있고, 중괄호 글자 자체는 {{ }} 로 적으세요.", this);
            return description;   // 수치 없이 원문이라도 보여 줍니다
        }
    }

    /// <summary>설명문의 {0} 자리에 들어갈 문자열. 자식이 각자 형식을 정합니다.</summary>
    protected abstract string GetValueText();

    /// <summary>카드를 골랐을 때의 효과 — 영구 카드는 스택을 쌓고, 즉시 카드는 보상 지급.</summary>
    /// <param name="manager">상태를 보관하는 허브</param>
    /// <param name="isRestore">저장에서 복구 중 — ⚠ true 면 즉시형은 아무것도 하지 말 것 (또 주면 무한 골드)</param>
    public abstract void Apply(AugmentManager manager, bool isRestore);

    /// <summary>영구 카드의 스탯 기여 — 스택 수만큼 매번 처음부터 다시 더함 (Apply 는 스택만 +1, 섞으면 복구 때 중복으로 쌓임 / 즉시·임시형은 비워 둠).</summary>
    public virtual void ContributePermanent(AugmentManager manager) { }

#if UNITY_EDITOR
    /// <summary>에디터에서 id 가 비어 있으면 파일 이름으로 채움.</summary>
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(id)) id = name;
    }
#endif
}

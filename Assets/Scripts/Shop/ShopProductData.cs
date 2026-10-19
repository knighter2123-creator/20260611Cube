using UnityEngine;
using Manager.currency;

/// <summary>
/// 상점 상품 1개를 정의하는 ScriptableObject.
/// Create → Shop → Shop Product 로 에셋 생성.
/// 상품을 늘릴 때 코드 수정 없이 에셋만 추가하면 됨.
///
/// [왜 ScriptableObject 인가]
///   상품 = 데이터입니다. 코드에 상품 목록을 박아 두면 가격 하나 바꿀 때마다 빌드를 다시 해야 하지만,
///   에셋으로 두면 기획자가 인스펙터에서 숫자만 고치면 됩니다. (데이터와 로직의 분리)
///
/// [사용 흐름]
///   ShopManager.products 리스트에 등록 → ShopProductList 가 Category 로 골라냄
///   → ShopItemUI 가 표시 → 구매 버튼 → ShopManager.TryPurchase 가 Cost/Reward 를 읽어 처리
/// </summary>
[CreateAssetMenu(fileName = "ShopProduct", menuName = "Shop/Shop Product")]
public class ShopProductData : ScriptableObject
{
    [SerializeField] private string id;
    [Tooltip("탭 분류. ShopProductList 의 category 와 철자·대소문자까지 똑같아야 합니다. 예) Gold, Gem, Gacha")]
    [SerializeField] private string category = "Gold";
    [SerializeField] private string displayName;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private Sprite icon;              // 상품 아이콘(보상 더미 그림 등)

    [Header("비용 (구매에 사용하는 재화)")]
    [SerializeField] private CurrencyType costType = CurrencyType.Gem;
    [Min(1)] [SerializeField] private int costAmount = 3000;   // [Min] — 인스펙터에서 0·음수 입력을 막음
    [SerializeField] private string cashPriceText = "";        // IAP 표기용 (예: "₩3,900")

    [Header("보상 (구매 시 얻는 재화)")]
    [Tooltip("Cash 는 보상으로 쓸 수 없습니다 (지급할 곳이 없음)")]
    [SerializeField] private CurrencyType rewardType = CurrencyType.Gold;
    [Min(1)] [SerializeField] private int rewardAmount = 100000;

    [Tooltip("(선택) 보상 수량 옆에 표시할 이미지.\n" +
             "비워 두면 보상 재화 종류에 맞는 아이콘(CurrencyIconTable)을 자동으로 씁니다 — 골드면 골드, 소환권이면 소환권.")]
    [SerializeField] private Sprite rewardIcon;

    // ── 읽기 전용 프로퍼티 ─────────────────────────────────────
    // 필드는 private + [SerializeField] 로 인스펙터에만 열고, 코드에는 읽기만 허용합니다.
    // 런타임에 누가 가격을 바꿔 버리면 에셋 파일 자체가(에디터에서는) 바뀌는 사고가 나기 때문입니다.
    public string Id => id;
    public string Category => category;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public CurrencyType CostType => costType;
    public int CostAmount => costAmount;
    public string CashPriceText => cashPriceText;
    public CurrencyType RewardType => rewardType;
    public int RewardAmount => rewardAmount;
    public Sprite RewardIcon => rewardIcon;   // 비어 있으면 null — 표시하는 쪽이 재화 아이콘으로 대체

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 값을 고칠 때마다 자동 호출됩니다 (빌드에는 포함되지 않음).
    /// 런타임에 터질 설정 실수를 "만드는 순간" 잡아내는 용도입니다.
    /// </summary>
    private void OnValidate()
    {
        // CompanionData와 동일하게 id 자동 채움
        if (string.IsNullOrEmpty(id)) id = name;

        // ShopManager.TryPurchase 가 막아 주긴 하지만, 구매 시점보다 지금 아는 편이 낫습니다.
        if (rewardType == CurrencyType.Cash)
            Debug.LogWarning($"[ShopProductData] '{name}': 보상 재화를 Cash 로 둘 수 없습니다. 구매가 항상 실패합니다.", this);

        // 앞뒤 공백 때문에 탭에 안 나오는 실수가 흔해서 미리 잘라 둡니다.
        if (category != null) category = category.Trim();
    }
#endif
}

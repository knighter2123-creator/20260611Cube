using UnityEngine;
using Manager.currency;

/// <summary>상점 상품 1개 SO — 데이터로 빼 상품 추가·가격 조정은 에셋만 수정, ShopManager.products 에 등록하면 목록·카드·구매가 읽어 씀.</summary>
// [생성] Create → Shop → Shop Product
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

    // ── 읽기 전용 프로퍼티 (런타임에 바꾸면 에디터에선 에셋 파일 자체가 바뀌어서) ──
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
    /// <summary>에디터에서 값을 고칠 때 자동 검증 (설정 실수를 만드는 순간 잡음, 빌드 미포함).</summary>
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

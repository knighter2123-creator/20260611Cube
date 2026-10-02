using UnityEngine;
using Manager.currency;

/// <summary>
/// 상점 상품 1개를 정의하는 ScriptableObject.
/// Create → Shop → Shop Product 로 에셋 생성.
/// 상품을 늘릴 때 코드 수정 없이 에셋만 추가하면 됨.
/// </summary>
[CreateAssetMenu(fileName = "ShopProduct", menuName = "Shop/Shop Product")]
public class ShopProductData : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string category = "Gold"; // 탭 분류: "Gold","Gem","Gacha" 등
    [SerializeField] private string displayName;
    [TextArea] [SerializeField] private string description;
    [SerializeField] private Sprite icon;              // 상품 아이콘(보상 더미 그림 등)

    [Header("비용 (구매에 사용하는 재화)")]
    [SerializeField] private CurrencyType costType = CurrencyType.Gem;
    [SerializeField] private int costAmount = 3000;
    [SerializeField] private string cashPriceText = "";   // IAP 표기용 (예: "₩3,900")

    [Header("보상 (구매 시 얻는 재화)")]
    [SerializeField] private CurrencyType rewardType = CurrencyType.Gold;
    [SerializeField] private int rewardAmount = 100000;

    [Tooltip("(선택) 보상 수량 옆에 표시할 이미지.\n" +
             "비워 두면 보상 재화 종류에 맞는 아이콘(CurrencyIconTable)을 자동으로 씁니다 — 골드면 골드, 소환권이면 소환권.")]
    [SerializeField] private Sprite rewardIcon;

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

    private void OnValidate()
    {
        // CompanionData와 동일하게 id 자동 채움
        if (string.IsNullOrEmpty(id)) id = name;
    }
}
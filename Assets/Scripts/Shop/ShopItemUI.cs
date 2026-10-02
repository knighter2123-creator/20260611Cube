using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager.currency;

/// <summary>
/// 상품 카드 1개. ShopProductList가 Instantiate 후 Setup으로 데이터 주입.
/// 가격(costIcon/costText)은 HUD의 CurrencyManager Gem 표시와 무관하게,
/// 각 상품의 CostType(아이콘) + CostAmount(숫자)를 따른다.
/// 보상(rewardImage/rewardText)도 같은 방식 — 상품의 RewardType(아이콘) + RewardAmount(숫자).
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    [Header("상품 정보")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    [Header("보상(얻는 재화) 표시")]
    [SerializeField] private Image rewardImage;           // 상품별 보상 재화 이미지 (골드 / 소환권 …)
    [SerializeField] private TMP_Text rewardText;

    [Header("가격(소모 재화) 표시")]
    [SerializeField] private CurrencyIconTable iconTable; // 재화 아이콘 매핑 (가격·보상 공용)
    [SerializeField] private Image costIcon;              // 상품별 소모 재화 이미지
    [SerializeField] private TMP_Text costText;           // 상품별 가격 텍스트

    [SerializeField] private Button buyButton;

    private ShopProductData product;

    public void Setup(ShopProductData data)
    {
        product = data;
        if (data == null) return;

        if (iconImage != null) iconImage.sprite = data.Icon;
        if (nameText  != null) nameText.text    = data.DisplayName;

        ApplyReward(data);
        ApplyCost(data);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnClickBuy);
        }
    }

    private void ApplyReward(ShopProductData data)
    {
        if (rewardText != null) rewardText.text = "+" + data.RewardAmount.ToString("N0");

        // 상품에 직접 지정한 이미지가 우선, 없으면 보상 재화 종류의 아이콘
        // (Sprite 는 Unity 오브젝트라 ?? 대신 != null 로 검사)
        Sprite s = data.RewardIcon != null ? data.RewardIcon : CurrencyIcon(data.RewardType);
        SetIcon(rewardImage, s);
    }

    private void ApplyCost(ShopProductData data)
    {
        // 현금 상품: 아이콘 숨기고 ₩ 텍스트만
        if (data.CostType == CurrencyType.Cash)
        {
            if (costIcon != null) costIcon.enabled = false;
            if (costText != null) costText.text = data.CashPriceText;
            return;
        }

        // 인게임 재화: 상품별 아이콘 + 가격
        SetIcon(costIcon, CurrencyIcon(data.CostType));
        if (costText != null)
            costText.text = data.CostAmount.ToString("N0");
    }

    private Sprite CurrencyIcon(CurrencyType type) => iconTable != null ? iconTable.Get(type) : null;

    // 그림이 없으면 Image 를 끕니다 — sprite 가 null 인 Image 는 흰 사각형으로 그려지기 때문
    private static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite  = sprite;
        image.enabled = sprite != null;
    }

    // 결과 로그는 ShopManager 가 성공/실패 각각 남깁니다.
    private void OnClickBuy()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.TryPurchase(product);
    }
}

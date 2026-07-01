using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager.currency;

/// <summary>
/// 상품 카드 1개. ShopProductList가 Instantiate 후 Setup으로 데이터 주입.
/// 가격(costIcon/costText)은 HUD의 CurrencyManager Gem 표시와 무관하게,
/// 각 상품의 CostType(아이콘) + CostAmount(숫자)를 따른다.
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    [Header("상품 정보")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rewardText;

    [Header("가격(소모 재화) 표시")]
    [SerializeField] private CurrencyIconTable iconTable; // 재화 아이콘 매핑
    [SerializeField] private Image costIcon;              // 상품별 소모 재화 이미지
    [SerializeField] private TMP_Text costText;           // 상품별 가격 텍스트

    [SerializeField] private Button buyButton;

    private ShopProductData product;

    public void Setup(ShopProductData data)
    {
        product = data;
        if (data == null) return;

        if (iconImage  != null) iconImage.sprite = data.Icon;
        if (nameText   != null) nameText.text    = data.DisplayName;
        if (rewardText != null) rewardText.text  = "+" + data.RewardAmount.ToString("N0");

        ApplyCost(data);

        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnClickBuy);
        }
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
        if (costIcon != null)
        {
            Sprite s = iconTable != null ? iconTable.Get(data.CostType) : null;
            costIcon.enabled = s != null;
            costIcon.sprite  = s;
        }
        if (costText != null)
            costText.text = data.CostAmount.ToString("N0");
    }

    private void OnClickBuy()
    {
        Debug.Log($"[Shop] OnClickBuy 호출 | 상품={product?.DisplayName} | UI ID={GetInstanceID()}");
        ShopManager.Instance?.TryPurchase(product);
    }
}
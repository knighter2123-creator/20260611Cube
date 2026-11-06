using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager.currency;

/// <summary>상품 카드 1개 — Setup 으로 상품의 비용·보상 종류/수량을 표시하고, 구매 가능 여부는 재화 변경 이벤트로만 갱신(매 프레임 확인은 낭비), 구매는 ShopManager.TryPurchase.</summary>
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

    [Header("구매 가능 표시")]
    [Tooltip("보유 재화가 모자라면 구매 버튼을 비활성(회색)으로 둡니다. 현금 상품도 결제 연동 전까지 비활성입니다.")]
    [SerializeField] private bool disableWhenUnaffordable = true;

    private ShopProductData product;

    // 구독한 매니저를 기억 (OnDisable 땐 Instance 가 바뀌었거나 null 일 수 있어 그 객체에서 해제)
    private CurrencyManager subscribedCurrency;

    // ── 이벤트 구독/해제는 OnEnable/OnDisable 짝으로 (특히 static 인 GachaTicket.OnChanged 는 해제 안 하면 파괴 후 콜백) ──
    private void OnEnable()
    {
        subscribedCurrency = CurrencyManager.Instance;
        if (subscribedCurrency != null)
        {
            subscribedCurrency.OnGoldChanged += OnCurrencyChanged;
            subscribedCurrency.OnGemChanged  += OnCurrencyChanged;
        }
        GachaTicket.OnChanged += OnCurrencyChanged;

        RefreshAffordable();   // 꺼져 있던 동안 바뀐 보유량 반영
    }

    private void OnDisable()
    {
        if (subscribedCurrency != null)
        {
            subscribedCurrency.OnGoldChanged -= OnCurrencyChanged;
            subscribedCurrency.OnGemChanged  -= OnCurrencyChanged;
            subscribedCurrency = null;
        }
        GachaTicket.OnChanged -= OnCurrencyChanged;
    }

    // ── 데이터 주입 — ShopProductList 가 Instantiate 직후 호출 ──
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
            // 프리팹에 인스펙터로 걸어 둔 리스너가 있어도 중복 호출되지 않게 비우고 다시 겁니다.
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(OnClickBuy);
        }

        // OnEnable 은 Setup 보다 먼저 돌아서(그때는 product 가 null) 여기서 한 번 더 반영합니다.
        RefreshAffordable();
    }

    private void ApplyReward(ShopProductData data)
    {
        if (rewardText != null) rewardText.text = "+" + data.RewardAmount.ToString("N0");

        // 상품 이미지 우선, 없으면 보상 재화 아이콘 (Sprite 는 Unity 오브젝트라 ?? 대신 != null)
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

    // ── 구매 가능 여부 ──

    // 이벤트들이 int 를 넘겨주지만 여기선 값이 필요 없습니다 — "바뀌었다"는 신호만 씁니다.
    private void OnCurrencyChanged(int _) => RefreshAffordable();

    private void RefreshAffordable()
    {
        if (!disableWhenUnaffordable || buyButton == null || product == null) return;

        // 판정은 CurrencyManager.Has 한 곳에 맡깁니다 — 소환권처럼 보관 장소가 다른 재화도 알아서 처리됩니다.
        var cm = CurrencyManager.Instance;
        bool affordable = product.CostType != CurrencyType.Cash
                       && cm != null
                       && cm.Has(product.CostType, product.CostAmount);

        buyButton.interactable = affordable;
    }

    private Sprite CurrencyIcon(CurrencyType type) => iconTable != null ? iconTable.Get(type) : null;

    // 그림이 없으면 Image 를 끕니다 — sprite 가 null 인 Image 는 흰 사각형으로 그려지기 때문
    private static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite  = sprite;
        image.enabled = sprite != null;
    }

    // 로그는 ShopManager 가, 버튼 상태는 재화 이벤트가 갱신
    private void OnClickBuy()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.TryPurchase(product);
    }
}

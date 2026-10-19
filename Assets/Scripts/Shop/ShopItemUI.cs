using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager.currency;

/// <summary>
/// 상품 카드 1개. ShopProductList가 Instantiate 후 Setup으로 데이터 주입.
/// 가격(costIcon/costText)은 HUD의 CurrencyManager Gem 표시와 무관하게,
/// 각 상품의 CostType(아이콘) + CostAmount(숫자)를 따른다.
/// 보상(rewardImage/rewardText)도 같은 방식 — 상품의 RewardType(아이콘) + RewardAmount(숫자).
///
/// [작동 순서]
///   Instantiate → (Awake/OnEnable: 재화 이벤트 구독, 아직 product 없음)
///   → Setup(data): 글자·아이콘 채우기 → 버튼 연결 → 구매 가능 여부 반영
///   → 재화가 바뀔 때마다(이벤트) RefreshAffordable → 버튼 켜기/끄기
///   → 구매 버튼 → ShopManager.TryPurchase
///   → OnDisable: 구독 해제
///
/// [왜 이벤트로 버튼 상태를 갱신하나]
///   Update 에서 매 프레임 보유량을 확인할 수도 있지만, 재화는 거의 안 바뀌는 값이라 낭비입니다.
///   "바뀌었을 때만" 알려 주는 이벤트를 들으면 비용이 0 에 가깝습니다. (관찰자 패턴)
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

    [Header("구매 가능 표시")]
    [Tooltip("보유 재화가 모자라면 구매 버튼을 비활성(회색)으로 둡니다. 현금 상품도 결제 연동 전까지 비활성입니다.")]
    [SerializeField] private bool disableWhenUnaffordable = true;

    private ShopProductData product;

    // 구독한 매니저를 기억해 두는 이유: 해제할 때 '구독했던 바로 그 객체'에서 빼야 하기 때문입니다.
    // OnDisable 시점에 CurrencyManager.Instance 가 이미 다른 것(또는 null)일 수 있습니다.
    private CurrencyManager subscribedCurrency;

    // ─────────────────────────────────────────────────────────
    //  이벤트 구독 / 해제 — OnEnable 과 OnDisable 은 항상 짝으로
    //  구독만 하고 해제를 안 하면, 이 오브젝트가 파괴된 뒤에도 매니저가 콜백을 불러 MissingReferenceException 이 납니다.
    //  특히 GachaTicket.OnChanged 는 static 이벤트라 씬이 바뀌어도 사라지지 않으므로 해제가 필수입니다.
    // ─────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────
    //  데이터 주입 — ShopProductList 가 Instantiate 직후 호출
    // ─────────────────────────────────────────────────────────
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

    // ─────────────────────────────────────────────────────────
    //  구매 가능 여부
    // ─────────────────────────────────────────────────────────

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

    // 결과 로그는 ShopManager 가 성공/실패 각각 남깁니다.
    // 구매 후 버튼 상태는 재화 변경 이벤트가 알아서 갱신하므로 여기서 따로 할 일이 없습니다.
    private void OnClickBuy()
    {
        if (ShopManager.Instance != null) ShopManager.Instance.TryPurchase(product);
    }
}

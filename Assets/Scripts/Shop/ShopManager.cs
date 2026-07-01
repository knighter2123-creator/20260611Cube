using System;
using System.Collections.Generic;
using UnityEngine;
using Manager.currency;

/// <summary>
/// 상점 상품 등록 + 구매 처리.
/// 구매: 비용 차감 → 보상 지급 → 마지막에 한 번 저장(정합성 보장).
/// CurrencyManager와 동일하게 씬 바운드(ShopScene에 배치, DontDestroyOnLoad 안 함).
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [SerializeField] private List<ShopProductData> products = new();

    public event Action<ShopProductData> OnPurchaseSuccess;
    public event Action<ShopProductData, string> OnPurchaseFailed;

    public IReadOnlyList<ShopProductData> Products => products;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>특정 카테고리(탭) 상품만 추려서 반환. ShopProductList가 사용.</summary>
    public IEnumerable<ShopProductData> GetByCategory(string category)
    {
        foreach (var p in products)
            if (p != null && p.Category == category)
                yield return p;
    }

    public bool TryPurchase(ShopProductData product)
    {
        if (product == null) return Fail(null, "상품 정보가 없습니다.");

        var cm = CurrencyManager.Instance;
        if (cm == null) return Fail(product, "재화 매니저를 찾을 수 없습니다.");

        if (product.CostType == CurrencyType.Cash)
            return Fail(product, "현금 결제는 아직 지원되지 않습니다.");   // 추후 IAP 연동

        // 1) 비용 차감 (부족하면 여기서 실패, 아무 것도 변경 안 됨)
        if (!cm.TrySpendCurrency(product.CostType, product.CostAmount))
            return Fail(product, $"{product.CostType} 이(가) 부족합니다.");

        // 2) 보상 지급
        cm.AddCurrency(product.RewardType, product.RewardAmount);

        // 3) 차감+지급이 모두 끝난 뒤 한 번만 저장
        SaveManager.Instance?.Save();

        OnPurchaseSuccess?.Invoke(product);
        Debug.Log($"[ShopManager] 구매 완료: {product.DisplayName}");
        return true;
    }

    private bool Fail(ShopProductData product, string reason)
    {
        Debug.Log($"[ShopManager] 구매 실패: {reason}");
        OnPurchaseFailed?.Invoke(product, reason);
        return false;
    }
}
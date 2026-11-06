using System;
using System.Collections.Generic;
using UnityEngine;
using Manager.currency;

/// <summary>상점 상품 등록·구매(ShopScene 바운드) — 검증 → 차감 → 지급 → 한 번 저장 → 알림 순서, 실패할 수 있는 검사는 전부 차감 전에 끝내 '돈만 나가는' 상태 방지.</summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [SerializeField] private List<ShopProductData> products = new();

    // 구매 결과를 UI 가 듣는 창구. ShopManager 는 UI 를 모르고, UI 가 ShopManager 를 구독합니다(의존 방향 한쪽).
    public event Action<ShopProductData> OnPurchaseSuccess;
    public event Action<ShopProductData, string> OnPurchaseFailed;

    // 외부에는 읽기 전용으로만 공개 — 다른 스크립트가 목록을 실수로 바꾸지 못하게
    public IReadOnlyList<ShopProductData> Products => products;

    // ── 싱글턴 — Awake 등록, OnDestroy 해제 (씬 바운드라 들어올 때마다 새로 등록) ──
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        // static 이 파괴된 오브젝트를 붙잡고 있지 않도록 정리
        if (Instance == this) Instance = null;
    }

    /// <summary>카테고리 상품만 yield 로 반환 (새 리스트 없이 하나씩).</summary>
    public IEnumerable<ShopProductData> GetByCategory(string category)
    {
        foreach (var p in products)
            if (p != null && string.Equals(p.Category, category, StringComparison.Ordinal))
                yield return p;
    }

    public bool TryPurchase(ShopProductData product)
    {
        // ── 1) 검증 — 여기까지는 아무 상태도 바꾸지 않습니다 ───────────
        if (product == null) return Fail(null, "상품 정보가 없습니다.");

        var cm = CurrencyManager.Instance;
        if (cm == null) return Fail(product, "재화 매니저를 찾을 수 없습니다.");

        if (product.CostType == CurrencyType.Cash)
            return Fail(product, "현금 결제는 아직 지원되지 않습니다.");   // 추후 IAP 연동

        // 보상이 Cash 면 차단 (AddCurrency 가 Cash 를 처리 안 해 비용만 빠짐)
        if (product.RewardType == CurrencyType.Cash)
            return Fail(product, "보상 재화 설정이 잘못된 상품입니다.");

        // 가격 0 이하는 설정 오류로 안내 (아니면 '재화 부족'으로 잘못 뜸)
        if (product.CostAmount <= 0 || product.RewardAmount <= 0)
            return Fail(product, "가격/보상 수량 설정이 잘못된 상품입니다.");

        // ── 2) 차감 — 부족하면 여기서 실패, 아무 것도 변경 안 됨 ─────────
        if (!cm.TrySpendCurrency(product.CostType, product.CostAmount))
            return Fail(product, $"{product.CostType} 이(가) 부족합니다.");

        // ── 3) 지급 — 위에서 검증을 끝냈으므로 이 줄은 실패하지 않습니다 ──
        cm.AddCurrency(product.RewardType, product.RewardAmount);

        // ── 4) 저장 — 차감+지급 후 한 번만 (중간 저장은 '차감만 저장' 위험), != null 은 가짜 null 대비 ──
        if (SaveManager.Instance != null) SaveManager.Instance.Save();

        // ── 5) 알림 ───────────────────────────────────────────────
        OnPurchaseSuccess?.Invoke(product);
        GameLog.Info($"[ShopManager] 구매 완료: {product.DisplayName}");
        return true;
    }

    // 실패 처리 한 곳 — 로그·이벤트를 같이 내보내고 false 반환 (return Fail(...) 한 줄)
    private bool Fail(ShopProductData product, string reason)
    {
        GameLog.Info($"[ShopManager] 구매 실패: {reason}");
        OnPurchaseFailed?.Invoke(product, reason);
        return false;
    }
}

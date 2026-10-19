using System;
using System.Collections.Generic;
using UnityEngine;
using Manager.currency;

/// <summary>
/// 상점 상품 등록 + 구매 처리.
/// CurrencyManager와 동일하게 씬 바운드(ShopScene에 배치, DontDestroyOnLoad 안 함).
///
/// [구매 작동 순서 — TryPurchase]
///   1) 검증   : 상품·매니저가 있는가, 결제 방식·가격·보상 설정이 올바른가
///   2) 차감   : 비용을 뺀다 (부족하면 여기서 실패 — 아직 아무 것도 바뀌지 않음)
///   3) 지급   : 보상을 준다
///   4) 저장   : 차감+지급이 모두 끝난 뒤 한 번만 저장 (정합성 보장)
///   5) 알림   : OnPurchaseSuccess 이벤트 → UI 가 버튼 상태 등을 갱신
///
/// [왜 "검증을 전부 끝낸 뒤 차감" 인가]
///   돈을 먼저 빼고 나서 문제가 발견되면 "돈은 나갔는데 물건은 안 온" 상태가 됩니다.
///   되돌리는(롤백) 코드를 짜는 것보다, 실패할 수 있는 조건을 차감 전에 모두 확인하는 편이 훨씬 안전합니다.
///   차감 이후에는 '실패할 수 없는' 일만 남겨 두는 것이 핵심입니다.
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    [SerializeField] private List<ShopProductData> products = new();

    // 구매 결과를 UI 가 듣는 창구. ShopManager 는 UI 를 모르고, UI 가 ShopManager 를 구독합니다(의존 방향 한쪽).
    public event Action<ShopProductData> OnPurchaseSuccess;
    public event Action<ShopProductData, string> OnPurchaseFailed;

    // 외부에는 읽기 전용으로만 공개 — 다른 스크립트가 목록을 실수로 바꾸지 못하게
    public IReadOnlyList<ShopProductData> Products => products;

    // ─────────────────────────────────────────────────────────
    //  싱글턴 — Awake 에서 등록, OnDestroy 에서 해제
    //  씬 바운드라 ShopScene 을 나가면 파괴되고, 다시 들어오면 새로 등록됩니다.
    // ─────────────────────────────────────────────────────────
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

    /// <summary>
    /// 특정 카테고리(탭) 상품만 추려서 반환. ShopProductList가 사용.
    /// yield return 을 쓰면 새 리스트를 만들지 않고 하나씩 꺼내 줍니다 (할당 없음).
    /// </summary>
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

        // ★ 버그 수정: 보상이 Cash 로 잘못 설정된 상품.
        //   CurrencyManager.AddCurrency 는 Cash 를 처리하지 않으므로, 예전에는
        //   "비용만 빠지고 보상은 0" 이 됐습니다. 차감 전에 막습니다.
        if (product.RewardType == CurrencyType.Cash)
            return Fail(product, "보상 재화 설정이 잘못된 상품입니다.");

        // ★ 버그 수정: 가격이 0 이하면 SpendGold/SpendGem 이 false 를 돌려줘서
        //   "재화가 부족합니다" 라는 엉뚱한 안내가 떴습니다. 설정 오류로 정확히 알려 줍니다.
        if (product.CostAmount <= 0 || product.RewardAmount <= 0)
            return Fail(product, "가격/보상 수량 설정이 잘못된 상품입니다.");

        // ── 2) 차감 — 부족하면 여기서 실패, 아무 것도 변경 안 됨 ─────────
        if (!cm.TrySpendCurrency(product.CostType, product.CostAmount))
            return Fail(product, $"{product.CostType} 이(가) 부족합니다.");

        // ── 3) 지급 — 위에서 검증을 끝냈으므로 이 줄은 실패하지 않습니다 ──
        cm.AddCurrency(product.RewardType, product.RewardAmount);

        // ── 4) 저장 — 차감+지급이 모두 끝난 뒤 한 번만 ─────────────────
        //   중간에 저장하면 "차감만 저장된" 파일이 생길 수 있습니다.
        //   (MonoBehaviour 는 '?.' 대신 '!= null' — 유니티의 가짜 null 을 제대로 거르기 위해)
        if (SaveManager.Instance != null) SaveManager.Instance.Save();

        // ── 5) 알림 ───────────────────────────────────────────────
        OnPurchaseSuccess?.Invoke(product);
        GameLog.Info($"[ShopManager] 구매 완료: {product.DisplayName}");
        return true;
    }

    // 실패 처리를 한 곳에 모아, 로그와 이벤트가 항상 같이 나가게 합니다.
    // bool 을 돌려주므로 'return Fail(...)' 한 줄로 쓸 수 있습니다.
    private bool Fail(ShopProductData product, string reason)
    {
        GameLog.Info($"[ShopManager] 구매 실패: {reason}");
        OnPurchaseFailed?.Invoke(product, reason);
        return false;
    }
}

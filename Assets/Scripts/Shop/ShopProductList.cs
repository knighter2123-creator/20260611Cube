using UnityEngine;

/// <summary>
/// 탭 패널(예: GoldPanel) 안에서 해당 카테고리 상품들을 Grid로 동적 생성.
/// Content(=contentRoot)에는 Grid Layout Group + Content Size Fitter가 깔려 있어야
/// 아이템이 늘 때 높이가 자동 확장되어 Scroll View에서 스크롤된다.
/// 패널이 켜질 때(OnEnable)마다 다시 빌드하므로 구매 후 보상 반영도 자연스럽다.
/// </summary>
public class ShopProductList : MonoBehaviour
{
    [SerializeField] private string category = "Gold"; // 이 패널이 보여줄 분류
    [SerializeField] private ShopItemUI itemPrefab;     // 빈 칸 프리팹
    [SerializeField] private Transform contentRoot;     // Grid Layout Group 부모

    private void OnEnable()
    {
        Build();
    }

    public void Build()
    {
        if (ShopManager.Instance == null || itemPrefab == null || contentRoot == null)
            return;

        // 기존 칸 정리
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        // 카테고리에 맞는 상품 찍기
        foreach (var product in ShopManager.Instance.GetByCategory(category))
        {
            var item = Instantiate(itemPrefab, contentRoot);
            item.Setup(product);
        }
    }
}
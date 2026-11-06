using UnityEngine;

/// <summary>탭 패널 안에 카테고리 상품 칸을 Grid 로 생성 — OnEnable/Start 에서 한 번만 Build(목록은 플레이 중 안 바뀌어 매번 부수고 짓는 건 낭비), 구매 가능 여부는 카드가 스스로 갱신, 구성 변경 시 Build() 직접 호출.</summary>
// ⚠ contentRoot 에 Grid Layout Group + Content Size Fitter 필요 (스크롤 높이 자동 확장)
public class ShopProductList : MonoBehaviour
{
    [Tooltip("이 패널이 보여줄 분류. ShopProductData.category 와 철자가 같아야 합니다.")]
    [SerializeField] private string category = "Gold";
    [SerializeField] private ShopItemUI itemPrefab;     // 빈 칸 프리팹
    [SerializeField] private Transform contentRoot;     // Grid Layout Group 부모

    private bool built;

    private void OnEnable() => EnsureBuilt();

    private void Start()
    {
        // Start 에서 재시도 — OnEnable 이 ShopManager.Awake 보다 먼저 돌면 Instance 가 없음
        EnsureBuilt();
    }

    private void EnsureBuilt()
    {
        if (!built) Build();
    }

    /// <summary>목록을 처음부터 다시 만듭니다. 상품 구성이 바뀌었을 때 외부에서 불러도 됩니다.</summary>
    public void Build()
    {
        // 준비가 안 됐으면 built 를 false 로 남겨 두어 다음 기회(Start/OnEnable)에 다시 시도하게 합니다.
        if (ShopManager.Instance == null || itemPrefab == null || contentRoot == null)
            return;

        // 기존 칸 정리 — 역순으로, Destroy 는 프레임 끝이라 SetActive(false) 로 레이아웃에서 즉시 제외
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        // 카테고리에 맞는 상품 찍기
        int count = 0;
        foreach (var product in ShopManager.Instance.GetByCategory(category))
        {
            var item = Instantiate(itemPrefab, contentRoot);
            item.Setup(product);
            count++;
        }

        // 빈 탭은 대개 category 철자 실수입니다. 조용히 비어 있으면 원인을 찾기 어려워 경고를 남깁니다.
        if (count == 0)
            Debug.LogWarning($"[ShopProductList] '{category}' 카테고리 상품이 없습니다. " +
                             "ShopManager 의 products 와 각 상품의 category 철자를 확인하세요.", this);

        built = true;
    }
}

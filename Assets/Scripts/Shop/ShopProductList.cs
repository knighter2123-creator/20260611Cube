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

    private bool built;

    private void OnEnable()
    {
        Build();
    }

    private void Start()
    {
        // 씬에 켜진 채 저장된 패널은 ShopManager.Awake 보다 OnEnable 이 먼저 돌 수 있습니다.
        // 그때는 Instance 가 없어 비어 있으므로, 모든 Awake 가 끝난 Start 에서 한 번 더 채웁니다.
        if (!built) Build();
    }

    public void Build()
    {
        if (ShopManager.Instance == null || itemPrefab == null || contentRoot == null)
            return;

        // 기존 칸 정리. Destroy 는 프레임 끝에 지우므로 SetActive(false) 로 레이아웃에서 즉시 빼 줍니다.
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }

        // 카테고리에 맞는 상품 찍기
        foreach (var product in ShopManager.Instance.GetByCategory(category))
        {
            var item = Instantiate(itemPrefab, contentRoot);
            item.Setup(product);
        }

        built = true;
    }
}

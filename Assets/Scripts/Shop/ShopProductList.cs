using UnityEngine;

/// <summary>
/// 탭 패널(예: GoldPanel) 안에서 해당 카테고리 상품들을 Grid로 동적 생성.
/// Content(=contentRoot)에는 Grid Layout Group + Content Size Fitter가 깔려 있어야
/// 아이템이 늘 때 높이가 자동 확장되어 Scroll View에서 스크롤된다.
///
/// [작동 순서]
///   패널이 켜짐(OnEnable) ─┐
///                          ├→ EnsureBuilt() → 아직 안 만들었으면 Build()
///   첫 프레임 직전(Start) ─┘
///   Build(): 기존 칸 정리 → ShopManager 에서 카테고리 상품을 받아 → 프리팹 Instantiate → Setup
///
/// [왜 탭을 열 때마다 다시 만들지 않나 — 이번 리팩토링]
///   예전에는 OnEnable 마다 칸을 전부 Destroy 하고 새로 Instantiate 했습니다.
///   상품 목록(에셋)은 플레이 중에 바뀌지 않으니, 탭을 오갈 때마다 같은 것을 부수고 다시 짓는 셈이었죠.
///   Instantiate/Destroy 는 비싸고 가비지도 남깁니다. 이제 한 번만 만들고,
///   "살 수 있는지(버튼 활성)" 같은 바뀌는 부분은 ShopItemUI 가 재화 이벤트로 스스로 갱신합니다.
///   상품 구성을 런타임에 바꿔야 하면 Build() 를 직접 부르면 됩니다.
/// </summary>
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
        // 씬에 켜진 채 저장된 패널은 ShopManager.Awake 보다 OnEnable 이 먼저 돌 수 있습니다.
        // (유니티는 오브젝트마다 Awake→OnEnable 을 붙여서 부르므로, 다른 오브젝트의 Awake 보다 먼저일 수 있음)
        // 그때는 Instance 가 없어 실패하므로, 모든 Awake 가 끝난 뒤인 Start 에서 한 번 더 시도합니다.
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

        // 기존 칸 정리. Destroy 는 프레임 끝에 지우므로 SetActive(false) 로 레이아웃에서 즉시 빼 줍니다.
        // (자식을 지우면서 도는 중이라 역순 — 앞에서부터 돌면 인덱스가 당겨져 하나씩 건너뜁니다)
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

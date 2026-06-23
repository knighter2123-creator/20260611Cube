using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class CompanionPlacementController : MonoBehaviour
{
    public static CompanionPlacementController Instance;
    [Header("배치 중 숨길 동료 리스트 패널")]
    [SerializeField] private GameObject companionListPanel;   // 인스펙터에서 연결
    
    [Header("배치 미리보기 (선택)")]
    [SerializeField] private SpriteRenderer ghost;        // 반투명 미리보기 스프라이트
    [SerializeField] private Color validColor   = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("안내 텍스트 / 취소 버튼 (선택)")]
    [SerializeField] private TextMeshProUGUI hintText;

    private Camera cam;
    private bool isPlacing;
    private CompanionData pendingData;
    private CompanionListItem callerItem;

    void Awake()
    {
        Instance = this;
        cam = Camera.main;
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    // CompanionListItem의 "배치" 버튼에서 호출 (기존 SlotSelectPanel.Open 대체)
    public void BeginPlacement(CompanionData data, CompanionListItem caller)
    {
        pendingData = data;
        callerItem  = caller;
        isPlacing   = true;

        if (companionListPanel != null) companionListPanel.SetActive(false); // ← 추가

        if (ghost != null)
        {
            ghost.gameObject.SetActive(true);
            if (data.icon != null) ghost.sprite = data.icon;
        }
        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            hintText.text = $"{data.companionName} — 배치할 위치를 탭하세요";
        }
    }

    public void CancelPlacement()
    {
        isPlacing   = false;
        pendingData = null;
        var caller  = callerItem;
        callerItem  = null;

        if (ghost != null)    ghost.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
        // 배치 모드가 끝나면 리스트는 그대로 닫힌 채로 둡니다(원하면 다시 켜도 됨)

        caller?.RefreshActionButtons();
    }

    void Update()
    {   
        if (!isPlacing) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
            return;
        }

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = 0f;

        if (!cm.TryGetCell(world, out Vector3Int cell)) return;

        bool canPlace = cm.CanPlaceAt(cell);

        if (ghost != null)
        {
            ghost.transform.position = cm.GetCellCenter(cell);
            ghost.color = canPlace ? validColor : invalidColor;
        }

        // UI 위 클릭은 무시 (리스트 버튼 누르다 배치되는 사고 방지)
        if (Input.GetMouseButtonDown(0) && canPlace && !IsPointerOverUI())
            ConfirmPlace(cm, cell);
    }

    private void ConfirmPlace(CompanionManager cm, Vector3Int cell)
    {
        // 이미 보유 중이면 그 인스턴스, 아니면 새로 획득
        Companion existing = cm.GetOwnedCompanions().Find(c => c.Data == pendingData);
        if (existing == null)
        {
            if (!cm.AddCompanion(pendingData)) { CancelPlacement(); return; }
            var list = cm.GetOwnedCompanions();
            existing = list[list.Count - 1];
        }

        cm.PlaceCompanion(existing, cell);
        Debug.Log($"[Placement] {pendingData.companionName} → {cell} 배치");

        CancelPlacement();
    }

    private bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
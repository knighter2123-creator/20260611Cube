using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class CompanionPlacementController : MonoBehaviour
{
    public static CompanionPlacementController Instance { get; private set; }

    [Header("배치 중 숨길 동료 리스트 패널")]
    [SerializeField] private GameObject companionListPanel;   // 인스펙터에서 연결

    [Header("배치 미리보기 (선택)")]
    [SerializeField] private SpriteRenderer ghost;        // 반투명 미리보기 스프라이트
    [SerializeField] private Color validColor   = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("안내 텍스트 / 취소 버튼 (선택)")]
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("입력")]
    [Tooltip("손가락을 뗄 때 배치합니다. 켜두면 터치를 끌면서 미리보기로 위치를 조준할 수 있습니다. " +
             "끄면 누르는 순간 즉시 배치됩니다(마우스 방식).")]
    [SerializeField] private bool placeOnRelease = true;

    [Tooltip("탭이 UI에 막혔을 때 무엇이 막았는지 콘솔에 남깁니다")]
    [SerializeField] private bool logBlockers = true;

    private Camera cam;
    private bool isPlacing;
    private bool armed;              // 배치 모드를 연 그 탭이 그대로 배치로 이어지지 않게 하는 잠금
    private CompanionData pendingData;
    private CompanionListItem callerItem;

    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);

    void Awake()
    {
        if (Instance != null && Instance != this)
            Debug.LogWarning("[Placement] 인스턴스가 둘 이상입니다. 나중 것을 사용합니다.", this);
        Instance = this;

        cam = Camera.main;
        if (ghost != null) ghost.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // CompanionListItem의 "배치" 버튼에서 호출
    public void BeginPlacement(CompanionData data, CompanionListItem caller)
    {
        if (data == null) return;

        pendingData = data;
        callerItem  = caller;
        isPlacing   = true;

        // ★ "배치" 버튼을 누른 그 입력이 같은 프레임에 배치로 이어지는 걸 막습니다.
        //   새로운 누름이 한 번 시작돼야 배치가 허용됩니다.
        armed = false;

        if (companionListPanel != null) companionListPanel.SetActive(false);

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
        armed       = false;
        pendingData = null;
        var caller  = callerItem;
        callerItem  = null;

        if (ghost != null)    ghost.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);

        caller?.RefreshActionButtons();
    }

    void Update()
    {
        if (!isPlacing) return;

        // 우클릭으로 취소 — 데스크톱/에디터 전용 (터치엔 우클릭 없음)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;

        // 씬 재로드 등으로 참조가 끊겼을 수 있으므로 다시 확보
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // 마우스 + 터치 통합 (Pointer는 둘의 상위 추상)
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        if (pointer.press.wasPressedThisFrame) armed = true;

        Vector2 screenPos = pointer.position.ReadValue();
        Vector3 world = cam.ScreenToWorldPoint(screenPos);
        world.z = 0f;

        bool hasCell  = cm.TryGetCell(world, out Vector3Int cell);
        bool canPlace = hasCell && cm.CanPlaceAt(cell);

        // 미리보기
        if (ghost != null)
        {
            if (ghost.gameObject.activeSelf != hasCell)
                ghost.gameObject.SetActive(hasCell);

            if (hasCell)
            {
                ghost.transform.position = cm.GetCellCenter(cell);
                ghost.color = canPlace ? validColor : invalidColor;
            }
        }

        if (!armed) return;

        bool triggered = placeOnRelease
            ? pointer.press.wasReleasedThisFrame
            : pointer.press.wasPressedThisFrame;

        if (!triggered || !canPlace) return;

        // UI 위 탭은 무시 (리스트 버튼 누르다 배치되는 사고 방지)
        if (IsPointerOverUI(screenPos)) return;

        ConfirmPlace(cm, cell);
    }

    private void ConfirmPlace(CompanionManager cm, Vector3Int cell)
    {
        if (pendingData == null) { CancelPlacement(); return; }

        // 보유 동료 중에서 배치할 인스턴스를 id로 찾는다 (인스턴스 비교 금지)
        Companion target = cm.GetOwnedCompanions()
            .Find(c => c != null && c.Data != null && c.Data.id == pendingData.id);

        if (target == null)
        {
            // 보유 목록엔 있는데 오브젝트가 없다면 RestoreIntoScene이 아직 안 된 것.
            // 배치에서 새로 획득하지 않는다.
            Debug.LogWarning($"[Placement] {pendingData.companionName} 배치 대상 없음 (오브젝트 미생성/미보유)");
            CancelPlacement();
            return;
        }

        cm.PlaceCompanion(target, cell);
        Debug.Log($"[Placement] {pendingData.companionName} → {cell} 배치");
        CancelPlacement();
    }

    /// <summary>
    /// ★ 원래 코드는 EventSystem.IsPointerOverGameObject() 를 인자 없이 불렀습니다.
    ///   이 오버로드는 '마우스 포인터'를 기준으로 동작해서, 터치 환경에서는 결과가 어긋납니다.
    ///   (에디터에서는 마우스라 정상 → 실기기에서만 배치가 막히는 원인)
    ///
    ///   포인터 id에 의존하지 않도록, 실제 탭 좌표로 직접 UI 레이캐스트를 돌립니다.
    ///   에디터와 실기기가 완전히 동일하게 동작합니다.
    /// </summary>
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        uiHits.Clear();
        PointerEventData ped = new PointerEventData(es) { position = screenPos };
        es.RaycastAll(ped, uiHits);

        if (uiHits.Count == 0) return false;

        if (logBlockers)
        {
            GameObject top = uiHits[0].gameObject;
            Debug.LogWarning(
                $"[Placement] 탭이 UI에 막혔습니다 → 최상단 '{top.name}'. " +
                "그리드를 덮고 있는 UI라면 해당 Graphic의 Raycast Target 을 꺼주세요.", top);
        }

        return true;
    }
}
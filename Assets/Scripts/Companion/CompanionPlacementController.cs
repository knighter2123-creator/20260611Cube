using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

public class CompanionPlacementController : MonoBehaviour
{
    public static CompanionPlacementController Instance { get; private set; }

    // ── 배치 모드 시작/종료 알림 — UI 위 탭은 무시하므로 덮고 있는 창이 비켜야 함 (비키는 건 TabWindow), 씬마다 재생성돼 static 이 아닌 인스턴스 이벤트 ──
    public event Action OnPlacementBegan;
    public event Action OnPlacementEnded;

    /// <summary>지금 배치 모드인가. (UI 가 자기를 숨길지 판단할 때 사용)</summary>
    public bool IsPlacing => isPlacing;

    [Header("배치 미리보기 (선택)")]
    [SerializeField] private SpriteRenderer ghost;        // 반투명 미리보기 스프라이트
    [SerializeField] private Color validColor   = new Color(0f, 1f, 0f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("안내 텍스트 (선택)")]
    [SerializeField] private TextMeshProUGUI hintText;

    [Tooltip("\"더 이상 배치할 수 없습니다\" 같은 짧은 알림을 띄울 텍스트 (선택).\n" +
             "비우면 Hint Text 를 빌려 씁니다.\n" +
             "★ 탭 창보다 '위에' 그려지는 곳에 두세요. 자리가 꽉 찼을 때는 창이 열린 채라, 창 뒤에 있으면 안 보입니다.")]
    [SerializeField] private TextMeshProUGUI noticeText;
    [SerializeField] private float noticeSeconds = 1.5f;

    [Header("입력")]
    [Tooltip("체크 해제(권장): 배치 대기 상태에서 칸을 '누르는 순간' 배치합니다.\n" +
             "체크: 손가락을 '뗄 때' 배치합니다 (끌면서 조준하는 방식).")]
    [SerializeField] private bool placeOnRelease = false;

    [Tooltip("탭이 UI에 막혔을 때 무엇이 막았는지 콘솔에 남깁니다")]
    [SerializeField] private bool logBlockers = true;

    private Camera cam;
    private bool isPlacing;
    private bool armed;              // 배치 모드를 연 그 탭이 그대로 배치로 이어지지 않게 하는 잠금
    private CompanionData pendingData;
    private CompanionListItem callerItem;

    private bool warnedNoTilemap;

    // ★ [추가] "눌렀다" 는 사실을 좌표가 유효해질 때까지 잠깐 기억합니다 (아래 Update 설명 참고)
    private bool  pendingTrigger;
    private float pendingSince;
    private const float PendingTimeout = 0.25f;   // 이 시간 안에 좌표가 안 들어오면 그 누름은 버림

    // 배치 대기 중 안내 문구 (알림이 끝난 뒤 Hint Text 를 원래 문구로 되돌릴 때 사용)
    private string hintMessage = string.Empty;
    private Coroutine noticeRoutine;

    private const string MsgFull       = "더 이상 배치 할 수 없습니다.";
    private const string MsgInvalidCell = "배치할 수 없는 위치입니다.";

    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);
    private PointerEventData pointerData;
    private EventSystem      pointerDataOwner;

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

        // 꺼져 있으면 시작 안 함 — 꺼진 컴포넌트도 메서드는 불려 창만 숨고 Update 가 안 돌아 조용히 고장 (흔한 원인: 탭 창의 자식)
        if (!isActiveAndEnabled)
        {
            Debug.LogError("[Placement] CompanionPlacementController 가 비활성 상태라 배치를 시작할 수 없습니다. " +
                           "이 오브젝트가 탭 창 안에 있지 않은지 확인하고, " +
                           "항상 켜져 있는 HUD 쪽으로 옮기세요.", this);
            return;
        }

        // 꽉 찼으면 대기에 들어가기 전에 알림 (창을 숨긴 뒤에야 알게 되지 않게, 시작 알림도 안 쏨)
        CompanionManager cm = CompanionManager.Instance;
        if (cm != null && !cm.IsPlaced(data.id) && cm.IsFull)
        {
            ShowNotice(MsgFull);
            return;
        }

        pendingData = data;
        callerItem  = caller;
        isPlacing   = true;

        // "배치" 버튼을 누른 입력이 그대로 배치로 이어지지 않게 새 누름부터 허용
        armed = false;
        warnedNoTilemap  = false;

        if (ghost != null)
        {
            ghost.gameObject.SetActive(true);
            if (data.icon != null) ghost.sprite = data.icon;
        }
        // 칸을 누를 때까지 대기 (잘못된 칸은 유지, 취소는 화살표 버튼·탭 전환·우클릭)
        hintMessage = $"{data.companionName} 배치 준비 중 — 배치할 칸을 누르세요";
        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
            hintText.text = hintMessage;
        }

        pendingTrigger = false;

        OnPlacementBegan?.Invoke();
    }

    public void CancelPlacement()
    {
        // 배치 중이었을 때만 종료 알림 (여러 곳에서 불려 중복 알림이면 창을 두 번 되살림)
        bool wasPlacing = isPlacing;

        pendingTrigger = false;
        isPlacing   = false;
        armed       = false;
        pendingData = null;
        var caller  = callerItem;
        callerItem  = null;

        if (ghost != null)    ghost.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);

        // 목록 갱신 때 파괴될 수 있어 ?. 대신 != null
        if (caller != null) caller.RefreshActionButtons();

        if (wasPlacing) OnPlacementEnded?.Invoke();
    }

    void Update()
    {
        // 배치 대기 중에만, 모든 포인터 장치를 훑어 눌림 + 유효 좌표를 씀 (Pointer.current 는 좌표가 Infinity 인 Simulated Touchscreen 이 잡히기도 함)
        if (!isPlacing) return;

        // 좌표 검사에 필요한 카메라 확보 (씬 재로드로 끊겼을 수 있음)
        if (cam == null) cam = Camera.main;

        PointerSample input = ReadPointers();

        // 우클릭으로 취소 — 데스크톱/에디터 전용 (터치엔 우클릭 없음)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        // ── 1) 눌림/뗌을 먼저 기록 — 터치는 누른 프레임에 좌표가 무효일 수 있어 유효해지는 첫 프레임에 판정 ──
        if (input.pressedThisFrame)
        {
            armed = true;
            if (!placeOnRelease) { pendingTrigger = true; pendingSince = Time.unscaledTime; }
        }
        if (placeOnRelease && armed && input.releasedThisFrame)
        {
            pendingTrigger = true; pendingSince = Time.unscaledTime;
        }

        // 너무 오래된 누름은 버립니다 (한참 뒤 엉뚱한 위치에서 배치되는 것 방지)
        if (pendingTrigger && Time.unscaledTime - pendingSince > PendingTimeout)
            pendingTrigger = false;

        // ── 2) 판정에 필요한 것들 확인 ──
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;

        // ★ 배치 타일맵이 연결되지 않았으면 어떤 칸도 배치할 수 없습니다.
        if (!cm.HasPlaceableTilemap)
        {
            if (!warnedNoTilemap)
            {
                warnedNoTilemap = true;
                Debug.LogError("[Placement] CompanionManager 에 배치 타일맵이 연결되지 않았습니다. " +
                               "MainScene 의 SceneBinder 에 Build Tilemap 이 연결됐는지 확인하세요.", this);
            }
            return;
        }

        // Camera.main 은 'MainCamera' 태그가 붙은 카메라만 찾습니다.
        if (cam == null) return;

        // 쓸 수 있는 좌표가 하나도 없으면 이번 프레임은 건너뜁니다 (배치 대기는 유지)
        if (!input.hasPosition)
        {
            if (ghost != null && ghost.gameObject.activeSelf) ghost.gameObject.SetActive(false);
            return;
        }

        Vector2 screenPos = input.position;
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

        // ── 3) 기억해 둔 누름이 있으면 지금 좌표로 판정 ──
        if (!armed || !pendingTrigger) return;
        pendingTrigger = false;   // 한 번의 누름은 한 번만 처리

        // UI 위 탭은 무시 — 칸 검사보다 먼저 (HUD 버튼을 눌렀는데 '배치 불가' 가 뜨지 않게)
        if (IsPointerOverUI(screenPos)) return;

        // ★ [추가] 배치 불가 칸을 누르면 알려 주고, 대기 상태는 그대로 유지합니다.
        if (!canPlace)
        {
            ShowNotice(MsgInvalidCell);
            return;
        }

        ConfirmPlace(cm, cell);
    }

    // ── 포인터 읽기 (모든 장치) ──

    /// <summary>한 프레임 동안 모은 포인터 입력 요약</summary>
    private struct PointerSample
    {
        public bool    pressedThisFrame;   // 어떤 장치든 이번 프레임에 눌렸는가
        public bool    releasedThisFrame;  // 어떤 장치든 이번 프레임에 떼어졌는가
        public bool    hasPosition;        // 쓸 수 있는 좌표를 찾았는가
        public Vector2 position;
    }

    /// <summary>모든 포인터(마우스·터치·펜)를 훑어 이번 프레임 입력 정리 — 좌표 우선순위: 이번에 눌림(3) > 누르는 중(2) > 호버(1), 무효·화면 밖 좌표는 제외 (장치가 적고 devices 는 할당 없는 뷰라 가벼움).</summary>
    private PointerSample ReadPointers()
    {
        PointerSample result = default;
        int bestScore = 0;

        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
        {
            // 'is 타입 변수' — 형 검사와 변환을 한 번에 (Pointer 가 아니면 건너뜀)
            if (!(devices[i] is Pointer p) || !p.enabled) continue;

            if (p is Touchscreen ts)
            {
                // 터치스크린은 손가락마다 슬롯(TouchControl)이 따로 있습니다. 전부 봅니다.
                var touches = ts.touches;
                for (int t = 0; t < touches.Count; t++)
                {
                    TouchControl tc = touches[t];
                    Consider(ref result, ref bestScore,
                             tc.position.ReadValue(),
                             tc.press.wasPressedThisFrame, tc.press.wasReleasedThisFrame, tc.press.isPressed);
                }
            }
            else
            {
                Consider(ref result, ref bestScore,
                         p.position.ReadValue(),
                         p.press.wasPressedThisFrame, p.press.wasReleasedThisFrame, p.press.isPressed);
            }
        }

        result.hasPosition = bestScore > 0;

        return result;
    }

    // ★ ref — 구조체를 '복사본' 이 아니라 원본 그대로 넘겨서, 함수 안에서 고친 값이 바깥에 남게 합니다.
    private void Consider(ref PointerSample r, ref int bestScore,
                          Vector2 pos, bool pressed, bool released, bool held)
    {
        // 눌림/뗌 사실은 좌표가 고장 났어도 기록합니다 (좌표는 다른 장치에서 얻을 수 있으므로)
        r.pressedThisFrame  |= pressed;
        r.releasedThisFrame |= released;

        if (cam == null || !IsUsableScreenPos(pos)) return;

        int score = pressed || released ? 3 : held ? 2 : 1;
        if (score <= bestScore) return;

        bestScore  = score;
        r.position = pos;
    }

    private void ConfirmPlace(CompanionManager cm, Vector3Int cell)
    {
        if (pendingData == null) { CancelPlacement(); return; }

        // 보유 동료 중에서 배치할 인스턴스를 id로 찾는다 (인스턴스 비교 금지)
        Companion target = cm.FindOwnedCompanion(pendingData.id);

        if (target == null)
        {
            // 오브젝트가 없으면 RestoreIntoScene 전 — 배치에서 새로 만들지 않음
            Debug.LogError($"[Placement] {pendingData.companionName} 배치 대상 없음 (오브젝트 미생성/미보유)");
            CancelPlacement();
            return;
        }

        // 확정 직전 재확인 (대기 중 배치가 늘었을 수 있음, 옮기기는 통과)
        if (!target.IsPlaced && cm.IsFull)
        {
            ShowNotice(MsgFull);
            CancelPlacement();
            return;
        }

        if (!cm.PlaceCompanion(target, cell))
        {
            // PlaceCompanion 이 거절한 경우(타일 없음/점유 등) — 대기 상태를 유지하고 다시 고르게 합니다.
            ShowNotice(MsgInvalidCell);
            return;
        }

        GameLog.Info($"[Placement] {pendingData.companionName} → {cell} 배치");

        // 유저가 직접 한 변경이라 바로 저장 (안 하면 저장 전 종료 시 배치가 사라짐)
        SaveManager.Instance?.Save();

        CancelPlacement();
    }

    // ── 알림 (배치 수·이미 배치됨은 CompanionManager 에 직접 물음 — 같은 사실을 두 곳에서 세지 않기) ──

    /// <summary>짧은 알림을 noticeSeconds 동안 띄웁니다. (연속 호출 시 앞의 알림을 덮어씀)</summary>
    private void ShowNotice(string message)
    {
        TextMeshProUGUI target = noticeText != null ? noticeText : hintText;
        if (target == null)
        {
            // 표시할 곳이 없으면 최소한 콘솔에는 남깁니다 (조용한 실패 방지)
            Debug.LogWarning($"[Placement] {message} (Notice Text / Hint Text 가 없어 화면에 표시하지 못했습니다)", this);
            return;
        }

        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(NoticeRoutine(target, message));
    }

    private IEnumerator NoticeRoutine(TextMeshProUGUI target, string message)
    {
        target.gameObject.SetActive(true);
        target.text = message;

        // ★ Realtime — 설정창이 timeScale = 0 을 만들어도, 배속 4배여도 같은 시간만큼 보입니다.
        yield return new WaitForSecondsRealtime(noticeSeconds);
        noticeRoutine = null;

        if (target == null) yield break;   // 그사이 씬이 바뀌어 파괴됐을 수 있음

        // Hint Text 를 빌려 쓴 경우: 아직 대기 중이면 원래 안내로 되돌리고, 아니면 숨깁니다.
        if (target == hintText && isPlacing)
            target.text = hintMessage;
        else
            target.gameObject.SetActive(false);
    }

    /// <summary>쓸 수 있는 스크린 좌표인가 — NaN/Infinity 아님(계산에 계속 퍼짐) + 카메라 pixelRect 안(Game 뷰 밖 마우스 제외).</summary>
    private bool IsUsableScreenPos(Vector2 p)
    {
        if (float.IsNaN(p.x) || float.IsNaN(p.y))           return false;
        if (float.IsInfinity(p.x) || float.IsInfinity(p.y)) return false;
        return cam.pixelRect.Contains(p);
    }

    /// <summary>탭 좌표에 UI 가 있는가 — 실제 좌표로 직접 UI 레이캐스트 (인자 없는 IsPointerOverGameObject 는 마우스 기준이라 실기기 터치에서 어긋남).</summary>
    private bool IsPointerOverUI(Vector2 screenPos)
    {
        EventSystem es = EventSystem.current;
        if (es == null) return false;

        // 탭마다 new 하지 않고 재사용 (EventSystem 이 바뀌었으면 새로 만듦)
        if (pointerData == null || pointerDataOwner != es)
        {
            pointerData      = new PointerEventData(es);
            pointerDataOwner = es;
        }
        pointerData.Reset();
        pointerData.position = screenPos;

        uiHits.Clear();
        es.RaycastAll(pointerData, uiHits);

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
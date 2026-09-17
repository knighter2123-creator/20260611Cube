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

    // ══════════════════════════════════════════════
    //  배치 모드 시작 / 종료 알림
    // ══════════════════════════════════════════════
    //
    // ★ 왜 이벤트가 필요한가
    //   배치 모드에 들어가면 화면을 덮고 있는 UI 가 전부 비켜줘야 합니다.
    //   IsPointerOverUI() 는 "탭 좌표에 UI 가 하나라도 있으면 무시" 하는 방식이라,
    //   반투명 배경 한 장만 깔려 있어도 배치가 영원히 안 됩니다.
    //
    //   지금까지는 companionListPanel 하나만 직접 껐는데, 탭 창 구조에서는
    //   꺼야 할 대상이 '창 전체'로 바뀝니다. 그렇다고 이 스크립트가 탭 창을 알 필요는 없죠.
    //   "배치가 시작됐다" 만 알리고, 비켜주는 건 각자 알아서 하게 둡니다.
    //
    // ★ static 이 아니라 인스턴스 이벤트인 이유
    //   이 컴포넌트는 씬에 속해 있어 씬을 넘나들면 파괴/재생성됩니다.
    //   static 이벤트는 구독자가 해제를 한 번만 빠뜨려도 파괴된 오브젝트를 계속 붙잡습니다.
    public event Action OnPlacementBegan;
    public event Action OnPlacementEnded;

    /// <summary>지금 배치 모드인가. (UI 가 자기를 숨길지 판단할 때 사용)</summary>
    public bool IsPlacing => isPlacing;

    /// <summary>
    /// 배치 중 숨기도록 설정된 패널 (읽기 전용).
    /// TabWindow 가 "탭 창과 겹치게 설정됐는지" 검사할 때 씁니다. 탭 창 구조에서는 비어 있어야 정상입니다.
    /// </summary>
    public GameObject ListPanelToHide => companionListPanel;

    [Header("배치 중 숨길 동료 리스트 패널")]
    [SerializeField] private GameObject companionListPanel;   // 인스펙터에서 연결

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

        // ★ [추가] 컨트롤러가 꺼져 있으면 대기에 들어가지 않습니다.
        //   꺼진 컴포넌트도 '메서드 호출'은 됩니다. 그래서 예전에는 isPlacing = true 가 되고
        //   배치 시작 알림까지 나가서 탭 창은 숨는데, Update() 는 돌지 않아 맵을 눌러도 아무 일이 없었습니다.
        //   에러도 없이 창만 사라지는 — 가장 찾기 힘든 형태의 고장입니다.
        //   흔한 원인: 이 오브젝트가 탭 창 / Companion List Panel 의 '자식'이라 창과 같이 꺼짐.
        if (!isActiveAndEnabled)
        {
            Debug.LogError("[Placement] CompanionPlacementController 가 비활성 상태라 배치를 시작할 수 없습니다. " +
                           "이 오브젝트가 탭 창(또는 Companion List Panel) 안에 있지 않은지 확인하고, " +
                           "항상 켜져 있는 HUD 쪽으로 옮기세요.", this);
            return;
        }

        // ★ [추가] 숨길 패널이 이 컨트롤러의 부모이면, 아래 SetActive(false) 가 자기 자신을 꺼버립니다.
        if (companionListPanel != null && transform.IsChildOf(companionListPanel.transform))
        {
            Debug.LogError("[Placement] Companion List Panel 이 이 컨트롤러의 부모입니다. 패널을 끄면 컨트롤러도 꺼져 배치가 멈춥니다. " +
                           "탭 창 구조에서는 이 칸을 비워두세요 (창 숨기기는 TabWindow 가 합니다).", this);
            return;
        }

        // ★ [추가] 배치 대기에 들어가기 '전에' 자리부터 확인합니다.
        //   꽉 찬 상태로 대기에 들어가면 창이 숨겨지고, 유저가 맵을 눌러 본 뒤에야
        //   안 된다는 걸 알게 됩니다. 입구에서 막는 게 친절합니다.
        //   이 경우 OnPlacementBegan 을 쏘지 않으므로 탭 창도 그대로 열려 있습니다.
        CompanionManager cm = CompanionManager.Instance;
        if (cm != null && !IsAlreadyPlaced(cm, data) && CountPlaced(cm) >= cm.MaxCompanions)
        {
            ShowNotice(MsgFull);
            return;
        }

        pendingData = data;
        callerItem  = caller;
        isPlacing   = true;

        // ★ "배치" 버튼을 누른 그 입력이 같은 프레임에 배치로 이어지는 걸 막습니다.
        //   새로운 누름이 한 번 시작돼야 배치가 허용됩니다.
        armed = false;
        warnedNoTilemap  = false;

        if (companionListPanel != null) companionListPanel.SetActive(false);

        if (ghost != null)
        {
            ghost.gameObject.SetActive(true);
            if (data.icon != null) ghost.sprite = data.icon;
        }
        // ★ '배치 준비 중' 상태 — 유저가 칸을 누를 때까지 이 상태로 기다립니다.
        //   (잘못된 칸을 눌러도 대기는 풀리지 않습니다. 취소는 화살표 버튼 / 탭 전환 / 우클릭)
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
        // ★ 이미 배치 모드가 아니면 알림을 쏘지 않습니다.
        //   CancelPlacement 는 ConfirmPlace 끝, 목록 닫기, 우클릭 등 여러 곳에서 불립니다.
        //   가드가 없으면 "끝났다"가 여러 번 발생해, 구독자가 창을 두 번 되살리는 식으로 어긋납니다.
        bool wasPlacing = isPlacing;

        pendingTrigger = false;
        isPlacing   = false;
        armed       = false;
        pendingData = null;
        var caller  = callerItem;
        callerItem  = null;

        if (ghost != null)    ghost.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);

        // ★ [수정] ?. → != null
        //   CompanionListUI 는 목록을 갱신할 때 아이템을 Destroy 합니다.
        //   파괴된 아이템은 유니티식 == null 검사에서는 null 이지만, C# 의 ?. 는 그걸 모르고 통과시킵니다.
        //   그러면 파괴된 오브젝트의 메서드가 불려 MissingReferenceException 이 날 수 있습니다.
        if (caller != null) caller.RefreshActionButtons();

        if (wasPlacing) OnPlacementEnded?.Invoke();
    }

    void Update()
    {
        // ★ [수정] Pointer.current 하나만 믿지 않고, 연결된 '모든' 포인터 장치에서 읽습니다.
        //   겪은 사례: Pointer.current 가 'Simulated Touchscreen'(Input Debugger 의 터치 흉내 기능) 으로 잡혀 있는데,
        //   이 장치는 눌러도 좌표가 계속 (Infinity, ±Infinity) 였습니다.
        //   그런데 UI 버튼은 정상적으로 눌립니다 → 다른 장치(마우스 등)는 올바른 좌표를 주고 있다는 뜻입니다.
        //   Pointer.current 는 '마지막으로 신호를 보낸 장치' 일 뿐이라, 좌표가 고장 난 장치가 잡힐 수 있습니다.
        //   → 모든 장치를 훑어서 '눌린 + 좌표가 유효한' 쪽을 씁니다. (UI 입력 모듈이 하는 방식과 같습니다)
        // 좌표 유효성 검사(화면 영역)에 카메라가 필요하므로 먼저 확보합니다.
        // (씬 재로드 등으로 참조가 끊겼을 수 있음)
        if (cam == null) cam = Camera.main;

        PointerSample input = ReadPointers();

        if (!isPlacing) return;

        // 우클릭으로 취소 — 데스크톱/에디터 전용 (터치엔 우클릭 없음)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelPlacement();
            return;
        }

        // ── 1) '눌렀다/뗐다' 를 먼저 기록 ──
        //   터치는 누른 프레임에 좌표가 아직 무효일 수 있어, 누른 사실을 잠깐 기억했다가
        //   좌표가 유효해지는 첫 프레임에 판정합니다.
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

        // UI 위 탭은 무시 (리스트 버튼 누르다 배치되는 사고 방지)
        // ★ [순서 변경] UI 검사를 칸 검사보다 먼저 합니다.
        //   HUD 버튼을 눌렀는데 "배치할 수 없는 위치입니다" 가 뜨면 이상하니까요.
        if (IsPointerOverUI(screenPos)) return;

        // ★ [추가] 배치 불가 칸을 누르면 알려 주고, 대기 상태는 그대로 유지합니다.
        if (!canPlace)
        {
            ShowNotice(MsgInvalidCell);
            return;
        }

        ConfirmPlace(cm, cell);
    }

    // ══════════════════════════════════════════════
    //  포인터 읽기 (모든 장치)
    // ══════════════════════════════════════════════

    /// <summary>한 프레임 동안 모은 포인터 입력 요약</summary>
    private struct PointerSample
    {
        public bool    pressedThisFrame;   // 어떤 장치든 이번 프레임에 눌렸는가
        public bool    releasedThisFrame;  // 어떤 장치든 이번 프레임에 떼어졌는가
        public bool    hasPosition;        // 쓸 수 있는 좌표를 찾았는가
        public Vector2 position;
    }

    /// <summary>
    /// 연결된 모든 포인터(마우스·터치·펜)를 훑어 이번 프레임의 입력을 정리합니다.
    ///
    /// 좌표 우선순위 (높은 점수가 이김)
    ///   3 : 이번 프레임에 눌린 장치
    ///   2 : 누르고 있는 장치
    ///   1 : 누르지 않았지만 좌표가 있는 장치 (마우스 호버 → 미리보기용)
    ///   좌표가 NaN/무한대/화면 밖인 장치는 후보에서 뺍니다.
    ///
    /// ★ 성능: 연결된 장치는 보통 2~3개, 터치 슬롯은 장치당 10개 정도라 매 프레임 돌려도 부담이 없습니다.
    ///   InputSystem.devices 는 새 배열을 만들지 않는 읽기 전용 뷰라 GC 도 생기지 않습니다.
    /// </summary>
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
        Companion target = cm.GetOwnedCompanions()
            .Find(c => c != null && c.Data != null && c.Data.id == pendingData.id);

        if (target == null)
        {
            // 보유 목록엔 있는데 오브젝트가 없다면 RestoreIntoScene이 아직 안 된 것.
            // 배치에서 새로 획득하지 않는다.
            Debug.LogError($"[Placement] {pendingData.companionName} 배치 대상 없음 (오브젝트 미생성/미보유)");
            CancelPlacement();
            return;
        }

        // ★ [추가] 확정 직전에 한 번 더 확인합니다 (대기 중에 다른 경로로 배치가 늘었을 수 있음).
        //   이미 배치된 동료를 옮기는 경우는 수가 늘지 않으므로 통과시킵니다.
        if (!target.IsPlaced && CountPlaced(cm) >= cm.MaxCompanions)
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

        Debug.Log($"[Placement] {pendingData.companionName} → {cell} 배치");

        // ★ [추가] 배치는 유저가 직접 한 '의미 있는 변경'이라 바로 저장합니다.
        //   예전에는 배치만으로는 저장이 일어나지 않아, 다른 이유로 저장되기 전에 앱이 종료되면
        //   배치가 사라졌습니다. (지금 시점은 타일맵이 연결돼 있고 occupied 가 채워져 있어
        //   CaptureTo 가 배치 스냅샷을 정상적으로 기록합니다)
        SaveManager.Instance?.Save();

        CancelPlacement();
    }

    // ══════════════════════════════════════════════
    //  배치 수 / 알림
    // ══════════════════════════════════════════════

    /// <summary>
    /// 지금 맵에 배치된 동료 수.
    /// </summary>
    private static int CountPlaced(CompanionManager cm) => cm.PlacedCount;
    //  ★ 배치 수는 '점유 셀' 을 관리하는 CompanionManager 가 가장 정확히 압니다.
    //    그 값을 그대로 물어봅니다 (같은 사실을 두 곳에서 세지 않기).

    /// <summary>이 동료가 이미 맵에 있는가 (있으면 '옮기기'라 수가 늘지 않음)</summary>
    private static bool IsAlreadyPlaced(CompanionManager cm, CompanionData data)
    {
        List<Companion> list = cm.GetOwnedCompanions();
        if (list == null) return false;

        foreach (Companion c in list)
            if (c != null && c.Data != null && c.Data.id == data.id)
                return c.IsPlaced;
        return false;
    }

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

    /// <summary>
    /// 스크린 좌표가 계산에 쓸 수 있는 값인가.
    ///
    /// ① NaN / Infinity 가 아닐 것
    ///    float 는 '숫자가 아닌 값'을 담을 수 있고, 그런 값은 어떤 계산을 거쳐도 계속 퍼집니다.
    ///    (NaN + 1 = NaN) 그래서 입구에서 한 번 걸러내는 게 가장 싸고 확실합니다.
    /// ② 카메라가 그리는 영역(pixelRect) 안일 것
    ///    마우스가 Game 뷰 밖에 있을 때처럼, 숫자는 정상이지만 화면 밖인 경우도 걸러냅니다.
    ///    (지금 카메라 rect 가 0,0,2190,1080 이니 화면 전체와 같습니다)
    /// </summary>
    private bool IsUsableScreenPos(Vector2 p)
    {
        if (float.IsNaN(p.x) || float.IsNaN(p.y))           return false;
        if (float.IsInfinity(p.x) || float.IsInfinity(p.y)) return false;
        return cam.pixelRect.Contains(p);
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
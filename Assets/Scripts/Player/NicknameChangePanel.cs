using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MainScene 닉네임 팝업. 이름 유무에 따라 두 가지 모드로 열립니다.
///
///   이름 없음 → '최초 설정' 모드 : 무료, PlayerProfile.TryRegister
///   이름 있음 → '변경' 모드      : 보석 소모, PlayerProfile.TryChangeName (기존 동작 그대로)
///
/// 여는 쪽(네임플레이트 클릭, NicknamePrompt 의 스테이지 도달 자동 팝업)은 모드를 신경 쓰지 않고
/// Open() 만 부르면 됩니다. 모드는 "여는 순간의 이름 유무" 로 이 스크립트가 정합니다.
///   → 같은 판단을 여는 쪽마다 따로 하면, 한쪽만 고치는 실수가 생깁니다.
///
/// 이 스크립트는 화면(입력값 받기 · 비용/에러 표시)만 담당하고, 실제 유효성 검사·보석 차감·저장은
/// 전부 PlayerProfile 에 맡깁니다 (UI 가 규칙을 중복 구현하지 않게).
///
/// ★ [닉네임 인게임 이동] 이번 변경
///   1. 최초 설정 모드 추가 (제목/비용 문구/확인 동작이 모드에 따라 바뀜)
///   2. IsOpen 추가 — NicknamePrompt 가 "이미 열려 있으면 기다리기" 에 사용
///   3. Open() 두 번 호출 방어 — 두 번 열면 OnGemChanged 가 두 번 구독되고 Close 는 한 번만 해제해서
///      닫힌 뒤에도 구독 하나가 남습니다. 이제 자동 팝업과 네임플레이트 클릭이 겹칠 수 있어 필요해졌습니다.
///   4. 비활성으로 저장된 패널에서 Open() 이 처음 불려도 안전하게 (EnsureInit, 아래 Awake 주석 참고)
///   5. 구독 해제를 CurrencyManager.Instance 가 아니라 "구독했던 대상" 으로 (boundCurrency)
/// </summary>
public class NicknameChangePanel : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("열고 닫을 패널 루트. 이 스크립트가 붙은 오브젝트 자신이면 비워둬도 자동으로 채워집니다.")]
    [SerializeField] private GameObject panelRoot;

    [Header("입력")]
    [SerializeField] private TMP_Text titleText;    // ★ 신규(선택): "닉네임 설정" / "닉네임 변경"
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text costText;     // 변경: "변경 시 보석 3000 소모" / 최초: "첫 설정은 무료입니다"
    [SerializeField] private TMP_Text errorText;    // 실패 사유 표시 (비어 있으면 숨김)
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("문구")]
    [SerializeField] private string registerTitle   = "닉네임 설정";
    [SerializeField] private string changeTitle     = "닉네임 변경";
    [SerializeField] private string registerCostMsg = "첫 설정은 무료입니다";

    [Header("비용 표시 색상")]
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color insufficientColor = Color.red;   // UpgradeUI 의 "부족하면 빨간색" 관례와 통일

    /// <summary>
    /// 지금 팝업이 열려 있는가. (NicknamePrompt 가 겹쳐 열지 않으려고 확인)
    /// ★ 별도 bool 에 기억하지 않고 "실제로 켜져 있는가" 를 매번 읽습니다.
    ///   bool 로 들고 있으면, 누가 Close() 를 거치지 않고 패널을 꺼 버렸을 때 true 로 굳어
    ///   이후 Open() 이 영영 무시되고 NicknamePrompt 도 영원히 기다리게 됩니다.
    /// </summary>
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private bool opening;            // Open() 안에서 SetActive(true) 하는 그 순간에만 true (Awake 가 닫지 않게)
    private bool isRegisterMode;     // 열 때 정해진 모드. 열려 있는 동안은 바뀌지 않음
    private bool initialized;

    // 구독한 CurrencyManager 를 기억해 두고 "그 대상에서" 해제합니다.
    // Instance 에서 해제하면, 그 사이 Instance 가 바뀌었을 때(계정 삭제로 매니저 재생성 등)
    // 옛 매니저에 구독이 남습니다. (탭 창 문서의 "Instance 가 아니라 bound 를 쓰는 이유" 와 같음)
    private CurrencyManager boundCurrency;

    // ───────────────────────────── 초기화 ─────────────────────────────

    /// <summary>
    /// ★ Awake 대신 여기서 초기화하는 이유
    ///   패널을 "비활성 상태로 저장" 하면 씬이 로드돼도 Awake 가 불리지 않습니다 (Awake 는 처음 켜질 때 1회).
    ///   그 상태에서 누가 Open() 을 부르면, 초기화 전이라 panelRoot 가 비어 있어 NullReferenceException 이 나고,
    ///   panelRoot 를 채워 뒀더라도 SetActive(true) 순간 Awake 가 돌면서 panelRoot.SetActive(false) 로
    ///   방금 연 패널을 도로 닫아 버립니다.
    ///   → Open() 과 Awake() 가 모두 EnsureInit() 을 부르고(한 번만 실행), Awake 는 "열리는 중이 아닐 때만" 닫습니다.
    /// </summary>
    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        if (panelRoot == null) panelRoot = gameObject;

        // ★ 리스너는 여기서만 겁니다. 인스펙터의 OnClick 에도 같은 함수를 연결하면
        //   클릭 1번에 2번 처리됩니다. → Confirm / Cancel 버튼의 On Click() 은 인스펙터에서 비워 두세요.
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Close);
    }

    private void Awake()
    {
        EnsureInit();
        if (!opening) panelRoot.SetActive(false);   // Open() 때문에 켜지는 중이면 닫지 않음
    }

    // 패널이 Close() 가 아닌 다른 경로로 꺼져도(다른 스크립트가 SetActive(false), 씬 전환 등) 구독을 남기지 않음.
    // (이 스크립트가 panelRoot 자신에 붙어 있을 때 해당. 부모에 붙어 있다면 Open() 첫머리의 Unsubscribe 가 대신 막음)
    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.RemoveListener(Close);
    }

    // ───────────────────────────── 열기 / 닫기 ─────────────────────────────

    /// <summary>팝업을 엽니다. 이름이 없으면 '최초 설정', 있으면 '변경' 모드.</summary>
    public void Open()
    {
        EnsureInit();
        if (IsOpen) return;   // 이미 열려 있으면 무시 (구독 중복 방지 — 위 요약 3번)

        isRegisterMode = !PlayerProfile.HasName;

        // 혹시 남아 있는 구독이 있으면 먼저 정리 (Close() 를 거치지 않고 꺼졌던 경우 대비 — 이중 구독 방지)
        Unsubscribe();

        opening = true;                             // ★ SetActive 보다 먼저 (첫 활성화 때 Awake 가 닫지 않도록)
        panelRoot.SetActive(true);
        opening = false;

        SetText(titleText, isRegisterMode ? registerTitle : changeTitle);

        if (nameInputField != null)
        {
            // 최초 설정은 빈 칸, 변경은 현재 이름으로 채움 (같은 이름 확인 시 보석 안 씀 — TryChangeName 참고)
            nameInputField.text = isRegisterMode ? string.Empty : PlayerProfile.Name;
            nameInputField.characterLimit = PlayerProfile.MAX_LENGTH;
        }
        SetError(string.Empty);

        // 변경 모드에서만 보석 표시가 필요하므로 그때만 구독합니다.
        // Open()/Close() 로 구독을 짝지어, 열려 있을 때만 구독하므로 리스너가 쌓이지 않습니다.
        if (!isRegisterMode && CurrencyManager.Instance != null)
        {
            boundCurrency = CurrencyManager.Instance;
            boundCurrency.OnGemChanged += HandleGemChanged;
        }

        RefreshCostText();
    }

    /// <summary>팝업을 닫습니다. 최초 설정 모드에서 닫으면 '나중에' — 이름 없이 계속 플레이합니다.</summary>
    public void Close()
    {
        EnsureInit();
        Unsubscribe();
        panelRoot.SetActive(false);
    }

    private void Unsubscribe()
    {
        // boundCurrency 가 파괴됐어도(== null) C# 이벤트 해제 자체는 문제가 없지만,
        // 파괴된 Unity 오브젝트의 멤버 접근을 피하려고 Unity 식 null 검사를 거칩니다.
        if (boundCurrency != null)
            boundCurrency.OnGemChanged -= HandleGemChanged;
        boundCurrency = null;
    }

    // ───────────────────────────── 표시 ─────────────────────────────

    private void HandleGemChanged(int _) => RefreshCostText();

    private void RefreshCostText()
    {
        if (isRegisterMode)
        {
            // 최초 설정: 무료 — 항상 누를 수 있음
            if (costText != null)
            {
                costText.text  = registerCostMsg;
                costText.color = affordableColor;
            }
            if (confirmButton != null) confirmButton.interactable = true;
            return;
        }

        int cost = PlayerProfile.CHANGE_NAME_GEM_COST;
        int gem  = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gem : 0;
        bool enough = gem >= cost;

        if (costText != null)
        {
            costText.text  = $"변경 시 보석 {cost} 소모";
            costText.color = enough ? affordableColor : insufficientColor;
        }

        if (confirmButton != null) confirmButton.interactable = enough;
    }

    // ───────────────────────────── 확인 ─────────────────────────────

    private void OnClickConfirm()
    {
        if (nameInputField == null) return;

        // 모드에 따라 PlayerProfile 의 다른 창구를 부릅니다. 규칙 검사·저장은 전부 PlayerProfile 이 합니다.
        string error;
        bool ok;
        if (isRegisterMode) ok = PlayerProfile.TryRegister(nameInputField.text, out error);
        else                ok = PlayerProfile.TryChangeName(nameInputField.text, out error);

        if (ok) Close();
        else    SetError(error);
    }

    private void SetError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    // 선택 연결 TMP 용 — Unity 오브젝트는 ?. 대신 == null 로 검사
    private static void SetText(TMP_Text tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }
}
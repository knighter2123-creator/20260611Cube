using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>MainScene 닉네임 팝업: 여는 순간 이름 유무로 모드 결정(없음 = 무료 최초 설정, 있음 = 보석 변경) — 화면만 담당하고 검사·차감·저장은 PlayerProfile, 로그인 계정은 서버 응답 동안 "확인 중...".</summary>
// 목표 스테이지 전엔 열되 입력을 막은 잠김 화면 (반응이 없으면 고장으로 보여서) / 이미 열려 있으면 Open 무시 (중복 구독 방지)
public class NicknameChangePanel : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("열고 닫을 패널 루트. 이 스크립트가 붙은 오브젝트 자신이면 비워둬도 자동으로 채워집니다.")]
    [SerializeField] private GameObject panelRoot;

    [Header("입력")]
    [SerializeField] private TMP_Text titleText;    // (선택) "닉네임 설정" / "닉네임 변경"
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

    /// <summary>열려 있는가 — bool 대신 실제 activeSelf 를 읽음 (Close 없이 꺼지면 bool 이 굳어 영영 못 엶).</summary>
    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private bool opening;            // Open() 안에서 SetActive(true) 하는 그 순간에만 true (Awake 가 닫지 않게)
    private bool isRegisterMode;     // 열 때 정해진 모드. 열려 있는 동안은 바뀌지 않음
    private bool isLocked;           // ★ [도달 전 설정 잠금] 최초 설정인데 아직 목표 스테이지 전 → 입력 불가
    private bool initialized;

    // 구독한 CurrencyManager 에서 해제 (Instance 가 바뀌면 옛 매니저에 구독이 남음)
    private CurrencyManager boundCurrency;

    // ───────────────────────────── 초기화 ─────────────────────────────

    /// <summary>1회 초기화(Open·Awake 공용) — 꺼진 채 저장된 패널은 Awake 가 안 불려, Open 에서도 초기화하고 Awake 는 '여는 중이 아닐 때만' 닫음.</summary>
    private void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        if (panelRoot == null) panelRoot = gameObject;

        // 리스너는 여기서만 — 인스펙터 On Click 은 비울 것 (둘 다면 한 번에 두 번 처리)
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Close);
    }

    private void Awake()
    {
        EnsureInit();
        if (!opening) panelRoot.SetActive(false);   // Open() 때문에 켜지는 중이면 닫지 않음
    }

    // Close() 가 아닌 경로로 꺼져도 구독을 남기지 않음 (부모에 붙었으면 Open 첫머리의 Unsubscribe 가 대신 막음)
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
        if (IsOpen) return;   // 이미 열려 있으면 무시 (구독 중복 방지)

        isRegisterMode = !PlayerProfile.HasName;

        // ★ [도달 전 설정 잠금] 잠금은 '최초 설정' 에만 적용. 도달 여부 판단은 NicknamePrompt 한 곳에 맡깁니다.
        isLocked = isRegisterMode && !NicknamePrompt.IsFirstNameUnlocked;

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

            // 잠김이면 입력을 막음 — 열 때마다 다시 설정해야 도달 후 다시 열었을 때 막힌 채 남지 않음
            nameInputField.interactable = !isLocked;
        }
        SetError(string.Empty);

        // 변경 모드에서만 보석 변화 구독 (Open/Close 짝이라 열린 동안만 — 쌓이지 않음)
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
        // 파괴된 Unity 오브젝트 멤버 접근을 피하려고 Unity 식 null 검사
        if (boundCurrency != null)
            boundCurrency.OnGemChanged -= HandleGemChanged;
        boundCurrency = null;
    }

    // ───────────────────────────── 표시 ─────────────────────────────

    private void HandleGemChanged(int _) => RefreshCostText();

    private void RefreshCostText()
    {
        // 잠김: 보석 부족과 같은 빨간색 = "지금은 안 됨" 표시 통일
        if (isLocked)
        {
            ShowCost(NicknamePrompt.LockedMessage, canConfirm: false);
            return;
        }

        // 최초 설정: 무료 — 항상 누를 수 있음
        if (isRegisterMode)
        {
            ShowCost(registerCostMsg, canConfirm: true);
            return;
        }

        int cost = PlayerProfile.CHANGE_NAME_GEM_COST;
        int gem  = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gem : 0;
        ShowCost($"변경 시 보석 {cost} 소모", canConfirm: gem >= cost);
    }

    /// <summary>비용 문구와 확인 버튼 상태를 함께 정합니다. 누를 수 없으면 문구도 빨간색.</summary>
    private void ShowCost(string message, bool canConfirm)
    {
        if (costText != null)
        {
            costText.text  = message;
            costText.color = canConfirm ? affordableColor : insufficientColor;
        }
        if (confirmButton != null) confirmButton.interactable = canConfirm;
    }

    // ───────────────────────────── 확인 ─────────────────────────────

    private void OnClickConfirm()
    {
        if (nameInputField == null) return;

        // 잠김이면 동작에서도 한 번 더 막음 (키보드 완료 키 등 — 화면 쪽 막기는 인스펙터 실수로 풀릴 수 있음)
        if (isLocked)
        {
            SetError(NicknamePrompt.LockedMessage);
            return;
        }

        if (PlayerProfile.IsNameRequestPending) return;   // 서버 응답 기다리는 중 — 연타 무시

        // 모드별 PlayerProfile 창구 호출 (검사·서버·보석·저장은 전부 PlayerProfile, 로그인 계정은 결과가 다음 프레임 이후)
        SetBusy(true);
        System.Action<bool, string> onDone = (ok, error) =>
        {
            if (this == null) return;   // 기다리는 사이 씬이 바뀜
            SetBusy(false);

            if (ok) Close();
            else    SetError(error);
        };

        if (isRegisterMode) PlayerProfile.RequestRegister(nameInputField.text, onDone);
        else                PlayerProfile.RequestChangeName(nameInputField.text, onDone);
    }

    /// <summary>서버 응답을 기다리는 동안 입력·확인을 막고 안내합니다. 끝나면 비용 문구·버튼 상태를 다시 계산합니다.</summary>
    private void SetBusy(bool busy)
    {
        if (nameInputField != null) nameInputField.interactable = !busy && !isLocked;
        if (busy)
        {
            SetError(string.Empty);
            if (confirmButton != null) confirmButton.interactable = false;
            SetText(costText, "확인 중...");
        }
        else
        {
            RefreshCostText();
        }
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
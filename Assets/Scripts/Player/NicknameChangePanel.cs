using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★ 신규 파일. MainScene 닉네임 변경(개명) 팝업.
/// PlayerName(네임플레이트) 클릭으로 열립니다 (PlayerName.OnPointerClick → namePanel.Open()).
///
/// 이 스크립트는 화면(입력값 받기 · 비용/에러 표시)만 담당하고, 실제 유효성 검사·보석 차감·저장은
/// 전부 PlayerProfile.TryChangeName 에 맡깁니다.
///   → 매니저(데이터)가 씬 오브젝트를 몰라도 되게 하는 것과 반대로, 여기서는 UI가 "규칙"을
///     중복 구현하지 않게 하는 것이 목적입니다 (learnings.md 의 데이터/UI 분리 원칙과 같은 이유).
/// </summary>
public class NicknameChangePanel : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("열고 닫을 패널 루트. 이 스크립트가 붙은 오브젝트 자신이면 비워둬도 자동으로 채워집니다.")]
    [SerializeField] private GameObject panelRoot;

    [Header("입력")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private TMP_Text costText;     // 예: "변경 시 보석 300 소모 (보유 120)"
    [SerializeField] private TMP_Text errorText;    // 실패 사유 표시 (비어 있으면 숨김)
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("비용 표시 색상")]
    [SerializeField] private Color affordableColor = Color.white;
    [SerializeField] private Color insufficientColor = Color.red;   // UpgradeUI 의 "부족하면 빨간색" 관례와 통일

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        // ★ 리스너는 여기서만 겁니다. 인스펙터의 OnClick 에도 같은 함수를 연결하면
        //   클릭 1번에 2번 처리됩니다 (설정 패널 셋업 문서에 기록된 리스너 중복 사례와 동일).
        //   → Confirm / Cancel 버튼의 On Click() 은 인스펙터에서 비워 두세요.
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(Close);

        panelRoot.SetActive(false);
    }

    /// <summary>팝업을 엽니다. 현재 이름으로 입력칸을 채우고, 보석 부족 여부를 바로 반영합니다.</summary>
    public void Open()
    {
        panelRoot.SetActive(true);
        

        if (nameInputField != null)
        {
            nameInputField.text = PlayerProfile.Name;
            nameInputField.characterLimit = PlayerProfile.MAX_LENGTH;
        }
        SetError(string.Empty);

        // 열려 있는 동안 보석이 바뀌면(다른 경로로 보석을 얻거나 쓰면) 비용 표시를 바로 갱신합니다.
        // Open()/Close() 로 구독을 짝지어 열려 있을 때만 구독하므로 리스너가 쌓이지 않습니다.
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemChanged += HandleGemChanged;

        RefreshCostText();
    }

    /// <summary>팝업을 닫습니다. 구독 해제와 타임스케일 복구를 함께 처리합니다.</summary>
    public void Close()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemChanged -= HandleGemChanged;

        panelRoot.SetActive(false);
        
    }

    private void HandleGemChanged(int _) => RefreshCostText();

    private void RefreshCostText()
    {
        if (costText == null) return;

        int cost = PlayerProfile.CHANGE_NAME_GEM_COST;
        int gem  = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gem : 0;
        bool enough = gem >= cost;

        costText.text  = $"변경 시 보석 {cost} 소모";
        costText.color = enough ? affordableColor : insufficientColor;

        if (confirmButton != null) confirmButton.interactable = enough;
    }

    private void OnClickConfirm()
    {
        if (nameInputField == null) return;

        if (PlayerProfile.TryChangeName(nameInputField.text, out string error))
        {
            Close();
        }
        else
        {
            SetError(error);
        }
    }

    private void SetError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    private void OnDestroy()
    {

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnGemChanged -= HandleGemChanged;
    }
}

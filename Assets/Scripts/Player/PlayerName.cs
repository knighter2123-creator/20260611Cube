using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerName : MonoBehaviour
{
    private const string PrefsKey     = "PlayerName";
    private const string DefaultName  = "플레이어";
    private const int    MinLength    = 2;
    private const int    MaxLength    = 6;

    [Header("닉네임 표시")]
    [SerializeField] private TMP_Text nickNameText;        // "유저 : OOO" 텍스트
    [SerializeField] private Button nicknameDisplayButton; // 텍스트에 붙은 Button 컴포넌트

    [Header("팝업")]
    [SerializeField] private GameObject popupPanel;        // 팝업 패널 오브젝트
    [SerializeField] private TMP_InputField usernameField; // 팝업 안의 InputField
    [SerializeField] private Button confirmButton;         // 확인 버튼
    [SerializeField] private Button cancelButton;          // 취소 버튼

    void Start()
    {
        // 인스펙터 미할당을 한 번에 잡아내고, 여기서 멈춰 연쇄 에러를 막는다
        if (!ValidateReferences()) return;

        popupPanel.SetActive(false);

        nicknameDisplayButton.onClick.AddListener(OpenPopup);
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(ClosePopup);

        Refresh();
    }

    private bool ValidateReferences()
    {
        bool ok = true;

        // 두 번째 인자로 this를 넘기면 콘솔의 에러를 클릭했을 때
        // Hierarchy에서 해당 오브젝트가 하이라이트된다
        if (popupPanel == null)
        {
            Debug.LogError("[PlayerName] popupPanel이 할당되지 않았습니다. 인스펙터에서 팝업 패널을 넣어주세요.", this);
            ok = false;
        }
        if (nicknameDisplayButton == null)
        {
            Debug.LogError("[PlayerName] nicknameDisplayButton이 할당되지 않았습니다.", this);
            ok = false;
        }
        if (usernameField == null)
        {
            Debug.LogError("[PlayerName] usernameField가 할당되지 않았습니다.", this);
            ok = false;
        }
        if (confirmButton == null)
        {
            Debug.LogError("[PlayerName] confirmButton이 할당되지 않았습니다.", this);
            ok = false;
        }
        if (cancelButton == null)
        {
            Debug.LogError("[PlayerName] cancelButton이 할당되지 않았습니다.", this);
            ok = false;
        }

        return ok;
    }

    public void Refresh()
    {
        string savedName = PlayerPrefs.GetString(PrefsKey, DefaultName);
        if (nickNameText != null)
            nickNameText.text = savedName;
    }

    void OpenPopup()
    {
        // 팝업 열 때 현재 저장된 닉네임을 InputField에 미리 채워줌
        usernameField.text = PlayerPrefs.GetString(PrefsKey, string.Empty);
        popupPanel.SetActive(true);

        // SetActive 직후 바로 Select하면 무시될 수 있어 한 프레임 뒤에 처리
        usernameField.ActivateInputField();
    }

    void ClosePopup()
    {
        popupPanel.SetActive(false);
        usernameField.text = string.Empty;
    }

    void OnConfirm()
    {
        string inputName = usernameField.text.Trim();

        if (inputName.Length < MinLength || inputName.Length > MaxLength)
        {
            Debug.Log($"닉네임을 {MinLength}~{MaxLength}글자 이내로 작성하세요.");
            usernameField.text = string.Empty;
            usernameField.ActivateInputField();
            return;
        }

        PlayerPrefs.SetString(PrefsKey, inputName);
        PlayerPrefs.Save();

        Debug.Log($"[PlayerName] 닉네임 저장 완료: {inputName}");

        Refresh();      // 텍스트 즉시 갱신
        ClosePopup();   // 팝업 닫기
    }
}
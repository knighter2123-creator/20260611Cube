using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerName : MonoBehaviour
{
    [Header("닉네임 표시")]
    [SerializeField] private TMP_Text nickNameText;       // "유저 : OOO" 텍스트
    [SerializeField] private Button nicknameDisplayButton; // 텍스트에 붙은 Button 컴포넌트

    [Header("팝업")]
    [SerializeField] private GameObject popupPanel;        // 팝업 패널 오브젝트
    [SerializeField] private TMP_InputField usernameField; // 팝업 안의 InputField
    [SerializeField] private Button confirmButton;         // 확인 버튼
    [SerializeField] private Button cancelButton;          // 취소 버튼

    void Start()
    {
        popupPanel.SetActive(false);

        nicknameDisplayButton.onClick.AddListener(OpenPopup);
        confirmButton.onClick.AddListener(OnConfirm);
        cancelButton.onClick.AddListener(ClosePopup);

        Refresh();
    }

    public void Refresh()
    {
        string savedName = PlayerPrefs.GetString("PlayerName", "플레이어");
        if (nickNameText != null)
            nickNameText.text = savedName;
    }

    void OpenPopup()
    {
        // 팝업 열 때 현재 저장된 닉네임을 InputField에 미리 채워줌
        usernameField.text = PlayerPrefs.GetString("PlayerName", "");
        popupPanel.SetActive(true);
        usernameField.Select(); // 키보드 바로 올라오게
    }

    void ClosePopup()
    {
        popupPanel.SetActive(false);
        usernameField.text = string.Empty;
    }

    void OnConfirm()
    {
        string inputName = usernameField.text.Trim();

        if (inputName.Length < 2 || inputName.Length > 6)
        {
            Debug.Log("닉네임을 2~6글자 이내로 작성하세요.");
            usernameField.text = string.Empty;
            return;
        }

        PlayerPrefs.SetString("PlayerName", inputName);
        PlayerPrefs.Save();

        Debug.Log($"[PlayerName] 닉네임 저장 완료: {inputName}");

        Refresh();      // 텍스트 즉시 갱신
        ClosePopup();   // 팝업 닫기
    }
}
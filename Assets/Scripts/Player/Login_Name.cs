using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Login_Name : MonoBehaviour
{
    [Header("Input Field")]
    [SerializeField] private TMP_InputField usernameField;
    [SerializeField] private Button submitButton;

    void Start()
    {
        submitButton.onClick.AddListener(PlayToMain);
    }

    public void PlayToMain()
    {
        string inputName = usernameField.text;

        if (inputName.Length < 2 || inputName.Length > 10)
        {
            Debug.Log("닉네임을 2~10글자 이내로 작성하세요.");
            usernameField.text = string.Empty;
            return;
        }

        PlayerPrefs.SetString("PlayerName", inputName);
        PlayerPrefs.Save();

        Debug.Log($"[Login] 닉네임 저장 완료: {inputName}");
        SceneManager.LoadScene("StageScene");
    }
}
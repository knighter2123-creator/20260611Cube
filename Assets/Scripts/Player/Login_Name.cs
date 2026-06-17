using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Login_Name : MonoBehaviour
{
    [Header("Input Field")]
    
    [SerializeField] private Button submitButton;

    void Start()
    {
        submitButton.onClick.AddListener(PlayToMain);
    }

    public void PlayToMain()
    {
        // ✅ SceneLoader로 교체
        SceneLoader.Instance.GoToStage();
    }
}
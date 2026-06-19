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
        if (SceneLoader.Instance == null)
        {
            GameObject obj = new GameObject("SceneLoader");
            obj.AddComponent<SceneLoader>();
        }

        SceneLoader.Instance.GoToStageWithLoading();
    }
}
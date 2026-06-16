using UnityEngine;
using UnityEngine.UI;

public class StageToGachaButton : MonoBehaviour
{
    [SerializeField] private Button gachaButton;

    void Start()
    {
        gachaButton.onClick.AddListener(() => SceneLoader.Instance.GoToGacha());
    }
}
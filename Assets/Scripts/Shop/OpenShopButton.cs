using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// StageScene의 상점 버튼 처리. 풀 씬 이동으로 ShopScene 진입.
/// ※ ShopScene을 Build Settings의 Scenes In Build에 반드시 등록할 것.
/// </summary>
public class OpenShopButton : MonoBehaviour
{
    [SerializeField] private string shopSceneName = "ShopScene";

    // Stage의 상점 버튼 OnClick에 연결
    public void OpenShop()
    {
        SaveManager.Instance?.Save();
        SceneManager.LoadScene(shopSceneName);
    }
}
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ShopScene의 Close 버튼 처리. 풀 씬 이동으로 StageScene 복귀.
/// (additive 아님. StageScene은 세이브 기준으로 재구성됨.)
/// </summary>
public class ShopSceneController : MonoBehaviour
{
    [SerializeField] private string stageSceneName = "StageScene";

    // CloseButton OnClick에 연결
    public void CloseShop()
    {
        SaveManager.Instance?.Save();   // 안전하게 저장 후 복귀(선택)
        SceneManager.LoadScene(stageSceneName);
    }
}
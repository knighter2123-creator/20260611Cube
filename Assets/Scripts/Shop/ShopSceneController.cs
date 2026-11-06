using UnityEngine;

/// <summary>ShopScene 닫기 버튼 — ShopSceneLoader.SaveAndLoad 로 StageScene 에 Single 전환 복귀 (additive 면 뒤에서 적·코루틴이 계속 돌아 복잡, 세이브로 재구성하면 재화도 자연 반영).</summary>
public class ShopSceneController : MonoBehaviour
{
    [SerializeField] private string stageSceneName = "StageScene";

    // CloseButton OnClick에 연결 (씬 파일이 이 함수 이름으로 연결돼 있으니 이름을 바꾸지 마세요)
    public void CloseShop() => ShopSceneLoader.SaveAndLoad(stageSceneName);
}

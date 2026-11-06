using UnityEngine;

/// <summary>StageScene 상점 버튼 — OnClick 용 얇은 껍데기로 목적지 씬만 들고 ShopSceneLoader.SaveAndLoad 에 위임(연타 방지·씬 확인·저장·비동기 로드).</summary>
// ⚠ ShopScene 을 Build Profiles 의 Scene List 에 등록할 것
public class OpenShopButton : MonoBehaviour
{
    [SerializeField] private string shopSceneName = "ShopScene";

    // Stage의 상점 버튼 OnClick에 연결 (씬 파일이 이 함수 이름으로 연결돼 있으니 이름을 바꾸지 마세요)
    public void OpenShop() => ShopSceneLoader.SaveAndLoad(shopSceneName);
}

using UnityEngine;

/// <summary>
/// StageScene의 상점 버튼 처리. 풀 씬 이동으로 ShopScene 진입.
/// ※ ShopScene을 Build Profiles 의 Scene List 에 반드시 등록할 것.
///
/// [작동 순서]
///   버튼 OnClick → OpenShop() → ShopSceneLoader.SaveAndLoad()
///     → (연타 방지) → (씬 등록 확인) → 저장 → 비동기 로드
///   실제 로직은 ShopSceneLoader 한 곳에 있고, 이 컴포넌트는 "어느 씬으로 갈지"만 들고 있습니다.
///   (인스펙터 OnClick 에 연결하려면 MonoBehaviour 의 public 함수가 필요해서 얇은 껍데기로 남겨 둡니다)
/// </summary>
public class OpenShopButton : MonoBehaviour
{
    [SerializeField] private string shopSceneName = "ShopScene";

    // Stage의 상점 버튼 OnClick에 연결 (씬 파일이 이 함수 이름으로 연결돼 있으니 이름을 바꾸지 마세요)
    public void OpenShop() => ShopSceneLoader.SaveAndLoad(shopSceneName);
}

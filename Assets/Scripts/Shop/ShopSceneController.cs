using UnityEngine;

/// <summary>
/// ShopScene의 Close 버튼 처리. 풀 씬 이동으로 StageScene 복귀.
/// (additive 아님. StageScene은 세이브 기준으로 재구성됨.)
///
/// [작동 순서]
///   Close 버튼 OnClick → CloseShop() → ShopSceneLoader.SaveAndLoad()
///     → (연타 방지) → (씬 등록 확인) → 저장 → 비동기 로드
///   OpenShopButton 과 방향만 반대인 같은 동작이라, 공통 로직은 ShopSceneLoader 에 있습니다.
///
/// [왜 additive 로 상점을 띄우지 않나]
///   additive 로 겹쳐 올리면 StageScene 의 적·코루틴이 뒤에서 계속 돌아 상태 관리가 복잡해집니다.
///   통째로 씬을 바꾸고 돌아올 때 세이브로 다시 짓는 쪽이 단순하고, 상점에서 바뀐 재화도 자연스럽게 반영됩니다.
/// </summary>
public class ShopSceneController : MonoBehaviour
{
    [SerializeField] private string stageSceneName = "StageScene";

    // CloseButton OnClick에 연결 (씬 파일이 이 함수 이름으로 연결돼 있으니 이름을 바꾸지 마세요)
    public void CloseShop() => ShopSceneLoader.SaveAndLoad(stageSceneName);
}

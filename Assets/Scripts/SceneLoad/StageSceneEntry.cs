using UnityEngine;

/// <summary>
/// Stage 씬 진입 지점. 항상 활성인 오브젝트에 붙인다.
/// 상점 왕복(풀 씬 전환)으로 이 Start가 다시 불려도,
/// EnsureInitialized는 lastIdleClaimTime이 0일 때만 세팅하므로 경과가 리셋되지 않는다.
/// </summary>
public class StageSceneEntry : MonoBehaviour
{
    [SerializeField] private IdleRewardPopup idlePopup;

    void Start()
    {
        // ★ 최초 1회만 시각 세팅 (이미 값이 있으면 아무것도 안 함 → 경과 누적 유지)
        IdleRewardManager.Instance?.EnsureInitialized();

        // 쌓인 보상이 있으면 팝업이 열리고, 없으면 내부에서 스스로 닫힘
        idlePopup?.Open();
    }
}
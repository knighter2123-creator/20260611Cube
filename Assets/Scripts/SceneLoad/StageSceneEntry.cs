using UnityEngine;

/// <summary>Stage 씬 진입 지점 — 오프라인 보상 초기화·팝업 (상점 왕복으로 다시 불려도 정산 시각은 0일 때만 세팅돼 경과 유지).</summary>
// [붙이는 위치] StageScene 의 항상 활성인 오브젝트
public class StageSceneEntry : MonoBehaviour
{
    [SerializeField] private IdleRewardPopup idlePopup;

    void Start()
    {
        // ★ 최초 1회만 시각 세팅 (이미 값이 있으면 아무것도 안 함 → 경과 누적 유지)
        IdleRewardManager.Instance?.EnsureInitialized();

        // 쌓인 보상이 있을 때만 팝업 (?. 는 미연결 '가짜 null' 을 못 걸러 != null 사용)
        if (idlePopup != null) idlePopup.OpenOnAppStart();
    }
}
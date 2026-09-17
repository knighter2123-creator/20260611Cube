using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// MainScene 의 배치 타일맵을 CompanionManager 에 연결하고, 보유 동료를 이 씬에 복원합니다. (신규)
///
/// ■ 왜 필요한가
///   CompanionManager 는 LoginScene 에서 태어나 계속 살아 있는 매니저입니다.
///   유니티 인스펙터는 '다른 씬'의 오브젝트를 연결할 수 없어서, MainScene 의 타일맵은
///   MainScene 이 열릴 때마다 코드로 건네줘야 합니다. 그 일을 이 컴포넌트가 합니다.
///
///   RestoreIntoScene 은 세 가지를 한 번에 합니다.
///     ① 타일맵 연결   ② (앱 재시작 직후라면) 동료 오브젝트 생성   ③ 저장된 칸에 다시 배치
///
/// ■ 이미 다른 스크립트가 RestoreIntoScene 을 부르고 있다면 이 컴포넌트는 필요 없습니다.
///   (같은 타일맵이 이미 연결돼 있으면 스스로 건너뜁니다)
///
/// ★ 붙이는 곳: MainScene 의 항상 켜져 있는 오브젝트 (예: 타일맵 오브젝트 자신, 또는 씬 관리 오브젝트)
/// </summary>
public class CompanionSceneBinder : MonoBehaviour
{
    [Tooltip("동료를 배치할 수 있는 칸이 칠해진 타일맵")]
    [SerializeField] private Tilemap placeableTilemap;

    // 이 씬에서 복원이 끝났는가. 끝나기 전에 씬이 내려가면 스냅샷을 찍지 않습니다
    // (그때의 배치 정보는 이 씬 것이 아니라 이전 씬 것이라 덮어쓰면 안 됨).
    private bool restored;

    /// <summary>
    /// ★ Awake 가 아니라 Start 인 이유
    ///   같은 씬의 다른 오브젝트들(Player 등)이 Awake 에서 준비를 마친 뒤에 동료를 배치하는 게 안전합니다.
    ///   CompanionManager 는 이전 씬에서 이미 살아 있으므로 여기서는 항상 준비돼 있습니다.
    /// </summary>
    private void Start()
    {
        if (placeableTilemap == null)
        {
            Debug.LogError("[CompanionSceneBinder] Placeable Tilemap 이 연결되지 않았습니다.", this);
            return;
        }

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null)
        {
            // 에디터에서 MainScene 을 바로 재생하면 LoginScene 의 매니저가 없어 여기로 옵니다.
            Debug.LogError("[CompanionSceneBinder] CompanionManager 가 없습니다. LoginScene 부터 실행하세요.", this);
            return;
        }

        // 다른 스크립트가 이미 이 타일맵으로 복원했다면 두 번 하지 않습니다.
        if (cm.PlaceableTilemap == placeableTilemap)
        {
            Debug.Log("[CompanionSceneBinder] 이미 이 타일맵으로 복원돼 있어 건너뜁니다.", this);
            restored = true;
            return;
        }

        cm.RestoreIntoScene(placeableTilemap);
        restored = true;
    }

    /// <summary>
    /// ★ 씬을 떠날 때 현재 배치를 메모리에 기록해 둡니다.
    ///   다음에 이 씬으로 돌아오면 RestoreIntoScene 이 이 기록대로 다시 배치합니다.
    ///   (파일 저장은 하지 않습니다 — 씬이 내려가는 도중에는 타일맵이 먼저 파괴됐을 수 있어
    ///    CaptureTo 가 배치를 건너뛰기 때문입니다. 파일 저장은 배치 확정 시점에 이미 했습니다)
    /// </summary>
    private void OnDestroy()
    {
        if (!restored) return;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;

        // ★ 배치가 0개여도 찍습니다 — '전부 회수한 상태' 도 기억해야
        //   돌아왔을 때 회수한 동료가 다시 나타나지 않습니다.
        cm.SavePlacementSnapshot();
    }
}

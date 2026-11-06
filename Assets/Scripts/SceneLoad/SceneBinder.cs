using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>MainScene 타일맵을 상주 CompanionManager 에 넘기고 RestoreIntoScene(연결·동료 생성·저장 칸 배치) 호출 — 인스펙터는 다른 씬 오브젝트를 연결 못 해서, 매니저 없음·빈 타일맵은 에러로 알림.</summary>
// [붙이는 위치] MainScene 의 항상 켜진 오브젝트 (예: 타일맵 자신)
// ⚠ 클래스명 SceneBinder·필드명 placeableTilemap 을 바꾸면 Missing Script / 연결 끊김
public class SceneBinder : MonoBehaviour
{
    [Tooltip("동료를 배치할 수 있는 칸이 칠해진 타일맵")]
    [SerializeField] private Tilemap placeableTilemap;

    // 복원 완료 여부 — 완료 전에 떠나면 스냅샷 안 찍음 (그때 정보는 이전 씬 것)
    private bool restored;

    /// <summary>Start 에서 복원 — 같은 씬 오브젝트(Player 등)의 Awake 준비가 끝난 뒤가 안전.</summary>
    private void Start()
    {
        if (placeableTilemap == null)
        {
            Debug.LogError("[SceneBinder] Placeable Tilemap 이 연결되지 않았습니다.", this);
            return;
        }

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null)
        {
            // 에디터에서 MainScene 을 바로 재생하면 LoginScene 의 매니저가 없어 여기로 옵니다.
            Debug.LogError("[SceneBinder] CompanionManager 가 없습니다. LoginScene 부터 실행하세요.", this);
            return;
        }

        // 다른 스크립트가 이미 이 타일맵으로 복원했다면 두 번 하지 않습니다.
        if (cm.PlaceableTilemap == placeableTilemap)
        {
            restored = true;
            return;
        }

        // 타일이 하나도 없는 타일맵이면 어디를 눌러도 배치할 수 없으니 알려 줍니다.
        if (placeableTilemap.GetUsedTilesCount() == 0)
            Debug.LogError("[SceneBinder] 연결한 타일맵에 칠해진 타일이 없습니다. 어디를 눌러도 배치할 수 없습니다. " +
                           "Build Tilemap 이 맞는지, 타일을 이 타일맵에 칠했는지 확인하세요.", placeableTilemap);

        cm.RestoreIntoScene(placeableTilemap);
        restored = true;
    }

    /// <summary>씬을 떠날 때 배치를 메모리에만 기록(돌아오면 복원) — 내려가는 중엔 타일맵이 먼저 파괴돼 파일 저장은 배치 확정 시점에 이미 함.</summary>
    private void OnDestroy()
    {
        if (!restored) return;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null) return;

        // 0개여도 기록 (전부 회수한 상태도 기억해야 회수한 동료가 다시 안 나타남)
        cm.SavePlacementSnapshot();
    }
}
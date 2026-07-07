using UnityEngine;
using UnityEngine.Tilemaps;

public class SceneBinder : MonoBehaviour
{
    [SerializeField] private Tilemap placeableTilemap;

    void Start()
    {
        if (CompanionManager.Instance != null && placeableTilemap != null)
            CompanionManager.Instance.RestoreIntoScene(placeableTilemap);
    }

    void OnDestroy()
    {
        // 씬을 떠나기 전 현재 배치를 의도로 보존
        CompanionManager.Instance?.SavePlacementSnapshot();
    }
}
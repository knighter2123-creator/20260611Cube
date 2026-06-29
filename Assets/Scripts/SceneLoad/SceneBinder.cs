using UnityEngine;
using UnityEngine.Tilemaps;

public class SceneBinder : MonoBehaviour
{
    [SerializeField] private Tilemap placeableTilemap;

    void Start()
    {
        Debug.Log($"[Binder] RestoreIntoScene 호출 대상 instance={CompanionManager.Instance?.GetInstanceID()}, tilemap={placeableTilemap?.name}");
        
        if (CompanionManager.Instance != null && placeableTilemap != null)
            CompanionManager.Instance?.RestoreIntoScene(placeableTilemap);
    }
}

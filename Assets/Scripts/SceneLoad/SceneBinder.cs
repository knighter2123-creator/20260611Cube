using UnityEngine;
using UnityEngine.Tilemaps;

public class SceneBinder : MonoBehaviour
{
    [SerializeField] private Tilemap placeableTilemap;

    void Start()
    {
        if (CompanionManager.Instance != null && placeableTilemap != null)
            CompanionManager.Instance?.RestoreIntoScene(placeableTilemap);
    }
}

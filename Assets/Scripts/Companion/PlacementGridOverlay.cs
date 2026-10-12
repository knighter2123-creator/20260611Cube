using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 동료 배치 격자 — 배치 모드 동안 배치 가능한 칸마다 네모 테두리를 띄웁니다.
///   빈 칸 = emptyColor(초록), 이미 동료가 있는 칸 = occupiedColor(빨강)
///
/// [붙이는 위치] CompanionPlacementController 와 같은 오브젝트 (항상 켜져 있는 곳).
///   배치 시작/종료 이벤트만 구독하므로 배치 컨트롤러 코드는 건드리지 않습니다.
///
/// 칸 목록은 CompanionManager 의 배치 타일맵(Build Tilemap)에 칠해진 타일을 그대로 씁니다.
/// 타일을 새로 칠하면 다음 배치 때 자동으로 반영됩니다.
/// </summary>
public class PlacementGridOverlay : MonoBehaviour
{
    [Header("격자 칸")]
    [Tooltip("칸 하나에 쓸 스프라이트 (흰색 — 아래 색으로 물들임). 1칸 크기에 맞춰 자동으로 늘립니다.")]
    [SerializeField] private Sprite cellSprite;
    [SerializeField] private Color  emptyColor    = new Color(0.55f, 1f, 0.55f, 0.9f);
    [SerializeField] private Color  occupiedColor = new Color(1f, 0.45f, 0.45f, 0.8f);

    [Header("정렬")]
    [Tooltip("타일맵(0) 위, 동료(1) 아래에 그려지도록 동료와 같은 순서 + 살짝 뒤(z)로 둡니다.")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int    sortingOrder     = 1;
    [SerializeField] private float  zOffset          = 0.1f;

    private CompanionPlacementController controller;
    private Transform container;
    private readonly List<SpriteRenderer> cells = new List<SpriteRenderer>();

    void Start()
    {
        // 컨트롤러는 Awake 에서 Instance 를 세우므로 Start 에서 찾습니다.
        controller = CompanionPlacementController.Instance;
        if (controller == null)
        {
            Debug.LogWarning("[배치 격자] CompanionPlacementController 를 찾지 못했습니다. 격자를 표시하지 않습니다.", this);
            return;
        }
        if (cellSprite == null)
            Debug.LogWarning("[배치 격자] Cell Sprite 가 비어 있습니다.", this);

        controller.OnPlacementBegan += Show;
        controller.OnPlacementEnded += Hide;

        // 시작하기 전에 이미 배치 중이었다면 (순서가 엇갈린 경우) 바로 그립니다.
        if (controller.IsPlacing) Show();
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnPlacementBegan -= Show;
            controller.OnPlacementEnded -= Hide;
        }

        // 격자 오브젝트는 루트에 만들었으므로 직접 정리합니다.
        if (container != null) Destroy(container.gameObject);
    }

    private void Show()
    {
        CompanionManager cm = CompanionManager.Instance;
        if (cm == null || !cm.HasPlaceableTilemap || cellSprite == null) return;

        if (container == null)
        {
            // 부모의 스케일/위치 영향을 받지 않도록 루트에 둡니다 (월드 좌표로 직접 배치).
            container = new GameObject("PlacementGrid").transform;
            container.SetParent(null, false);
        }
        container.gameObject.SetActive(true);

        Tilemap tilemap = cm.PlaceableTilemap;
        Vector3 cellSize = tilemap.layoutGrid != null ? tilemap.layoutGrid.cellSize : Vector3.one;
        Vector2 spriteSize = cellSprite.bounds.size;
        Vector3 scale = new Vector3(
            spriteSize.x > 0f ? cellSize.x * tilemap.transform.lossyScale.x / spriteSize.x : 1f,
            spriteSize.y > 0f ? cellSize.y * tilemap.transform.lossyScale.y / spriteSize.y : 1f,
            1f);

        int used = 0;
        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (!tilemap.HasTile(cell)) continue;

            SpriteRenderer sr = GetCell(used++);
            Vector3 pos = cm.GetCellCenter(cell);
            pos.z += zOffset;
            sr.transform.position   = pos;
            sr.transform.localScale = scale;
            sr.color = cm.CanPlaceAt(cell) ? emptyColor : occupiedColor;
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
        }

        // 타일이 줄었으면 남는 칸은 끕니다.
        for (int i = used; i < cells.Count; i++)
            if (cells[i].gameObject.activeSelf) cells[i].gameObject.SetActive(false);
    }

    private void Hide()
    {
        if (container != null) container.gameObject.SetActive(false);
    }

    /// <summary>index 번째 칸 렌더러 (없으면 만들어서 재사용 목록에 추가).</summary>
    private SpriteRenderer GetCell(int index)
    {
        if (index < cells.Count) return cells[index];

        GameObject go = new GameObject("Cell");
        go.transform.SetParent(container, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite           = cellSprite;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder     = sortingOrder;
        cells.Add(sr);
        return sr;
    }

    void OnDisable() => Hide();
}

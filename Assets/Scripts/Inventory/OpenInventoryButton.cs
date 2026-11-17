using UnityEngine;
using UnityEngine.UI;

/// <summary>인벤토리 창 열기 버튼.</summary>
[RequireComponent(typeof(Button))]
public class OpenInventoryButton : MonoBehaviour
{
    [SerializeField] private InventoryPanel inventoryPanel;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (inventoryPanel != null) inventoryPanel.Open();
            else Debug.LogWarning("[OpenInventoryButton] Inventory Panel 이 연결되지 않았습니다.", this);
        });
    }
}

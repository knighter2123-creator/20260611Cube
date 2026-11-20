using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>인벤토리 격자 한 칸 (재사용).</summary>
[RequireComponent(typeof(Button))]
public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image    iconImage;
    [Tooltip("(선택) 조각이면 동료 등급 색")]
    [SerializeField] private Image    gradeFrame;
    [SerializeField] private Color    defaultFrameColor = Color.white;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text countText;
    [Tooltip("{count} = 수량")]
    [SerializeField] private string   countFormat = "x{count}";
    [Tooltip("(선택) 새로 들어온 아이템이면 켜는 레드닷 (Tools/UI 굽기/레드닷 붙이기 가 만듦)")]
    [SerializeField] private GameObject newMark;

    private ItemDef def;
    private Button  button;
    private Action<ItemDef> onClicked;

    public void Init(Action<ItemDef> clickCallback)
    {
        onClicked = clickCallback;
        if (button != null) return;
        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(ItemDef item, int count, bool isNew = false)
    {
        def = item;
        if (newMark    != null) newMark.SetActive(isNew);
        ItemChart.ApplyIcon(iconImage, item);
        if (gradeFrame != null) gradeFrame.color = ItemChart.FrameColor(item, defaultFrameColor);
        if (nameText   != null) nameText.text    = item.name;
        if (countText  != null) countText.text   = ChatText.Fill(countFormat, "count", count.ToString("N0"));
    }

    private void HandleClick()
    {
        if (def != null) onClicked?.Invoke(def);
    }
}

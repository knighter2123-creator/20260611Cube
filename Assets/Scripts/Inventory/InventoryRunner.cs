using UnityEngine;

/// <summary>Inventory 의 매 프레임 처리용 숨은 DDOL 오브젝트 (Inventory 가 스스로 만듦).</summary>
[AddComponentMenu("")]
public sealed class InventoryRunner : MonoBehaviour
{
    private void Update() => Inventory.Tick();

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        Inventory.WriteLocalIfDirty();
        Inventory.FlushSoon();
        Inventory.Tick();
    }

    private void OnApplicationQuit() => Inventory.WriteLocalIfDirty();
    private void OnDestroy()         => Inventory.WriteLocalIfDirty();
}

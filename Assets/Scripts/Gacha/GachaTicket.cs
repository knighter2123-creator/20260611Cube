using System;
using UnityEngine;

/// <summary>소환권 — 인벤토리 아이템 gacha_ticket 을 읽고 쓰는 창구.</summary>
public static class GachaTicket
{
    public static event Action<int> OnChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => OnChanged = null;

    public static int Count => Inventory.Count(ItemIds.GachaTicket);

    public static void Add(int amount)
    {
        if (amount <= 0) return;
        Inventory.Add(ItemIds.GachaTicket, amount);
        GameLog.Info($"[GachaTicket] 소환권 +{amount} | 현재: {Count}");
    }

    /// <summary>모자라면 아무것도 바꾸지 않고 false.</summary>
    public static bool TrySpend(int amount)
    {
        if (!Inventory.TrySpend(ItemIds.GachaTicket, amount)) return false;
        GameLog.Info($"[GachaTicket] 소환권 -{amount} | 현재: {Count}");
        return true;
    }

    internal static void RaiseChanged(int count) => OnChanged?.Invoke(count);
}

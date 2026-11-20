using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 '새 아이템' 표시 — 아이템이 늘면 새것으로 기억하고, 상세창을 보거나 인벤토리 창을 닫으면 지움.
/// 계정별로 기기(PlayerPrefs)에 둠. 서버에서 받은 값(다른 기기에서 얻은 것)은 새것으로 치지 않음.
/// </summary>
public static class InventoryNewItems
{
    public static event Action OnChanged;

    private const string PrefsKey = "inventory.new";

    private static readonly HashSet<string> ids = new HashSet<string>();
    private static string account;
    private static bool   loaded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnChanged = null;
        ids.Clear();
        account = null;
        loaded  = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        Inventory.OnItemGained -= OnGained;
        Inventory.OnItemGained += OnGained;
    }

    // ───────────────────────────── 읽기 ─────────────────────────────

    public static bool IsNew(string itemId)
    {
        Sync();
        return !string.IsNullOrEmpty(itemId) && ids.Contains(itemId);
    }

    /// <summary>새 아이템 중 지금도 1개 이상 가진 종류 수 (다 써 버린 것은 세지 않음).</summary>
    public static int Count
    {
        get
        {
            Sync();
            int n = 0;
            foreach (string id in ids) if (Inventory.Count(id) > 0) n++;
            return n;
        }
    }

    // ───────────────────────────── 바꾸기 ─────────────────────────────

    public static void MarkSeen(string itemId)
    {
        Sync();
        if (string.IsNullOrEmpty(itemId) || !ids.Remove(itemId)) return;
        Save();
        OnChanged?.Invoke();
    }

    public static void MarkAllSeen()
    {
        Sync();
        if (ids.Count == 0) return;
        ids.Clear();
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>계정 삭제 — 이 계정의 기록을 지움.</summary>
    public static void DeleteForReset()
    {
        PlayerPrefs.DeleteKey(KeyFor(SaveManager.ActiveAccountId));
        ids.Clear();
        loaded = false;
        OnChanged?.Invoke();
    }

    private static void OnGained(string itemId, int countBefore)
    {
        InventorySettings s = InventorySettings.Get();
        if (s.newMarkOnlyNewKind && countBefore > 0) return;
        if (s.newMarkIgnoreIds != null && s.newMarkIgnoreIds.Contains(itemId)) return;

        Sync();
        if (!ids.Add(itemId)) return;
        Save();
        OnChanged?.Invoke();
    }

    // ───────────────────────────── 계정 · 저장 ─────────────────────────────

    private static string KeyFor(string accountId) => PrefsKey + SaveManager.KeySuffixFor(accountId);

    /// <summary>세이브 주인이 바뀌었으면 그 계정의 기록으로 갈아탐.</summary>
    private static void Sync()
    {
        string acc = SaveManager.ActiveAccountId ?? string.Empty;
        if (loaded && acc == account) return;

        bool hadAny = ids.Count > 0;
        account = acc;
        loaded  = true;
        ids.Clear();

        string raw = PlayerPrefs.GetString(KeyFor(acc), string.Empty);
        foreach (string id in raw.Split('\n'))
            if (!string.IsNullOrEmpty(id)) ids.Add(id);

        if (hadAny || ids.Count > 0) RedDot.Recheck();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(KeyFor(account), string.Join("\n", ids));
        PlayerPrefs.Save();
    }
}

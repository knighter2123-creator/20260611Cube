using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;
using UnityEngine.UI;

/// <summary>아이템 정의(뒤끝 차트) — 접속마다 1회 받아 기기에 캐시, 없는 조각·소환권은 기본 표시.</summary>
// 차트 열: itemId, itemType, name, iconKey, targetId, description, sortOrder (예시: Assets/Datas/BackEndChart/InventoryItemChart.csv)
public static class ItemChart
{
    private const string PREF_CACHE = "inventory.chart.v1";

    public static event Action OnChanged;
    public static bool IsLoaded { get; private set; }

    private static readonly Dictionary<string, ItemDef> defs = new Dictionary<string, ItemDef>();
    private static bool  cacheRead;
    private static bool  loading;
    private static float nextTryAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnChanged = null;
        IsLoaded = cacheRead = loading = false;
        nextTryAt = 0f;
        defs.Clear();
    }

    // ───────────────────────────── 찾기 ─────────────────────────────

    /// <summary>표시용 정의 — 차트 행, 없으면 id 규칙으로 만든 기본값 (null 아님).</summary>
    public static ItemDef Resolve(string itemId)
    {
        ReadCacheOnce();
        if (itemId != null && defs.TryGetValue(itemId, out ItemDef def)) return def;

        InventorySettings s = InventorySettings.Get();

        if (ItemIds.TryGetFragmentTarget(itemId, out string companionId))
        {
            CompanionData c = FindCompanion(companionId);
            return new ItemDef
            {
                itemId      = itemId,
                itemType    = ItemTypes.Fragment,
                name        = ChatText.Fill(s.fragmentNameFormat, "name", c != null ? c.companionName : companionId),
                targetId    = companionId,
                description = s.fragmentDescription,
                sortOrder   = (4 - (c != null ? (int)c.grade : 0)) * 100 + 99,   // 높은 등급이 앞
            };
        }

        if (itemId == ItemIds.GachaTicket)
            return new ItemDef
            {
                itemId = itemId, itemType = ItemTypes.GachaTicket, name = s.gachaTicketName,
                iconKey = s.gachaTicketIconKey, description = s.gachaTicketDescription, sortOrder = 0,
            };

        return new ItemDef { itemId = itemId, name = ChatText.Fill(s.unknownItemName, "id", itemId), sortOrder = int.MaxValue };
    }

    /// <summary>iconKey 스프라이트, 없으면 조각은 동료 아이콘.</summary>
    public static Sprite Icon(ItemDef def)
    {
        if (def == null) return null;
        Sprite s = InventorySettings.Get().FindIcon(def.iconKey);
        if (s != null || !def.IsFragment) return s;

        CompanionData c = Companion(def);
        return c != null ? c.icon : null;
    }

    /// <summary>조각 아이템의 동료 (아니면 null).</summary>
    public static CompanionData Companion(ItemDef def)
    {
        if (def == null || !def.IsFragment) return null;
        CompanionData c = FindCompanion(def.targetId);   // 차트 빈칸이 이상한 값이어도 id 규칙으로
        if (c != null) return c;
        return ItemIds.TryGetFragmentTarget(def.itemId, out string id) ? FindCompanion(id) : null;
    }

    // ── UI 공용 ──

    public static void ApplyIcon(Image image, ItemDef def)
    {
        if (image == null) return;
        Sprite s = Icon(def);
        image.sprite         = s;
        image.enabled        = s != null;
        image.preserveAspect = true;
    }

    /// <summary>조각이면 동료 등급 색, 아니면 fallback.</summary>
    public static Color FrameColor(ItemDef def, Color fallback)
    {
        CompanionData c = Companion(def);
        return c != null ? CompanionGradeStyle.GetColor(c.grade) : fallback;
    }

    private static CompanionData FindCompanion(string companionId)
    {
        CompanionManager cm = CompanionManager.Instance;
        return cm != null ? cm.FindData(companionId) : null;
    }

    // ───────────────────────────── 받기 ─────────────────────────────

    internal static void Tick(bool canUseServer)
    {
        if (IsLoaded || loading || !canUseServer || Time.unscaledTime < nextTryAt) return;
        Load();
    }

    private static void Load()
    {
        loading = true;
        InventorySettings s = InventorySettings.Get();

        if (!string.IsNullOrEmpty(s.chartFileId)) { LoadContents(s.chartFileId); return; }

        // ⚠ chartName / selectedChartFileId 키는 문서 기준 (실서버 미확인)
        BackendManager.Call(cb => Backend.Chart.GetChartList(cb), bro =>
        {
            if (bro == null || !bro.IsSuccess()) { Fail($"차트 목록 조회 실패: {BackendManager.Describe(bro)}"); return; }

            string fileId = null;
            JsonData rows = bro.FlattenRows();
            for (int i = 0; rows != null && rows.IsArray && i < rows.Count; i++)
            {
                if (BackendJson.Str(rows[i], "chartName") != s.chartName) continue;
                fileId = BackendJson.Str(rows[i], "selectedChartFileId");
                break;
            }

            if (string.IsNullOrEmpty(fileId)) { Fail($"'{s.chartName}' 차트를 찾지 못했습니다 (차트 이름 또는 InventorySettings.chartFileId 확인)."); return; }
            LoadContents(fileId);
        });
    }

    private static void LoadContents(string fileId)
    {
        BackendManager.Call(cb => Backend.Chart.GetChartContents(fileId, cb), bro =>
        {
            if (bro == null || !bro.IsSuccess()) { Fail($"차트({fileId}) 내용 조회 실패: {BackendManager.Describe(bro)}"); return; }

            var list = new List<ItemDef>();
            JsonData rows = bro.FlattenRows();
            for (int i = 0; rows != null && rows.IsArray && i < rows.Count; i++)
            {
                JsonData r = rows[i];
                string id = BackendJson.Str(r, "itemId");
                if (string.IsNullOrEmpty(id)) continue;

                list.Add(new ItemDef
                {
                    itemId      = id.Trim(),
                    itemType    = (BackendJson.Str(r, "itemType") ?? string.Empty).Trim(),
                    name        = BackendJson.Str(r, "name") ?? id,
                    iconKey     = BackendJson.Str(r, "iconKey"),
                    targetId    = BackendJson.Str(r, "targetId"),
                    description = BackendJson.Str(r, "description") ?? string.Empty,
                    sortOrder   = (int)BackendJson.Long(r, "sortOrder", 500),
                });
            }

            loading  = false;
            IsLoaded = true;
            Apply(list);
            WriteCache(list);
            GameLog.Info($"[Inventory] 아이템 차트 {list.Count}행 받음 (파일 {fileId})");
            OnChanged?.Invoke();
        });
    }

    private static void Fail(string why)
    {
        loading   = false;
        nextTryAt = Time.unscaledTime + InventorySettings.Get().retryInterval;
        Debug.LogWarning($"[Inventory] {why} — 캐시된 정의로 표시합니다.");
    }

    private static void Apply(List<ItemDef> list)
    {
        defs.Clear();
        foreach (ItemDef d in list) defs[d.itemId] = d;
    }

    // ───────────────────────────── 기기 캐시 ─────────────────────────────

    [Serializable] private class CacheFile { public List<ItemDef> items = new List<ItemDef>(); }

    private static void ReadCacheOnce()
    {
        if (cacheRead) return;
        cacheRead = true;
        if (IsLoaded) return;

        string json = PlayerPrefs.GetString(PREF_CACHE, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            CacheFile f = JsonUtility.FromJson<CacheFile>(json);
            if (f != null && f.items != null) Apply(f.items);
        }
        catch (Exception e) { Debug.LogWarning($"[Inventory] 아이템 차트 캐시를 읽지 못했습니다: {e.Message}"); }
    }

    private static void WriteCache(List<ItemDef> list)
    {
        PlayerPrefs.SetString(PREF_CACHE, JsonUtility.ToJson(new CacheFile { items = list }));
        PlayerPrefs.Save();
    }
}

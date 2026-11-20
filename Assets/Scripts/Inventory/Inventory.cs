using System;
using System.Collections.Generic;
using System.IO;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>인벤토리(아이템 id → 수량). 서버 값 + 못 올린 증감을 기기에 두고 모아서 올림.</summary>
public static class Inventory
{
    public static event Action<string, int> OnItemChanged;
    public static event Action OnChanged;
    /// <summary>이 기기에서 아이템이 늘었을 때 (id, 늘기 전 수량) — 서버에서 받은 값으로 바뀐 것은 부르지 않음 (레드닷용).</summary>
    public static event Action<string, int> OnItemGained;

    public static bool IsServerLoaded => serverLoaded;
    public static bool HasPending     => pending.Count > 0;

    private static readonly Dictionary<string, int> baseItems   = new Dictionary<string, int>();   // 서버와 맞춘 값
    private static readonly Dictionary<string, int> pending     = new Dictionary<string, int>();   // 아직 못 올린 증감
    private static readonly Dictionary<string, int> unconfirmed = new Dictionary<string, int>();   // 보냈지만 결과를 모르는 증감

    private static string account;
    private static bool   opened, migrated, serverLoaded, busy, localDirty;
    private static string rowInDate;
    private static long   serverRev;        // 서버 행의 rev — 올릴 때마다 +1
    private static long   unconfirmedRev;   // 결과를 모르는 업로드가 쓰려던 rev (0 = 없음)
    private static int    generation;
    private static float  nextLoadAt;
    private static float  uploadAt = -1f;

    private static InventoryRunner runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnItemChanged = null;
        OnChanged     = null;
        OnItemGained  = null;
        ClearState();
        account    = null;
        opened     = false;
        generation = 0;
        runner     = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRunner();
    }

    // AccountReset 이 DDOL 을 지우므로 씬마다 다시 만듦
    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => EnsureRunner();

    private static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("[Inventory]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<InventoryRunner>();
    }

    // ───────────────────────────── 읽기 ─────────────────────────────

    public static int Count(string itemId)
    {
        Sync();
        return string.IsNullOrEmpty(itemId) ? 0 : CountRaw(itemId);
    }

    /// <summary>1개 이상 가진 아이템 (id, 수량).</summary>
    public static List<KeyValuePair<string, int>> Snapshot()
    {
        Sync();
        var list = new List<KeyValuePair<string, int>>();
        foreach (string id in AllIds())
        {
            int c = CountRaw(id);
            if (c > 0) list.Add(new KeyValuePair<string, int>(id, c));
        }
        return list;
    }

    // ───────────────────────────── 바꾸기 ─────────────────────────────

    public static void Add(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return;
        long room = (long)int.MaxValue - Count(itemId);
        Change(itemId, (int)Math.Min(amount, room));
    }

    public static bool TrySpend(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0 || Count(itemId) < amount) return false;
        Change(itemId, -amount);
        return true;
    }

    /// <summary>못 올린 증감을 다음 프레임에 올림.</summary>
    public static void FlushSoon()
    {
        if (pending.Count > 0) uploadAt = 0f;
    }

    private static void Change(string itemId, int delta)
    {
        if (delta == 0) return;
        int before = CountRaw(itemId);
        AddTo(pending, itemId, delta);
        localDirty = true;
        if (uploadAt < 0f) uploadAt = Time.unscaledTime + InventorySettings.Get().uploadDelay;
        int after = CountRaw(itemId);
        RaiseItem(itemId, after);
        if (after > before) OnItemGained?.Invoke(itemId, before);
        OnChanged?.Invoke();
    }

    private static void RaiseItem(string itemId, int count)
    {
        OnItemChanged?.Invoke(itemId, count);
        if (itemId == ItemIds.GachaTicket) GachaTicket.RaiseChanged(count);
    }

    // ───────────────────────────── 계정 ─────────────────────────────

    /// <summary>세이브 주인이 바뀌었으면 그 계정의 기기 파일로 갈아탐.</summary>
    private static void Sync()
    {
        string acc = SaveManager.ActiveAccountId ?? string.Empty;
        if (opened && acc == account) return;

        WriteLocalIfDirty();
        Dictionary<string, int> before = Counts();

        generation++;
        account = acc;
        opened  = true;
        ClearState();
        ReadLocal();

        if (pending.Count > 0) uploadAt = 0f;
        RaiseDiff(before);
    }

    /// <summary>계정 삭제 — 이 계정의 기기 파일을 지움 (서버 행은 뒤끝 탈퇴로 사라짐).</summary>
    public static void DeleteLocalForReset()
    {
        string path = LocalPath(SaveManager.ActiveAccountId);
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) { Debug.LogWarning($"[Inventory] 기기 인벤토리 파일 삭제 실패: {e.Message}"); }
        InventoryNewItems.DeleteForReset();

        generation++;
        ClearState();
        opened = false;
    }

    private static void ClearState()
    {
        baseItems.Clear();
        pending.Clear();
        unconfirmed.Clear();
        migrated = serverLoaded = busy = localDirty = false;
        rowInDate      = null;
        serverRev      = 0;
        unconfirmedRev = 0;
        nextLoadAt     = 0f;
        uploadAt       = -1f;
    }

    // ───────────────────────────── 서버 ─────────────────────────────

    internal static void Tick()
    {
        Sync();
        bool can = CanUseServer();
        ItemChart.Tick(can);
        StarUpChart.Tick(can);
        WriteLocalIfDirty();

        if (!can || busy) return;
        float now = Time.unscaledTime;

        if (!serverLoaded) { if (now >= nextLoadAt) Load(); return; }
        if (pending.Count > 0 && uploadAt >= 0f && now >= uploadAt) Upload();
    }

    private static bool CanUseServer()
    {
        AuthManager auth = AuthManager.Instance;
        return BackendManager.IsInitialized
            && auth != null && auth.IsOnline
            && !string.IsNullOrEmpty(account) && auth.Uid == account
            && SaveManager.Instance != null
            && !SaveManager.IsSaveLocked
            && !ServerAccountData.IsDeleting
            && !AccountReset.IsRunning;
    }

    private static void Load()
    {
        busy = true;
        int gen = generation;
        InventorySettings s = InventorySettings.Get();

        BackendTable.GetMyRow(s.tableName, (row, error) =>
        {
            if (gen != generation) return;
            busy = false;

            if (error != null)
            {
                nextLoadAt = Time.unscaledTime + s.retryInterval;
                Debug.LogWarning($"[Inventory] 서버 인벤토리를 받지 못해 기기 값으로 계속합니다 ('{s.tableName}' 테이블 확인): {error}");
                return;
            }

            Dictionary<string, int> before = Counts();
            string inDate = BackendJson.Str(row, "inDate");
            baseItems.Clear();

            if (string.IsNullOrEmpty(inDate))
            {
                rowInDate = null;
                serverRev = 0;
                MigrateLegacySave();   // 행이 없음 → 옛 세이브 값을 증감으로 얹고 다음 업로드가 행을 만듦
            }
            else
            {
                rowInDate = inDate;
                serverRev = BackendJson.Long(row, s.revColumn);
                foreach (KeyValuePair<string, int> kv in ParseItems(BackendJson.Get(row, s.itemsColumn)))
                    baseItems[kv.Key] = kv.Value;
                migrated = true;       // 서버에 이미 있음 → 옛 세이브 값은 버림
                ClearLegacySave();
            }

            ResolveUnconfirmed();
            serverLoaded = true;
            localDirty   = true;
            if (pending.Count > 0) uploadAt = 0f;
            RaiseDiff(before);
            GameLog.Info($"[Inventory] 서버 인벤토리 받음 — {baseItems.Count}종, rev {serverRev}, 올릴 증감 {pending.Count}건");
        });
    }

    /// <summary>결과를 모르던 업로드가 서버에 들어갔으면(rev 가 그만큼 올라감) 그 증감을 빼서 두 번 더해지지 않게.</summary>
    private static void ResolveUnconfirmed()
    {
        if (unconfirmedRev == 0) return;
        if (serverRev >= unconfirmedRev) SubtractFrom(pending, unconfirmed);
        unconfirmed.Clear();
        unconfirmedRev = 0;
    }

    private static void Upload()
    {
        busy = true;
        int gen = generation;
        InventorySettings s = InventorySettings.Get();

        var  sent   = new Dictionary<string, int>(pending);
        var  next   = new Dictionary<string, int>();
        long newRev = serverRev + 1;
        foreach (string id in AllIds())
        {
            baseItems.TryGetValue(id, out int b);
            sent.TryGetValue(id, out int d);
            long c = (long)b + d;
            if (c > 0) next[id] = c > int.MaxValue ? int.MaxValue : (int)c;
        }

        // 응답을 못 받아도 다음 Load 에서 판별할 수 있게 먼저 기록
        unconfirmed.Clear();
        foreach (KeyValuePair<string, int> kv in sent) unconfirmed[kv.Key] = kv.Value;
        unconfirmedRev = newRev;
        localDirty     = true;
        WriteLocalIfDirty();

        var param = new Param();
        param.Add(s.itemsColumn, next);
        param.Add(s.revColumn, newRev);

        Action<string> finish = error =>
        {
            if (gen != generation) return;
            busy = false;

            if (error != null)
            {
                serverLoaded = false;   // 다시 받아서 rev 로 판별한 뒤 올림
                nextLoadAt   = Time.unscaledTime + s.retryInterval;
                Debug.LogWarning($"[Inventory] 인벤토리 저장 실패 — {s.retryInterval:0}초 뒤 다시 받아서 올립니다: {error}");
                return;
            }

            baseItems.Clear();
            foreach (KeyValuePair<string, int> kv in next) baseItems[kv.Key] = kv.Value;
            serverRev = newRev;
            SubtractFrom(pending, sent);
            unconfirmed.Clear();
            unconfirmedRev = 0;

            localDirty = true;
            uploadAt   = pending.Count > 0 ? Time.unscaledTime + s.uploadDelay : -1f;
        };

        if (rowInDate != null)
        {
            BackendTable.Update(s.tableName, rowInDate, param, finish);
        }
        else
        {
            BackendTable.Insert(s.tableName, param, (inDate, error) =>
            {
                if (gen == generation && error == null) rowInDate = inDate;
                finish(error);
            });
        }
    }

    private static Dictionary<string, int> ParseItems(JsonData map)
    {
        var result = new Dictionary<string, int>();
        if (map == null || !map.IsObject) return result;

        foreach (string key in map.Keys)
        {
            JsonData v = BackendJson.Unwrap(map[key]);
            long n = 0;
            if (v == null) continue;
            if (v.IsInt) n = (int)v;
            else if (v.IsLong) n = (long)v;
            else if (v.IsDouble) n = (long)(double)v;
            else if (v.IsString) long.TryParse((string)v, out n);
            if (n > 0) result[key] = n > int.MaxValue ? int.MaxValue : (int)n;
        }
        return result;
    }

    // ───────────────────────────── 옛 세이브 이관 ─────────────────────────────

    private static void MigrateLegacySave()
    {
        if (migrated) return;
        migrated   = true;
        localDirty = true;

        SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Current : null;
        if (d == null) return;

        int moved = 0;
        if (d.companionFragments != null)
            foreach (FragmentEntry e in d.companionFragments)
                if (e != null && !string.IsNullOrEmpty(e.companionId) && e.count > 0)
                {
                    AddTo(pending, ItemIds.Fragment(e.companionId), e.count);
                    moved++;
                }

        if (d.gachaTicket > 0) { AddTo(pending, ItemIds.GachaTicket, d.gachaTicket); moved++; }

        if (moved > 0) GameLog.Info($"[Inventory] 옛 세이브의 조각·소환권 {moved}종을 인벤토리로 옮겼습니다.");
        ClearLegacySave();
    }

    private static void ClearLegacySave()
    {
        SaveManager sm = SaveManager.Instance;
        SaveData d = sm != null ? sm.Current : null;
        if (d == null) return;
        if (d.gachaTicket == 0 && (d.companionFragments == null || d.companionFragments.Count == 0)) return;

        d.gachaTicket = 0;
        d.companionFragments?.Clear();
        sm.RequestSave();
    }

    // ───────────────────────────── 기기 파일 ─────────────────────────────

    [Serializable] private class Entry { public string id; public int count; }

    [Serializable]
    private class LocalFile
    {
        public List<Entry> items       = new List<Entry>();
        public List<Entry> pending     = new List<Entry>();
        public List<Entry> unconfirmed = new List<Entry>();
        public bool        migrated;
        public long        serverRev;
        public long        unconfirmedRev;
    }

    private static string LocalPath(string accountId)
        => Path.Combine(Application.persistentDataPath, $"inventory{SaveManager.KeySuffixFor(accountId)}.json");

    private static void ReadLocal()
    {
        string path = LocalPath(account);
        if (!File.Exists(path)) return;
        try
        {
            LocalFile f = JsonUtility.FromJson<LocalFile>(File.ReadAllText(path));
            if (f == null) return;
            Fill(baseItems,   f.items);
            Fill(pending,     f.pending);
            Fill(unconfirmed, f.unconfirmed);
            migrated       = f.migrated;
            serverRev      = f.serverRev;
            unconfirmedRev = f.unconfirmedRev;
        }
        catch (Exception e) { Debug.LogWarning($"[Inventory] 기기 인벤토리 파일을 읽지 못했습니다: {e.Message}"); }
    }

    internal static void WriteLocalIfDirty()
    {
        if (!localDirty || !opened || SaveManager.IsSaveLocked) return;
        localDirty = false;

        var f = new LocalFile { migrated = migrated, serverRev = serverRev, unconfirmedRev = unconfirmedRev };
        Dump(baseItems,   f.items);
        Dump(pending,     f.pending);
        Dump(unconfirmed, f.unconfirmed);

        try { File.WriteAllText(LocalPath(account), JsonUtility.ToJson(f)); }
        catch (Exception e) { Debug.LogError($"[Inventory] 기기 인벤토리 저장 실패: {e.Message}"); }
    }

    private static void Fill(Dictionary<string, int> to, List<Entry> from)
    {
        if (from == null) return;
        foreach (Entry e in from)
            if (e != null && !string.IsNullOrEmpty(e.id) && e.count != 0) to[e.id] = e.count;
    }

    private static void Dump(Dictionary<string, int> from, List<Entry> to)
    {
        foreach (KeyValuePair<string, int> kv in from) to.Add(new Entry { id = kv.Key, count = kv.Value });
    }

    // ───────────────────────────── 도우미 ─────────────────────────────

    private static void AddTo(Dictionary<string, int> d, string id, int delta)
    {
        d.TryGetValue(id, out int v);
        long next = Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)v + delta));
        if (next == 0) d.Remove(id); else d[id] = (int)next;
    }

    private static void SubtractFrom(Dictionary<string, int> d, Dictionary<string, int> amounts)
    {
        foreach (KeyValuePair<string, int> kv in amounts) AddTo(d, kv.Key, -kv.Value);
    }

    private static HashSet<string> AllIds()
    {
        var ids = new HashSet<string>(baseItems.Keys);
        ids.UnionWith(pending.Keys);
        return ids;
    }

    private static int CountRaw(string id)
    {
        baseItems.TryGetValue(id, out int b);
        pending.TryGetValue(id, out int d);
        long c = (long)b + d;
        return c <= 0 ? 0 : c > int.MaxValue ? int.MaxValue : (int)c;
    }

    private static Dictionary<string, int> Counts()
    {
        var d = new Dictionary<string, int>();
        foreach (string id in AllIds()) d[id] = CountRaw(id);
        return d;
    }

    private static void RaiseDiff(Dictionary<string, int> before)
    {
        var ids = AllIds();
        ids.UnionWith(before.Keys);
        bool any = false;
        foreach (string id in ids)
        {
            before.TryGetValue(id, out int old);
            int now = CountRaw(id);
            if (old == now) continue;
            any = true;
            RaiseItem(id, now);
        }
        if (any) OnChanged?.Invoke();
    }
}

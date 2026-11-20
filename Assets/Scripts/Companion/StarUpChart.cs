using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>동료 성급 상승 확률 한 행 — (등급, 현재 성급) → 다음 성급으로 오를 확률과 실패 시 환급 조각 범위.</summary>
[Serializable]
public class StarUpRow
{
    public CompanionGrade grade;
    public int    star;        // 현재 성급 (이 성급 → star+1)
    public float  rate;        // 성공 확률 % (0 ~ 100, 소수 가능)
    public int    refundMin;   // 실패 시 돌려줄 조각 최소 (나머지는 소모)
    public int    refundMax;   // 실패 시 돌려줄 조각 최대
}

/// <summary>
/// 동료 성급 상승 확률(뒤끝 차트) — 접속마다 1회 받아 기기에 캐시. 받은 적이 없으면(캐시도 없음) 성급 올리기를 막음.
/// 차트 열: grade(Normal/Rare/Epic/Legendary 또는 0~3), star, rate, refundMin, refundMax
/// </summary>
public static class StarUpChart
{
    private const string PREF_CACHE = "companion.starUpChart.v1";

    public static event Action OnChanged;

    /// <summary>이번 접속에 서버에서 받았는가.</summary>
    public static bool IsLoaded { get; private set; }
    /// <summary>쓸 수 있는 확률이 있는가 (서버 또는 캐시).</summary>
    public static bool HasData { get { ReadCacheOnce(); return rows.Count > 0; } }

    private static readonly Dictionary<(CompanionGrade, int), StarUpRow> rows = new Dictionary<(CompanionGrade, int), StarUpRow>();
    private static bool  cacheRead;
    private static bool  loading;
    private static float nextTryAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnChanged = null;
        IsLoaded = cacheRead = loading = false;
        nextTryAt = 0f;
        rows.Clear();
    }

    // ───────────────────────────── 찾기 ─────────────────────────────

    /// <summary>이 등급·현재 성급의 행. 차트에 없으면 false (성급 올리기 불가).</summary>
    public static bool TryGet(CompanionGrade grade, int currentStar, out StarUpRow row)
    {
        ReadCacheOnce();
        return rows.TryGetValue((grade, currentStar), out row);
    }

    // ───────────────────────────── 받기 ─────────────────────────────

    /// <summary>Inventory.Tick 이 부름 — 서버를 쓸 수 있으면 접속마다 1회 받음.</summary>
    internal static void Tick(bool canUseServer)
    {
        if (IsLoaded || loading || !canUseServer || Time.unscaledTime < nextTryAt) return;
        Load();
    }

    private static void Load()
    {
        loading = true;
        InventorySettings s = InventorySettings.Get();

        if (!string.IsNullOrEmpty(s.starUpChartFileId)) { LoadContents(s.starUpChartFileId); return; }

        BackendManager.Call(cb => Backend.Chart.GetChartList(cb), bro =>
        {
            if (bro == null || !bro.IsSuccess()) { Fail($"차트 목록 조회 실패: {BackendManager.Describe(bro)}"); return; }

            string fileId = null;
            JsonData list = bro.FlattenRows();
            for (int i = 0; list != null && list.IsArray && i < list.Count; i++)
            {
                if (BackendJson.Str(list[i], "chartName") != s.starUpChartName) continue;
                fileId = BackendJson.Str(list[i], "selectedChartFileId");
                break;
            }

            if (string.IsNullOrEmpty(fileId)) { Fail($"'{s.starUpChartName}' 차트를 찾지 못했습니다 (차트 이름 또는 InventorySettings.starUpChartFileId 확인)."); return; }
            LoadContents(fileId);
        });
    }

    private static void LoadContents(string fileId)
    {
        BackendManager.Call(cb => Backend.Chart.GetChartContents(fileId, cb), bro =>
        {
            if (bro == null || !bro.IsSuccess()) { Fail($"차트({fileId}) 내용 조회 실패: {BackendManager.Describe(bro)}"); return; }

            var list = new List<StarUpRow>();
            JsonData data = bro.FlattenRows();
            for (int i = 0; data != null && data.IsArray && i < data.Count; i++)
            {
                JsonData r = data[i];
                if (!TryParseGrade(BackendJson.Str(r, "grade"), out CompanionGrade grade))
                {
                    Debug.LogWarning($"[StarUp] {i + 1}행 grade 값을 읽지 못해 건너뜁니다: '{BackendJson.Str(r, "grade")}'");
                    continue;
                }

                int min = Mathf.Max(0, (int)BackendJson.Long(r, "refundMin"));
                int max = Mathf.Max(min, (int)BackendJson.Long(r, "refundMax", min));
                list.Add(new StarUpRow
                {
                    grade     = grade,
                    star      = (int)BackendJson.Long(r, "star"),
                    rate      = Mathf.Clamp((float)BackendJson.Double(r, "rate"), 0f, 100f),
                    refundMin = min,
                    refundMax = max,
                });
            }

            loading  = false;
            IsLoaded = true;
            Apply(list);
            WriteCache(list);
            GameLog.Info($"[StarUp] 성급 상승 확률 차트 {list.Count}행 받음 (파일 {fileId})");
            OnChanged?.Invoke();
        });
    }

    private static bool TryParseGrade(string s, out CompanionGrade grade)
    {
        grade = default;
        if (string.IsNullOrEmpty(s)) return false;
        s = s.Trim();
        if (int.TryParse(s, out int n)) { grade = (CompanionGrade)n; return Enum.IsDefined(typeof(CompanionGrade), grade); }
        return Enum.TryParse(s, true, out grade) && Enum.IsDefined(typeof(CompanionGrade), grade);
    }

    private static void Fail(string why)
    {
        loading   = false;
        nextTryAt = Time.unscaledTime + InventorySettings.Get().retryInterval;
        Debug.LogWarning($"[StarUp] {why} — {(rows.Count > 0 ? "캐시된 확률을 씁니다." : "확률을 받을 때까지 성급 올리기를 막습니다.")}");
    }

    private static void Apply(List<StarUpRow> list)
    {
        rows.Clear();
        foreach (StarUpRow r in list) rows[(r.grade, r.star)] = r;
    }

    // ───────────────────────────── 기기 캐시 ─────────────────────────────

    [Serializable] private class CacheFile { public List<StarUpRow> rows = new List<StarUpRow>(); }

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
            if (f != null && f.rows != null) Apply(f.rows);
        }
        catch (Exception e) { Debug.LogWarning($"[StarUp] 확률 차트 캐시를 읽지 못했습니다: {e.Message}"); }
    }

    private static void WriteCache(List<StarUpRow> list)
    {
        PlayerPrefs.SetString(PREF_CACHE, JsonUtility.ToJson(new CacheFile { rows = list }));
        PlayerPrefs.Save();
    }
}

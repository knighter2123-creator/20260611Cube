using System;
using System.Globalization;
using LitJson;

/// <summary>
/// 뒤끝 응답 JSON(LitJson) 을 안전하게 읽는 도우미.
///   - 키가 없거나 값이 null 이면 예외 대신 null / 기본값
///   - 원본 행은 {"level":{"N":"3"}} 처럼 타입으로 감싸져 올 수 있어 벗겨서 읽음 (FlattenRows 를 거친 값은 그대로)
/// </summary>
public static class BackendJson
{
    public static bool Has(JsonData obj, string key)
        => obj != null && obj.IsObject && obj.ContainsKey(key) && obj[key] != null;

    /// <summary>{"S": ...} / {"N": ...} / {"M": ...} / {"L": ...} / {"BOOL": ...} 를 벗깁니다.</summary>
    public static JsonData Unwrap(JsonData v)
    {
        if (v == null || !v.IsObject || v.Count != 1) return v;
        foreach (string k in v.Keys)
            if (k == "S" || k == "N" || k == "M" || k == "L" || k == "BOOL" || k == "NULL") return v[k];
        return v;
    }

    public static JsonData Get(JsonData obj, string key) => Has(obj, key) ? Unwrap(obj[key]) : null;

    public static string Str(JsonData obj, string key)
    {
        JsonData v = Get(obj, key);
        if (v == null) return null;
        return v.IsString ? (string)v : v.ToString();
    }

    public static long Long(JsonData obj, string key, long fallback = 0)
        => long.TryParse(Str(obj, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long r) ? r : fallback;

    /// <summary>뒤끝 날짜 문자열("2026-11-09T06:00:00.123Z") → UTC unix ms. 읽지 못하면 fallback.</summary>
    public static long DateMs(JsonData obj, string key, long fallback = 0)
    {
        string s = Str(obj, key);
        return DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset d)
            ? d.ToUnixTimeMilliseconds()
            : fallback;
    }
}

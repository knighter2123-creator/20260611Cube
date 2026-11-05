using System;
using System.Collections.Generic;
using System.Text;
using BackEnd;
using UnityEngine;

/// <summary>
/// 채팅 차단 목록 — 뒤끝 게임 정보 테이블에 계정마다 한 행으로 저장합니다 (같은 계정이면 어느 기기에서나 같음).
/// BackendFriendService 가 만들어 씁니다.
///
/// [뒤끝 콘솔 준비]  게임 정보 관리 > 테이블 생성
///   테이블 이름 TABLE ("BLOCK_DATA"), 공개 여부 '비공개' (다른 유저가 내 차단 목록을 읽지 못하게).
///   스키마를 정의한다면 컬럼 COLUMN ("blocks") 을 string 으로. 정의하지 않아도 됩니다.
///   테이블이 없으면 불러오기·저장이 실패합니다 → 오류 로그를 남기고, 차단하기를 누르면 실패 문구가 뜹니다.
///
/// [저장 형식]  blocks = "uid|Base64(닉네임),uid|Base64(닉네임),..."  (비었으면 "-")
///   닉네임을 Base64 로 두는 이유: 따옴표 등 특수 문자가 들어가도 JSON 이스케이프가 생기지 않게
///   (뒤끝 SDK 의 내장 JSON 검사가 문자열 안의 \" 를 거부한 적이 있음 — CloudSave 주석). uid(gamer inDate) 에는 | , 가 없습니다.
///
/// [흐름]
///   Open      — 이 기기의 캐시(PlayerPrefs)를 먼저 보여 주고 서버에서 받아 옴. 받은 것이 원본 (캐시를 덮어씀).
///               서버에 행이 아직 없고 캐시에 예전 차단 목록이 있으면 그것으로 행을 만듦 (기기에만 있던 목록 옮기기).
///   Set       — 서버에 먼저 저장하고, 성공하면 목록·캐시를 바꿈. 저장 중에는 다음 변경을 막음 (한 번에 하나).
///   처음 받기에 실패하면 캐시로 계속 보여 주고, 다음 친구 목록 갱신 때 다시 받습니다 (EnsureLoaded).
/// [약속]  모든 콜백·이벤트는 메인 스레드 (BackendManager.Call).
/// </summary>
public sealed class BackendBlockStore
{
    public const string TABLE  = "BLOCK_DATA";
    public const string COLUMN = "blocks";

    private const string PREF_CACHE = "friends.blocks.";   // + uid — 예전(기기 저장) 키와 같음 → 그대로 옮겨짐
    private const string Empty      = "-";
    private const char   PairSep    = ',';
    private const char   FieldSep   = '|';

    /// <summary>차단 목록 전체 (uid → 차단할 때의 닉네임). 바뀔 때마다 옵니다.</summary>
    public event Action<IReadOnlyDictionary<string, string>> Changed;

    private Dictionary<string, string> blocks = new Dictionary<string, string>();

    private string uid;
    private int    generation;   // Open/Close 마다 올림 — 이전 계정의 늦은 응답을 버림
    private string rowInDate;    // 서버의 내 행 (null = 아직 없음)
    private bool   loaded;       // 서버에서 한 번 받았음 (= 저장해도 됨)
    private bool   loading;
    private bool   writing;

    public IReadOnlyDictionary<string, string> Blocks => blocks;

    // ───────────────────────────── 열기 / 닫기 ─────────────────────────────

    public void Open(string accountUid)
    {
        Close();
        uid = accountUid;
        LoadCache();
        Publish();
        Load();
    }

    public void Close()
    {
        uid = null;
        generation++;
        rowInDate = null;
        loaded = loading = writing = false;
        blocks = new Dictionary<string, string>();
    }

    /// <summary>처음 받기에 실패했으면 다시 받음 (친구 목록 갱신 때 BackendFriendService 가 부름).</summary>
    public void EnsureLoaded()
    {
        if (uid != null && !loaded && !loading) Load();
    }

    private void Load()
    {
        loading = true;
        int gen = generation;

        BackendTable.GetMyRow(TABLE, (row, error) =>
        {
            if (gen != generation) return;
            loading = false;

            if (error != null)
            {
                Debug.LogWarning($"[Block] 차단 목록을 받지 못해 이 기기에 저장된 목록을 씁니다 — 뒤끝 콘솔에 '{TABLE}' 테이블이 있는지 확인하세요 (BackendBlockStore 주석): {error}");
                return;
            }

            loaded = true;
            string inDate = BackendJson.Str(row, "inDate");
            if (string.IsNullOrEmpty(inDate))
            {
                // 서버에 행이 없음 — 이 기기에만 있던 예전 목록이 있으면 그것으로 처음 저장
                if (blocks.Count > 0)
                    Write(new Dictionary<string, string>(blocks), e =>
                    {
                        if (e == null) GameLog.Info($"[Block] 이 기기의 차단 목록 {blocks.Count}명을 서버로 옮겼습니다.");
                    });
                return;
            }

            rowInDate = inDate;
            blocks    = Decode(BackendJson.Str(row, COLUMN));
            SaveCache();
            Publish();
        });
    }

    // ───────────────────────────── 바꾸기 ─────────────────────────────

    /// <summary>차단(on) / 해제(!on). 서버에 저장되면 onDone(null), 실패면 onDone(오류 문구) — 목록은 그대로.</summary>
    public void Set(string peerUid, string peerName, bool on, Action<string> onDone)
    {
        if (uid == null) { onDone?.Invoke(GameMessages.Block.loading); return; }
        if (!loaded)
        {
            EnsureLoaded();
            onDone?.Invoke(GameMessages.Block.loading);
            return;
        }
        if (writing) { onDone?.Invoke(GameMessages.Block.saving); return; }

        var next = new Dictionary<string, string>(blocks);
        if (on) next[peerUid] = string.IsNullOrEmpty(peerName) ? "?" : peerName;
        else if (!next.Remove(peerUid)) { onDone?.Invoke(null); return; }   // 이미 없음

        Write(next, onDone);
    }

    /// <summary>target 을 서버에 저장 (행이 없으면 만듦). 성공하면 목록·캐시를 target 으로.</summary>
    private void Write(Dictionary<string, string> target, Action<string> onDone)
    {
        writing = true;
        int gen = generation;

        var param = new Param();
        param.Add(COLUMN, Encode(target));

        Action<string> finish = error =>
        {
            if (gen != generation) { onDone?.Invoke(GameMessages.Common.accountChanged); return; }
            writing = false;

            if (error != null)
            {
                Debug.LogWarning($"[Block] 차단 목록 저장 실패: {error}");
                onDone?.Invoke(GameMessages.Block.saveFailed);
                return;
            }

            blocks = target;
            SaveCache();
            Publish();
            onDone?.Invoke(null);
        };

        if (rowInDate != null)
        {
            BackendTable.Update(TABLE, rowInDate, param, finish);
        }
        else
        {
            BackendTable.Insert(TABLE, param, (inDate, error) =>
            {
                if (gen == generation && error == null) rowInDate = inDate;
                finish(error);
            });
        }
    }

    private void Publish() => Changed?.Invoke(new Dictionary<string, string>(blocks));

    // ───────────────────────────── 형식 ─────────────────────────────

    private static string Encode(Dictionary<string, string> list)
    {
        if (list.Count == 0) return Empty;

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, string> pair in list)
        {
            if (sb.Length > 0) sb.Append(PairSep);
            sb.Append(pair.Key).Append(FieldSep)
              .Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value ?? string.Empty)));
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> Decode(string s)
    {
        var list = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(s) || s == Empty) return list;

        foreach (string entry in s.Split(PairSep))
        {
            int sep = entry.IndexOf(FieldSep);
            if (sep <= 0) continue;

            string peer = entry.Substring(0, sep);
            string name;
            try   { name = Encoding.UTF8.GetString(Convert.FromBase64String(entry.Substring(sep + 1))); }
            catch (FormatException) { name = "?"; }
            list[peer] = name;
        }
        return list;
    }

    // ───────────────────────────── 이 기기 캐시 ─────────────────────────────

    [Serializable] private class CacheList { public List<string> uids = new List<string>(); public List<string> names = new List<string>(); }

    private void LoadCache()
    {
        blocks = new Dictionary<string, string>();
        string json = PlayerPrefs.GetString(PREF_CACHE + uid, string.Empty);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            CacheList saved = JsonUtility.FromJson<CacheList>(json);
            for (int i = 0; saved != null && i < saved.uids.Count && i < saved.names.Count; i++)
                blocks[saved.uids[i]] = saved.names[i];
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Block] 이 기기의 차단 목록을 읽지 못해 비웁니다: {e.Message}");
        }
    }

    private void SaveCache()
    {
        var list = new CacheList();
        foreach (KeyValuePair<string, string> pair in blocks) { list.uids.Add(pair.Key); list.names.Add(pair.Value); }
        PlayerPrefs.SetString(PREF_CACHE + uid, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }
}

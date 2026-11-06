using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>증강 저장·복구: 영구 카드의 스택 개수만 PlayerPrefs 계정별 키(AUGMENT_SAVE_V1 + SaveManager.KeySuffixFor)에 저장 — 배율은 저장하지 않아 밸런스 패치가 바로 반영됨, 임시 버프는 저장 안 함.</summary>
// save.json 밖이라 저장 잠금을 직접 확인하고, 계정 삭제·이전·클라우드는 전용 static 창구로 처리 (근본 해결은 SaveData 로 합치기)
public partial class AugmentManager
{
    // 기본 키. saveKey 의 초기값과 DeleteAllSavesForReset 가 같은 값을 보게 합니다.
    public const string DEFAULT_SAVE_KEY = "AUGMENT_SAVE_V1";

    [Header("저장")]
    [Tooltip("영구 증강을 PlayerPrefs 에 저장합니다")]
    [SerializeField] private bool saveEnabled = true;

    [SerializeField] private string saveKey = DEFAULT_SAVE_KEY;

    // 이번 실행에서 쓰인 baseKey 목록(static) — 인스펙터 saveKey 를 바꿔도 인스턴스 없이 계정 삭제가 진짜 키를 지우게
    private static readonly HashSet<string> usedSaveKeys = new HashSet<string>();

    /// <summary>실제 PlayerPrefs 키 = baseKey + 계정 꼬리표 (목록엔 baseKey 만 둬서 계정이 바뀌어도 재사용).</summary>
    private static string KeyFor(string baseKey, string accountId) => baseKey + SaveManager.KeySuffixFor(accountId);

    /// <summary>현재 로그인 계정 기준으로 이 인스턴스가 읽고 쓰는 키.</summary>
    private string CurrentKey => KeyFor(saveKey, SaveManager.ActiveAccountId);

    /// <summary>저장 형식 — JsonUtility 가 Dictionary 를 못 다뤄 ids/counts 배열 두 개(같은 인덱스끼리 짝)로 펼침.</summary>
    [Serializable]
    private class AugmentSaveData
    {
        public string[] ids;
        public int[]    counts;
    }

    // ── 저장 ──
    public void Save()
    {
        if (!saveEnabled) return;

        // PlayerPrefs 라 SaveManager 잠금이 자동 적용 안 됨 → 직접 확인 (초기화 중 지운 스택이 되살아나지 않게)
        if (SaveManager.IsSaveLocked) return;

        usedSaveKeys.Add(saveKey);

        var data = new AugmentSaveData
        {
            ids    = new string[permanentStacks.Count],
            counts = new int[permanentStacks.Count]
        };

        int i = 0;
        foreach (var kv in permanentStacks)
        {
            data.ids[i]    = kv.Key;
            data.counts[i] = kv.Value;
            i++;
        }

        PlayerPrefs.SetString(CurrentKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // ── 복구 ──
    public void Load()
    {
        permanentStacks.Clear();

        usedSaveKeys.Add(saveKey);   // saveEnabled 가 꺼져 있어도 키는 기억 (예전에 켜서 저장했을 수 있음)

        if (!saveEnabled) return;

        // 지금 계정의 키 (계정이 바뀌면 AccountSwitch 가 매니저를 새로 만들어 다시 Load)
        string key = CurrentKey;
        if (!PlayerPrefs.HasKey(key)) return;

        try
        {
            var data = JsonUtility.FromJson<AugmentSaveData>(PlayerPrefs.GetString(key));
            if (data?.ids == null || data.counts == null) return;

            // 두 배열 길이가 어긋난 파일이 들어와도 터지지 않게 짧은 쪽 기준으로 돕니다.
            int n = Mathf.Min(data.ids.Length, data.counts.Length);

            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(data.ids[i])) continue;

                // 0 이하 스택은 버림 (깨진 값이 maxStack 판정을 어긋나게 하고 계속 저장됨)
                if (data.counts[i] <= 0) continue;

                permanentStacks[data.ids[i]] = data.counts[i];
            }
        }
        catch (Exception e)
        {
            // 저장 데이터는 게임 밖에서 깨질 수 있음 — 예외로 Awake 가 멈추느니 증강만 초기화 (try-catch 는 통제 못 하는 입력에만)
            Debug.LogWarning($"[Augment] 저장 데이터를 읽지 못했습니다: {e.Message}");
            permanentStacks.Clear();
        }
    }

    // ── 계정 삭제 / 이전 ──

    /// <summary>계정 삭제 전용: 지금 계정의 기본 키 + 이번 실행에 쓰인 키 삭제 — 메모리는 매니저 재생성으로 비워지고, PlayerPrefs.Save 는 AccountReset 이 한 번에.</summary>
    public static void DeleteAllSavesForReset()
    {
        // "지금 계정" 의 키만 지웁니다. 같은 기기의 다른 계정 증강은 남겨야 합니다.
        string account = SaveManager.ActiveAccountId;

        PlayerPrefs.DeleteKey(KeyFor(DEFAULT_SAVE_KEY, account));

        foreach (string key in usedSaveKeys)
            if (!string.IsNullOrEmpty(key)) PlayerPrefs.DeleteKey(KeyFor(key, account));

        GameLog.Info($"[Augment] 계정 삭제 — 증강 저장 삭제 (계정 {SaveManager.ActiveAccountLabel}, 키 {usedSaveKeys.Count + 1}개 확인)");
    }

    /// <summary>게스트 → 계정 이전 때 증강 저장을 옮김(원본 삭제) — 대상에 이미 있으면 덮지 않음.</summary>
    public static void MoveSavesBetweenAccounts(string fromAccountId, string toAccountId)
    {
        var baseKeys = new HashSet<string>(usedSaveKeys) { DEFAULT_SAVE_KEY };
        int moved = 0;

        foreach (string baseKey in baseKeys)
        {
            if (string.IsNullOrEmpty(baseKey)) continue;

            string from = KeyFor(baseKey, fromAccountId);
            string to   = KeyFor(baseKey, toAccountId);
            if (from == to || !PlayerPrefs.HasKey(from) || PlayerPrefs.HasKey(to)) continue;

            PlayerPrefs.SetString(to, PlayerPrefs.GetString(from));
            PlayerPrefs.DeleteKey(from);
            moved++;
        }

        PlayerPrefs.Save();   // 세이브 파일 이동과 짝이 맞게 즉시 기록
        GameLog.Info($"[Augment] 증강 저장 이동: {moved}개 키");
    }

    // ── 클라우드 세이브: save.json 밖이라 따로 실어 보냄 (씬의 saveKey 가 모두 기본 키라 기본 키만) ──

    /// <summary>이 계정의 증강 저장 문자열. 없으면 "".</summary>
    public static string ExportForCloud(string accountId)
        => PlayerPrefs.GetString(KeyFor(DEFAULT_SAVE_KEY, accountId), string.Empty);

    /// <summary>클라우드 증강 저장으로 교체(빈 문자열이면 삭제) — 메모리는 이후 매니저 재생성 Load 가 다시 읽음.</summary>
    public static void ImportFromCloud(string accountId, string json)
    {
        string key = KeyFor(DEFAULT_SAVE_KEY, accountId);
        if (string.IsNullOrEmpty(json)) PlayerPrefs.DeleteKey(key);
        else                            PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    /// <summary>저장 데이터를 통째로 지웁니다. 개발 중 초기화용.</summary>
    [ContextMenu("테스트: 저장 데이터 삭제")]
    private void DeleteSave()
    {
        PlayerPrefs.DeleteKey(CurrentKey);   // 지금 계정만
        PlayerPrefs.Save();
        GameLog.Info("[Augment] 저장 데이터를 삭제했습니다.");
    }
}
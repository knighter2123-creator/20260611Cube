using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 시스템 — 저장 / 복구.
///
/// StageManager.Save.cs 와 같은 자리의 파일입니다.
/// "무엇을 저장하는가"만 여기 모여 있으면, 나중에 세이브 방식을 바꿀 때
/// 이 파일 하나만 열면 됩니다.
///
/// [무엇을 저장하는가]
/// 영구 카드의 **스택 개수만** 저장합니다. 계산된 배율(x1.52)은 저장하지 않습니다.
/// 배율을 저장하면 밸런스 패치로 카드 수치를 바꿨을 때 기존 유저에게 반영되지 않고,
/// 저장/복구 과정에서 값이 어긋나기 시작하면 원인을 추적할 수 없습니다.
/// 스택만 저장하고 배율은 항상 다시 계산하면 그런 문제가 구조적으로 사라집니다.
///
/// 임시 버프는 저장하지 않습니다. 게임을 껐다 켰으면 이미 만료된 것으로 봅니다.
///
/// [저장 위치 — PlayerPrefs, 계정별 키]
///   증강은 save.json 이 아니라 PlayerPrefs 에 있으므로 키에 세이브 파일과 같은 계정 꼬리표를 붙입니다.
///     게스트 → "AUGMENT_SAVE_V1"
///     계정   → "AUGMENT_SAVE_V1_&lt;UID&gt;"
///   꼬리표 규칙은 SaveManager.KeySuffixFor 한 곳에 있습니다 (KeyFor 참고).
///   save.json 과 따로 있어서 SaveManager 의 저장 잠금을 직접 확인하고(Save),
///   계정 삭제·이전도 전용 static 창구(DeleteAllSavesForReset / MoveSavesBetweenAccounts)로 처리합니다.
///
///   ※ 근본 해결은 증강 저장을 SaveData 로 합치는 것입니다 (save.json 하나만 지우면 되도록).
/// </summary>
public partial class AugmentManager
{
    // 기본 키. saveKey 의 초기값과 DeleteAllSavesForReset 가 같은 값을 보게 합니다.
    public const string DEFAULT_SAVE_KEY = "AUGMENT_SAVE_V1";

    [Header("저장")]
    [Tooltip("영구 증강을 PlayerPrefs 에 저장합니다")]
    [SerializeField] private bool saveEnabled = true;

    [SerializeField] private string saveKey = DEFAULT_SAVE_KEY;

    // 이번 실행 중에 실제로 쓰인 키 목록 (계정 꼬리표 없는 baseKey).
    //
    //   saveKey 는 인스펙터에서 바꿀 수 있는 값입니다. 누군가 "AUGMENT_SAVE_V2" 로 바꿔 두면
    //   계정 삭제가 기본 키만 지우고 진짜 데이터는 남기는 일이 생깁니다.
    //   그런데 계정 삭제는 AugmentManager 가 씬에 없을 때(설정 패널이 부를 때 이미 파괴됐을 수도 있음)도
    //   동작해야 해서 인스턴스의 saveKey 를 직접 읽을 수 없습니다.
    //   그래서 인스턴스가 저장/복구할 때마다 자기 키를 여기에 적어 둡니다. static 이라 인스턴스가 사라져도 남습니다.
    private static readonly HashSet<string> usedSaveKeys = new HashSet<string>();

    /// <summary>
    /// 실제로 PlayerPrefs 에 쓰는 키. 인스펙터 키(baseKey) + 계정 꼬리표.
    /// usedSaveKeys 에는 꼬리표 없는 baseKey 를 적어 두고, 쓸 때마다 이 함수로 붙입니다
    /// → 계정이 바뀌어도 "어떤 baseKey 들을 썼는지" 목록은 그대로 재사용됩니다.
    /// </summary>
    private static string KeyFor(string baseKey, string accountId) => baseKey + SaveManager.KeySuffixFor(accountId);

    /// <summary>현재 로그인 계정 기준으로 이 인스턴스가 읽고 쓰는 키.</summary>
    private string CurrentKey => KeyFor(saveKey, SaveManager.ActiveAccountId);

    /// <summary>
    /// 저장 형식.
    ///
    /// JsonUtility 는 Dictionary 를 직렬화하지 못합니다.
    /// 그래서 ID 배열과 개수 배열 두 개로 펼쳐서 저장합니다. (같은 인덱스끼리 짝)
    ///
    ///   ids    = ["Aug_Attack_15", "Aug_Crit_25"]
    ///   counts = [3,               1            ]
    /// </summary>
    [Serializable]
    private class AugmentSaveData
    {
        public string[] ids;
        public int[]    counts;
    }

    // ─────────────────────────────────────────────────────────
    //  저장
    // ─────────────────────────────────────────────────────────
    public void Save()
    {
        if (!saveEnabled) return;

        // 이 파일은 save.json 이 아니라 PlayerPrefs 에 따로 저장합니다.
        //   그래서 SaveManager 의 잠금이 자동으로 적용되지 않습니다.
        //   초기화 도중(MainScene 이 내려가는 사이) 이 Save() 가 불리면
        //   방금 지운 증강 스택이 그대로 다시 기록되므로, 같은 잠금을 여기서도 확인합니다.
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

    // ─────────────────────────────────────────────────────────
    //  복구
    // ─────────────────────────────────────────────────────────
    public void Load()
    {
        permanentStacks.Clear();

        usedSaveKeys.Add(saveKey);   // saveEnabled 가 꺼져 있어도 키는 기억 (예전에 켜서 저장했을 수 있음)

        if (!saveEnabled) return;

        // 지금 계정의 키. Load 가 불리는 순간의 계정 기준이므로,
        //   계정이 바뀌면 AccountSwitch 가 매니저를 새로 만들어 이 Load 가 새 계정으로 다시 불리게 합니다.
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
                permanentStacks[data.ids[i]] = data.counts[i];
            }
        }
        catch (Exception e)
        {
            // ─── 왜 try-catch 를 쓰나 (학습 포인트) ──────────────────────
            // 저장 파일은 게임 밖에서 깨질 수 있습니다.
            // 앱이 저장 도중 강제 종료되거나, 버전 업데이트로 형식이 바뀌거나,
            // 기기 저장소가 손상되거나.
            // 그때 예외를 그냥 두면 Awake 가 중단되어 게임이 아예 시작되지 않습니다.
            // "증강만 초기화되고 게임은 돌아간다" 가 훨씬 나은 실패입니다.
            //
            // 단, 모든 곳에 try-catch 를 두르는 건 나쁜 습관입니다.
            // 외부 데이터를 읽는 지점처럼 "내가 통제할 수 없는 입력"에만 쓰세요.
            // ────────────────────────────────────────────────────────
            Debug.LogWarning($"[Augment] 저장 데이터를 읽지 못했습니다: {e.Message}");
            permanentStacks.Clear();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  계정 삭제 / 이전
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 계정 삭제 전용. 기본 키 + 이번 실행에서 쓰인 모든 키의 증강 저장을 지웁니다.
    /// PlayerPrefs.Save() 는 부르지 않습니다 — 여러 삭제를 모아 AccountReset 이 한 번만 부릅니다.
    ///
    /// ★ 살아 있는 AugmentManager 의 메모리(permanentStacks)는 건드리지 않습니다.
    ///   계정 삭제는 매니저를 통째로 새로 만들기 때문에, 새 인스턴스의 Load() 가 빈 상태로 시작합니다.
    ///   그 사이에 옛 인스턴스가 Save() 를 불러도 저장 잠금에 막힙니다.
    /// </summary>
    public static void DeleteAllSavesForReset()
    {
        // "지금 계정" 의 키만 지웁니다. 같은 기기의 다른 계정 증강은 남겨야 합니다.
        string account = SaveManager.ActiveAccountId;

        PlayerPrefs.DeleteKey(KeyFor(DEFAULT_SAVE_KEY, account));

        foreach (string key in usedSaveKeys)
            if (!string.IsNullOrEmpty(key)) PlayerPrefs.DeleteKey(KeyFor(key, account));

        Debug.Log($"[Augment] 계정 삭제 — 증강 저장 삭제 (계정 {SaveManager.ActiveAccountLabel}, 키 {usedSaveKeys.Count + 1}개 확인)");
    }

    /// <summary>
    /// 한 계정의 증강 저장을 다른 계정으로 "옮깁니다" (원본 키는 지움).
    /// 게스트 진행을 처음 로그인한 계정이 가져갈 때 세이브 파일과 함께 부릅니다.
    /// 대상 계정에 이미 저장이 있으면 그 키는 건드리지 않습니다 (그 계정의 진행을 덮어쓰지 않게).
    /// 확인하는 키: 기본 키 + 이번 실행에서 쓰인 키 (DeleteAllSavesForReset 와 같은 범위).
    /// </summary>
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
        Debug.Log($"[Augment] 증강 저장 이동: {moved}개 키");
    }

    /// <summary>저장 데이터를 통째로 지웁니다. 개발 중 초기화용.</summary>
    [ContextMenu("테스트: 저장 데이터 삭제")]
    private void DeleteSave()
    {
        PlayerPrefs.DeleteKey(CurrentKey);   // 지금 계정만
        PlayerPrefs.Save();
        Debug.Log("[Augment] 저장 데이터를 삭제했습니다.");
    }
}
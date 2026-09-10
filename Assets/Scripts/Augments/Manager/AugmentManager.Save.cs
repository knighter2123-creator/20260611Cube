using System;
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
/// </summary>
public partial class AugmentManager
{
    [Header("저장")]
    [Tooltip("영구 증강을 PlayerPrefs 에 저장합니다")]
    [SerializeField] private bool saveEnabled = true;

    [SerializeField] private string saveKey = "AUGMENT_SAVE_V1";

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

        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────────────────────────
    //  복구
    // ─────────────────────────────────────────────────────────
    public void Load()
    {
        permanentStacks.Clear();

        if (!saveEnabled) return;
        if (!PlayerPrefs.HasKey(saveKey)) return;

        try
        {
            var data = JsonUtility.FromJson<AugmentSaveData>(PlayerPrefs.GetString(saveKey));
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

    /// <summary>저장 데이터를 통째로 지웁니다. 개발 중 초기화용.</summary>
    [ContextMenu("테스트: 저장 데이터 삭제")]
    private void DeleteSave()
    {
        PlayerPrefs.DeleteKey(saveKey);
        PlayerPrefs.Save();
        Debug.Log("[Augment] 저장 데이터를 삭제했습니다.");
    }
}

using System;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>클라우드 세이브: 게임 시작 전 클라우드와 버전(savedAt/cloudSyncedAt '같은가'만 비교)을 맞춰 어느 쪽을 쓸지 정하고, 확인을 마친 계정만 uploadInterval 마다 세이브+증강을 Base64 로 올림.</summary>
// [붙이는 위치] SaveManager.Awake 가 자동으로 붙임 / [뒤끝 콘솔] 클라우드 저장에 컬렉션 "saves" 생성 필요 (없으면 404 로 올리지 않음)
// 판단: 클라우드 없음·같음·이 기기에서 세이브 삭제·클라우드 그대로 → 이 기기 / 새 기기·이 기기 그대로 → 클라우드 적용 후 재시작 / 둘 다 바뀜 → 팝업으로 물음
// ★ Base64 인 이유: 뒤끝 SDK 가 문자열 속 \" 를 못 읽어 400 이 남 / ★ 뒤끝 호출 직전 세이브 주인 == Backend.UserInDate 확인 (옛 계정 세이브를 새 계정에 안 올리게)
// ⚠ 올리기 전 받아 와서 다른 기기가 올렸으면 멈추지만, 트랜잭션이 없어 그 사이 1초 안팎의 경합은 못 막음 (감수)
public class CloudSave : MonoBehaviour
{
    public static CloudSave Instance { get; private set; }

    public const string COLLECTION = "saves";

    [Tooltip("게임 중 올리는 최소 간격(초). 스테이지를 깰 때마다 저장되므로 매번 올리지 않습니다.")]
    [SerializeField] private float uploadInterval = 60f;

    [Tooltip("실패 시 다시 시도하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 30f;

    public enum StartCheck { Proceed, Rebooting }

    private enum Decision { UseLocal, Same, UseCloud, Ask }

    /// <summary>받아 온 클라우드 데이터</summary>
    private class Remote
    {
        public bool     Exists;
        public string   Data;
        public string   Augments;
        public long     SavedAt;
        public SaveData Parsed;   // Data 를 읽은 것 (깨져 있으면 null)
    }

    /// <summary>올리는 JSON (JsonUtility 로 만듦 — Param 을 쓰지 않음: DateTime 직렬화 문제, 뒤끝 메모)</summary>
    [Serializable]
    private class Payload
    {
        public string data;
        public string augments;
        public long   savedAt;
        public int    world;
        public int    stage;
        public int    level;
        public string updatedAt;
    }

    private string checkedUid;        // 확인을 마친 계정 — 이 계정만 올림
    private string deferredUid;       // 게임 중 확인했지만 다음 실행으로 미룬 계정 (다시 묻지 않음)
    private bool   checking;
    private bool   uploading;
    private float  nextTryTime;
    private float  nextUploadTime;

    // 받아 오는 중인 요청 — 시간 초과면 결과를 기다리지 않고 끝냄 (늦게 온 응답은 requestId 로 버림)
    private int            requestId;
    private Action<Remote> fetchDone;
    private float          fetchDeadline;

    // 마지막으로 파일에 쓴 내용 (아직 올리지 않았으면 pendingJson != null)
    private string pendingJson;
    private long   pendingSavedAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>이 계정의 클라우드 확인을 마쳤는가. RankingManager 가 세이브 삭제 표시를 먼저 지우지 않게 기다릴 때 씁니다.</summary>
    public bool IsCheckedFor(string uid) => !string.IsNullOrEmpty(uid) && uid == checkedUid;

    /// <summary>올리는 중인가. 계정 삭제(ServerAccountData)가 지운 문서를 이 올리기가 되살리지 않게 기다릴 때 씁니다.</summary>
    public bool IsUploading => uploading;

    // ───────────────────────────── 게임 시작 전 확인 ─────────────────────────────

    /// <summary>씬 이동 직전 클라우드 확인 → onDone(Proceed=그대로 시작 / Rebooting=클라우드 적용 재시작 중이라 씬 이동 금지), 도중에 팝업이 뜰 수 있음.</summary>
    public void CheckBeforeStart(float timeoutSeconds, Action<StartCheck> onDone)
    {
        // 게스트 / 로그인 정보 없음 / 세이브 주인이 아직 다른 계정 → 클라우드와 상관없이 시작
        if (!ProfileSync.TryGetAccount(out string uid) || uid == checkedUid || checking)
        {
            onDone?.Invoke(StartCheck.Proceed);
            return;
        }

        Fetch(uid, timeoutSeconds, remote =>
        {
            if (this == null) return;

            if (remote == null)
            {
                Debug.LogWarning("[CloudSave] 클라우드 세이브를 확인하지 못해 이 기기 데이터로 시작합니다 (이번 실행에서는 올리지 않음).");
                onDone?.Invoke(StartCheck.Proceed);
                return;
            }

            // 기다리는 사이 계정이 바뀌었으면 이번 결과는 버림
            if (!ProfileSync.TryGetAccount(out string nowUid) || nowUid != uid)
            {
                onDone?.Invoke(StartCheck.Proceed);
                return;
            }

            switch (Decide(uid, remote))
            {
                case Decision.Same:
                case Decision.UseLocal:
                    AcceptLocal(uid, remote);
                    onDone?.Invoke(StartCheck.Proceed);
                    break;

                case Decision.UseCloud:
                    onDone?.Invoke(ApplyCloudAndRestart(uid, remote));
                    break;

                case Decision.Ask:
                    SaveData local = SaveManager.Instance.Current;
                    bool shown = CloudSaveConflictPopup.TryShow(Describe(local), Describe(remote.Parsed), useCloud =>
                    {
                        if (this == null) return;
                        if (useCloud)
                        {
                            onDone?.Invoke(ApplyCloudAndRestart(uid, remote));
                        }
                        else
                        {
                            AcceptLocal(uid, remote);
                            onDone?.Invoke(StartCheck.Proceed);
                        }
                    });

                    if (!shown)
                    {
                        // 팝업이 없으면 어느 쪽도 덮지 않음 — 이 기기 데이터로 시작하고 올리지 않음(다음 실행에 다시 물음)
                        Debug.LogError("[CloudSave] 저장 데이터 선택 팝업이 LoginScene 에 없습니다 (Tools/UI 굽기). 이번에는 이 기기 데이터로 시작하고 클라우드에 올리지 않습니다.");
                        deferredUid = uid;   // 게임 중 확인도 같은 결론이므로 다시 묻지 않음
                        onDone?.Invoke(StartCheck.Proceed);
                    }
                    break;
            }
        });
    }

    /// <summary>클래스 주석 [게임 시작 전 확인] ①~⑦</summary>
    private static Decision Decide(string uid, Remote remote)
    {
        SaveData local = SaveManager.Instance.Current;

        if (!remote.Exists)                       return Decision.UseLocal;   // ①
        if (remote.SavedAt == local.savedAt)      return Decision.Same;       // ②
        if (SaveManager.IsRankingResetPending(uid)) return Decision.UseLocal; // ③ 세이브 삭제 = 새로 시작하겠다는 뜻
        if (remote.Parsed == null)
        {
            // 클라우드 데이터가 깨짐 — 받을 수 없으므로 이 기기 것으로 덮어씀
            Debug.LogWarning("[CloudSave] 클라우드 세이브를 읽지 못해 이 기기 데이터를 씁니다.");
            return Decision.UseLocal;
        }
        if (IsEmpty(local))                       return Decision.UseCloud;   // ④

        // ⑤⑥ 은 한 번이라도 맞춘 적이 있어야 판단할 수 있음 (0 = 맞춘 적 없음 — 기능이 생기기 전 세이브 등)
        if (local.cloudSyncedAt != 0)
        {
            if (remote.SavedAt == local.cloudSyncedAt) return Decision.UseLocal;   // ⑤
            if (local.savedAt  == local.cloudSyncedAt) return Decision.UseCloud;   // ⑥
        }
        return Decision.Ask;                                                     // ⑦
    }

    /// <summary>새로 시작한 것과 다름없는 세이브인가 (새 기기·재설치)</summary>
    private static bool IsEmpty(SaveData d)
        => ProgressOf(d) == 0 && d.level <= 1 && (d.ownedCompanionIds == null || d.ownedCompanionIds.Count == 0);

    /// <summary>StageRecord.BestProgress 와 같은 계산을 임의의 SaveData 로</summary>
    private static int ProgressOf(SaveData d)
        => Mathf.Max(d.bestClearedProgress, StageFormula.Progress(d.currentWorld, d.currentStage));

    /// <summary>이 기기 것을 쓰기로 함 — 이제부터 이 계정은 올려도 됨. 클라우드가 있으면 그 버전을 기준으로 삼음.</summary>
    private void AcceptLocal(string uid, Remote remote)
    {
        checkedUid = uid;
        if (remote.Exists) SaveManager.Instance.MarkCloudSynced(remote.SavedAt);   // 트랜잭션이 "이 버전이면 덮어써도 됨" 으로 봄

        // 파일에 쓴 적은 있는데 아직 안 올렸으면 바로 올릴 수 있게
        nextUploadTime = 0f;
        GameLog.Info($"[CloudSave] 이 기기 세이브 사용 (클라우드 {(remote.Exists ? "있음" : "없음")})");
    }

    /// <summary>클라우드 세이브를 파일로 쓰고 매니저를 새로 만들어 다시 시작합니다. 실패하면 이 기기 것으로 시작.</summary>
    private StartCheck ApplyCloudAndRestart(string uid, Remote remote)
    {
        // 받은 데이터가 이제 "클라우드와 같은 버전" 이 되도록 표시를 맞춰서 씀
        SaveData d = remote.Parsed;
        d.savedAt       = remote.SavedAt;
        d.cloudSyncedAt = remote.SavedAt;
        string json = JsonUtility.ToJson(d, true);

        // ① 잠금 — 옛 매니저가 옛 값으로 파일을 덮어쓰지 못하게 (계정 전환과 같은 순서)
        if (!AccountReset.TryBeginCloudRestore(SceneLoader.LOGIN_SCENE, out string error))
        {
            Debug.LogError($"[CloudSave] 재시작 준비 실패 — 이 기기 데이터로 시작합니다: {error}");
            return StartCheck.Proceed;   // 확인을 마치지 않았으므로 이번 실행에서는 올리지 않음
        }

        // ② 파일 쓰기 — 실패하면 잠금을 풀고 이 기기 것으로 계속
        if (!SaveManager.TryWriteDownloadedSave(uid, json, out string writeError))
        {
            AccountReset.CancelBegin();
            Debug.LogError($"[CloudSave] 클라우드 세이브를 쓰지 못해 이 기기 데이터로 시작합니다: {writeError}");
            return StartCheck.Proceed;
        }
        AugmentManager.ImportFromCloud(uid, remote.Augments);

        // ③ 재시작 — 새 SaveManager 가 이 파일을 읽고 자동 시작 (그때 확인은 버전이 같아 바로 통과)
        GameLog.Info($"[CloudSave] 클라우드 세이브 적용 — 매니저를 새로 만들고 다시 시작합니다 ({Describe(d)})");
        AccountSwitch.RequestAutoStartAfterReload();
        AccountReset.RestartFromLogin();
        return StartCheck.Rebooting;
    }

    // ───────────────────────────── 받아 오기 ─────────────────────────────

    /// <summary>뒤끝 호출이 이 계정에 적용되는가 — 뒤끝은 '지금 로그인한 계정' 의 클라우드를 읽고 씀 (클래스 주석 ★).</summary>
    private static bool IsBackendAccount(string uid)
        => !string.IsNullOrEmpty(uid) && uid == Backend.UserInDate;

    /// <summary>클라우드 받기, 실패·시간 초과면 onDone(null) — ★ 404 "not exist file" 만 '없음'으로 봄 (실패를 없음으로 착각하면 클라우드를 덮어씀).</summary>
    private void Fetch(string uid, float timeoutSeconds, Action<Remote> onDone)
    {
        if (!IsBackendAccount(uid))
        {
            nextTryTime = Time.unscaledTime + 1f;   // 로그인 처리 중 — 곧 다시
            onDone?.Invoke(null);
            return;
        }

        checking      = true;
        fetchDone     = onDone;
        fetchDeadline = Time.unscaledTime + Mathf.Max(1f, timeoutSeconds);
        int id = ++requestId;

        BackendManager.Call(cb => Backend.CloudSave.Download(COLLECTION, cb), bro =>
        {
            if (this == null || id != requestId || !checking) return;   // 시간 초과로 이미 끝냄
            EndFetch(ToRemote(bro, "받아 오기"));
        });
    }

    /// <summary>받아 오기를 끝내고 결과를 알림. 실패(null)면 retryDelay 뒤에 다시 시도할 수 있게.</summary>
    private void EndFetch(Remote remote)
    {
        checking = false;
        requestId++;   // 아직 안 온 응답은 버림
        Action<Remote> done = fetchDone;
        fetchDone = null;
        if (remote == null) nextTryTime = Time.unscaledTime + retryDelay;
        done?.Invoke(remote);
    }

    /// <summary>Download 응답 → Remote. 클라우드에 없으면 Exists=false, 확인하지 못했으면 null.</summary>
    private static Remote ToRemote(BackendReturnObject bro, string what)
    {
        if (bro != null && bro.GetStatusCode() == "404")
        {
            string message = bro.GetMessage() ?? string.Empty;
            if (message.Contains("folder"))
            {
                Debug.LogError($"[CloudSave] 뒤끝 콘솔 > 클라우드 저장에 컬렉션 '{COLLECTION}' 이 없습니다. 만들어 주세요. (이번 실행에서는 올리지 않음)");
                return null;
            }
            return new Remote { Exists = false };   // "not exist file" — 이 계정은 아직 올린 적 없음
        }

        if (bro == null || !bro.IsSuccess())
        {
            Debug.LogWarning($"[CloudSave] {what} 실패: {BackendManager.Describe(bro)}");
            return null;
        }

        var r = new Remote { Exists = true };
        try
        {
            JsonData json = bro.GetReturnValuetoJSON();
            r.Data     = FromBase64(BackendJson.Str(json, "data"));
            r.Augments = FromBase64(BackendJson.Str(json, "augments"));
            r.SavedAt  = BackendJson.Long(json, "savedAt");
            r.Parsed   = string.IsNullOrEmpty(r.Data) ? null : JsonUtility.FromJson<SaveData>(r.Data);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CloudSave] 클라우드 세이브 읽기 실패: {e.Message}");
            r.Parsed = null;   // Decide 가 "깨짐 → 이 기기 것으로 덮어씀" 으로 처리
        }
        return r;
    }

    // ───────────────────────────── 올리기 ─────────────────────────────

    /// <summary>SaveManager 가 파일을 쓴 직후 부릅니다. 바로 올리지 않고 표시만 해 둡니다.</summary>
    public void NotifySaved(string json, long savedAt)
    {
        pendingJson    = json;
        pendingSavedAt = savedAt;
    }

    /// <summary>세이브 파일을 지웠을 때 — 아직 안 올린 옛 내용이 지운 뒤에 올라가지 않게 버립니다. (SaveManager.TryDeleteSaveFile)</summary>
    public void DiscardPending() => pendingJson = null;

    private void Update()
    {
        if (checking)
        {
            if (Time.unscaledTime >= fetchDeadline)
            {
                Debug.LogWarning("[CloudSave] 받아 오기 시간 초과");
                EndFetch(null);
            }
            return;
        }
        if (uploading || Time.unscaledTime < nextTryTime) return;
        if (!ProfileSync.TryGetAccount(out string uid)) return;

        if (uid != checkedUid)
        {
            RecheckInGame(uid);
            return;
        }

        if (pendingJson != null && Time.unscaledTime >= nextUploadTime)
            Upload(uid);
    }

    /// <summary>오프라인으로 시작했다 연결된 경우 게임 중 재확인 — 이 기기 것을 쓰는 결과만 확정하고, 클라우드가 필요하면 재시작할 수 없어 다음 실행으로 미룸.</summary>
    private void RecheckInGame(string uid)
    {
        if (uid == deferredUid) return;
        if (SaveManager.Instance == null || !SaveManager.Instance.IsGameplayStarted) return;   // LoginScene 에서는 CheckBeforeStart 가 함

        Fetch(uid, 10f, remote =>
        {
            if (this == null || remote == null) return;
            if (!ProfileSync.TryGetAccount(out string nowUid) || nowUid != uid) return;

            Decision d = Decide(uid, remote);
            if (d == Decision.Same || d == Decision.UseLocal)
            {
                AcceptLocal(uid, remote);
                return;
            }

            deferredUid = uid;
            GameLog.Info("[CloudSave] 클라우드에 다른 진행이 있어 다음 실행 때 확인합니다 (이번 실행에서는 올리지 않음).");
        });
    }

    private void Upload(string uid)
    {
        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null) return;

        if (!IsBackendAccount(uid)) return;   // 로그인 처리 중 — 다음 Update 에서 다시

        // 값은 지금 미리 만듦 — 응답을 기다리는 사이 세이브가 바뀌어도 '이 버전' 을 올림
        string json    = pendingJson;
        long   savedAt = pendingSavedAt;
        long   baseAt  = sm.Current.cloudSyncedAt;   // 이 버전이 클라우드에 있으면 덮어써도 됨
        var payload = new Payload
        {
            data      = ToBase64(json),
            augments  = ToBase64(AugmentManager.ExportForCloud(uid)),
            savedAt   = savedAt,
            world     = sm.Current.currentWorld,
            stage     = sm.Current.currentStage,
            level     = sm.Current.level,
            updatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
        };
        string body = JsonUtility.ToJson(payload);

        uploading      = true;
        pendingJson    = null;   // 올리는 도중 또 저장되면 다시 채워짐 → 끝난 뒤 다음 간격에 한 번 더
        nextUploadTime = Time.unscaledTime + uploadInterval;

        // ① 받아 와서 다른 기기가 그사이 올렸는지 확인 (트랜잭션 대신 — 클래스 주석 [올리기] ⚠)
        BackendManager.Call(cb => Backend.CloudSave.Download(COLLECTION, cb), bro =>
        {
            if (this == null) return;

            Remote remote = ToRemote(bro, "올리기 전 확인");
            if (remote == null) { FailUpload(json, savedAt); return; }

            if (remote.Exists && remote.SavedAt != baseAt && remote.SavedAt != savedAt)
            {
                // 다른 기기가 그사이 올림 — 덮어쓰지 않고, 이번 실행에서는 더 올리지 않음 → 다음 실행의 확인(⑦)에서 플레이어가 고름
                uploading   = false;
                checkedUid  = null;
                deferredUid = uid;
                Debug.LogWarning("[CloudSave] 다른 기기가 먼저 클라우드에 저장해 덮어쓰지 않았습니다. 다음 실행 때 어느 쪽을 쓸지 묻습니다.");
                return;
            }

            // 기다리는 사이 계정이 바뀌었으면 올리지 않음 (새 계정 클라우드를 덮어쓰게 됨)
            if (!IsBackendAccount(uid) || !ProfileSync.TryGetAccount(out string midUid) || midUid != uid)
            {
                uploading = false;
                return;
            }

            // ② 올리기
            BackendManager.Call(cb => Backend.CloudSave.Upload(COLLECTION, body, cb), up =>
            {
                if (this == null) return;

                if (up == null || !up.IsSuccess())
                {
                    if (up != null && up.GetStatusCode() == "400")
                        Debug.LogError($"[CloudSave] 서버가 세이브를 받지 않았습니다 (JSON 형식 또는 1MB 초과, {body.Length}자): {BackendManager.Describe(up)}");
                    else if (up != null && up.GetStatusCode() == "404")
                        Debug.LogError($"[CloudSave] 뒤끝 콘솔 > 클라우드 저장에 컬렉션 '{COLLECTION}' 이 없습니다. 만들어 주세요.");
                    FailUpload(json, savedAt);
                    return;
                }

                uploading = false;

                // 기다리는 사이 계정이 바뀌었으면 그 SaveManager 에 표시하지 않음
                if (ProfileSync.TryGetAccount(out string nowUid) && nowUid == uid)
                    SaveManager.Instance.MarkCloudSynced(savedAt);

                GameLog.Info($"[CloudSave] 올리기 완료 ({payload.world}-{payload.stage}, Lv.{payload.level})");
            });
        });
    }

    /// <summary>문자열 → UTF-8 Base64 (클래스 주석 [저장 구조] ★). 비어 있으면 빈 문자열.</summary>
    private static string ToBase64(string s)
        => string.IsNullOrEmpty(s) ? string.Empty : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s));

    /// <summary>Base64 → 문자열. Base64 가 아니면(직접 넣은 값 등) 그대로 돌려줌.</summary>
    private static string FromBase64(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        try   { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
        catch (FormatException) { return s; }
    }

    private void FailUpload(string json, long savedAt)
    {
        Debug.LogWarning($"[CloudSave] 올리기 실패 — {retryDelay}초 뒤 재시도");
        uploading   = false;
        RestorePending(json, savedAt);
        nextTryTime = Time.unscaledTime + retryDelay;
    }

    /// <summary>올리기 실패 — 그사이 더 새로 저장된 게 없으면 방금 것을 다시 올릴 대상으로 되돌림</summary>
    private void RestorePending(string json, long savedAt)
    {
        if (pendingJson != null) return;
        pendingJson    = json;
        pendingSavedAt = savedAt;
    }

    private void OnApplicationPause(bool paused)
    {
        // 백그라운드 전환 시 간격 없이 바로 올림 (멈추기 전 Update 가 안 돌 수 있어서 직접 호출, 못 올려도 다음 실행에 이어 올림)
        if (!paused) return;
        nextUploadTime = 0f;
        if (!checking && !uploading && pendingJson != null &&
            ProfileSync.TryGetAccount(out string uid) && uid == checkedUid)
            Upload(uid);
    }

    // ───────────────────────────── 표시 ─────────────────────────────

    /// <summary>팝업·로그용 요약</summary>
    private static string Describe(SaveData d)
    {
        if (d == null) return "데이터 없음";

        string when = d.savedAt > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(d.savedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "알 수 없음";
        int companions = d.ownedCompanionIds != null ? d.ownedCompanionIds.Count : 0;

        return $"스테이지 {d.currentWorld}-{d.currentStage}  (최고 {StageRecord.Format(ProgressOf(d))})\n" +
               $"레벨 {d.level}   골드 {d.gold}   보석 {d.gem}   동료 {companions}명\n" +
               $"저장 시각 {when}";
    }
}

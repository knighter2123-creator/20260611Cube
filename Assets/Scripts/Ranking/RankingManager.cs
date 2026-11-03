using System;
using System.Collections.Generic;
using BackEnd;
using LitJson;
using UnityEngine;

/// <summary>
/// 주간 스테이지 랭킹 — 뒤끝 유저 랭킹에 내 기록을 올리고, 상위 기록을 받아 옵니다.
///
/// [붙이는 위치]
///   로그인 씬의 ManagerRoot 하위. 다른 매니저들과 같은 DontDestroyOnLoad 싱글턴이다.
///   우편함 동기화(MailboxSync)도 Awake 에서 같은 오브젝트에 붙입니다.
///
/// [뒤끝 콘솔 준비] — 인스펙터의 rankUuid / tableName / column 과 맞춰야 합니다.
///   1) 게임 정보 관리 > 테이블 생성: tableName (기본 "RANK_DATA"), 컬럼 column (기본 "progress", int)
///   2) 랭킹 관리 > 유저 랭킹 생성: 위 테이블·컬럼, 내림차순, 초기화 주기 '주간' (가능하면 월요일 06:00)
///   3) 같은 랭킹의 보상 설정 — 순위 구간별 우편 보상 (아이템 차트 형식은 BackendMailService 주석)
///   4) 생성된 랭킹 UUID 를 rankUuid 에 넣기
///
/// [점수]  StageRecord.BestProgress (누적 클리어 수, 1-1 = 1). 기록은 누적이지만 랭킹은 매주 비워지므로,
///   그 주에 한 번이라도 올린 사람만 이번 주 랭킹·보상에 들어갑니다. 그래서 주차가 바뀌면 다시 올립니다.
///
/// [언제 올리는가]  "올려야 함(dirty)" 표시만 해 두고 Update 에서 조건이 맞을 때 한 번에 올립니다.
///   - 최고 기록 갱신 (StageRecord.OnImproved) / 로그인 상태 변화 / 앱 시작 직후 1회 / 랭킹 주차가 바뀜
///   조건: 로그인(온라인) + 게임 씬 진입(세이브 적용 후) + 깬 스테이지가 1개 이상
///   테이블 값이 이 기기보다 높으면(다른 기기에서 더 멀리 감) 그 값을 유지합니다.
///
/// [세이브 삭제 후 랭킹 초기화]  세이브를 지운 계정(SaveManager.IsRankingResetPending)이 아직 1-1 이면
///   테이블의 내 기록을 0 으로 내립니다 — 안 내리면 "테이블 값이 더 높으면 유지" 때문에 예전 기록이 다시 올라갑니다.
///   (이번 주 랭킹에 이미 올라간 줄은 다음 초기화 때 사라집니다)
///
/// ⚠ 기록은 클라이언트가 올리는 값을 그대로 믿습니다.
/// </summary>
public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance { get; private set; }

    /// <summary>옛 Firestore 컬렉션 이름 — ServerAccountData 의 Firestore 경로만 씀 (LegacyFirestore 를 지울 때 함께 지움)</summary>
    public const string COLLECTION = "rankings";

    [Header("뒤끝 랭킹 (클래스 주석 [뒤끝 콘솔 준비])")]
    [Tooltip("뒤끝 콘솔 > 랭킹 관리 에서 만든 주간 유저 랭킹의 UUID")]
    [SerializeField] private string rankUuid = "";
    [Tooltip("랭킹이 읽는 게임 정보 테이블")]
    [SerializeField] private string tableName = "RANK_DATA";
    [Tooltip("점수 컬럼 (int) — StageRecord.BestProgress 를 넣음")]
    [SerializeField] private string column = "progress";

    [Tooltip("랭킹 화면에 보여 줄 인원")]
    [SerializeField] private int topCount = 50;

    [Tooltip("업로드 실패 시 다시 시도하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 30f;

    public int TopCount => topCount;

    /// <summary>랭킹 한 줄.</summary>
    public struct Entry
    {
        public int    Rank;
        public string Uid;        // 뒤끝 gamerInDate (= AuthManager.Uid)
        public string Nickname;
        public int    Progress;   // StageRecord.Format(Progress) → "2-3"
    }

    // ── 업로드 상태 ──
    private bool   dirty = true;   // 앱 시작 직후 1회 동기화
    private bool   uploading;
    private bool   resetting;
    private float  nextTryTime;
    private bool   warnedNoUuid;

    private string rowUid;          // rowInDate 가 어느 계정 것인가
    private string rowInDate;       // 테이블의 내 행
    private int    uploadedWeek = int.MinValue;   // 마지막으로 올린 랭킹 주차 (RankingWeek) — 바뀌면 다시 올림
    private string uploadedUid;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 우편함 서버 동기화 — 이 오브젝트와 수명을 같이 함
        if (GetComponent<MailboxSync>() == null) gameObject.AddComponent<MailboxSync>();
    }

    private void OnEnable()
    {
        StageRecord.OnImproved   += MarkDirty;
        AuthManager.StateChanged += MarkDirty;
    }

    private void OnDisable()
    {
        StageRecord.OnImproved   -= MarkDirty;
        AuthManager.StateChanged -= MarkDirty;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>다음 기회에 내 기록을 다시 올리게 합니다.</summary>
    public void RequestUpload() => dirty = true;

    private void MarkDirty() => dirty = true;

    private bool IsConfigured
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(rankUuid)) return true;
            if (!warnedNoUuid)
            {
                warnedNoUuid = true;
                Debug.LogWarning("[Ranking] rankUuid 가 비어 있어 랭킹을 쓰지 않습니다. RankingManager 인스펙터에 뒤끝 랭킹 UUID 를 넣으세요.", this);
            }
            return false;
        }
    }

    // ───────────────────────────── 업로드 ─────────────────────────────

    private void Update()
    {
        if (uploading || resetting || Time.unscaledTime < nextTryTime) return;
        if (!IsConfigured) return;

        if (TryResetAfterSaveDelete()) return;   // 초기화가 먼저 — 그 전에 올리면 예전 기록이 유지됨

        if (!CanUpload(out string uid)) return;

        // 이번 주에 아직 안 올렸으면 올림 (주간 랭킹은 매주 비워짐)
        if (uid != uploadedUid || RankingWeek.Current != uploadedWeek) dirty = true;
        if (!dirty) return;

        Upload(uid);
    }

    /// <summary>지금 내 기록을 올릴 수 있는가. 조건이 안 되면 dirty 를 유지한 채 기다립니다.</summary>
    private static bool CanUpload(out string uid)
    {
        // 로그인 + 지금 세이브의 주인이 그 계정 (다른 계정 세이브의 기록을 올리지 않게 — ProfileSync 주석 ★ 참고)
        if (!ProfileSync.TryGetAccount(out uid)) return false;
        if (!AuthManager.Instance.IsOnline) return false;

        // 게임 씬 진입 전에는 세이브가 매니저에 적용되기 전일 수 있음 (SaveManager.IsGameplayStarted 주석 참고)
        if (!SaveManager.Instance.IsGameplayStarted) return false;

        return StageRecord.HasRecord;   // 아직 깬 스테이지가 없으면 랭킹에 안 올림
    }

    /// <summary>테이블의 내 행을 찾거나 만들고, onDone(inDate, 테이블에 있던 점수, 실패 문구).</summary>
    private void EnsureRow(string uid, int initialProgress, Action<string, int, string> onDone)
    {
        if (rowUid == uid && !string.IsNullOrEmpty(rowInDate))
        {
            onDone(rowInDate, -1, null);   // -1 = 테이블 값을 모름 (이번 실행에서 이미 올렸으므로 다시 읽지 않음)
            return;
        }

        BackendTable.GetOrInsertMyRow(tableName, () =>
        {
            var p = new Param();
            p.Add(column, initialProgress);
            return p;
        },
        (inDate, row, error) =>
        {
            if (error != null) { onDone(null, 0, error); return; }
            rowUid    = uid;
            rowInDate = inDate;
            onDone(inDate, row != null ? (int)BackendJson.Long(row, column) : 0, null);
        });
    }

    private void Upload(string uid)
    {
        int progress = StageRecord.BestProgress;
        int week     = RankingWeek.Current;

        dirty     = false;   // 올리는 도중 또 바뀌면 다시 true → 끝난 뒤 한 번 더 올림
        uploading = true;

        EnsureRow(uid, progress, (inDate, tableProgress, error) =>
        {
            if (this == null) return;
            if (error != null) { FailUpload(error); return; }

            // 다른 기기에서 더 멀리 갔으면 그 기록을 유지 (진행도는 기기마다 따로일 수 있음 — 클라우드 세이브 전까지)
            int score = Mathf.Max(progress, tableProgress);

            var param = new Param();
            param.Add(column, score);

            BackendManager.Call(cb => Backend.URank.User.UpdateUserScore(rankUuid, tableName, inDate, param, cb), bro =>
            {
                if (this == null) return;

                if (bro == null || !bro.IsSuccess())
                {
                    // 행이 지워졌을 수 있음 → 다음엔 다시 찾음
                    if (bro != null && bro.GetStatusCode() == "404") rowInDate = null;
                    FailUpload($"랭킹 갱신 실패: {BackendManager.Describe(bro)}");
                    return;
                }

                uploading    = false;
                uploadedUid  = uid;
                uploadedWeek = week;
                GameLog.Info($"[Ranking] 업로드 완료: {StageRecord.Format(score)} ({week}주차)");
            });
        });
    }

    private void FailUpload(string error)
    {
        Debug.LogWarning($"[Ranking] 업로드 실패 — {retryDelay}초 뒤 재시도: {error}");
        uploading   = false;
        dirty       = true;
        nextTryTime = Time.unscaledTime + retryDelay;
    }

    /// <summary>세이브를 지운 계정이 아직 1-1 이면 테이블의 내 기록을 0 으로 (클래스 주석). 시작했으면 true.</summary>
    private bool TryResetAfterSaveDelete()
    {
        if (!ProfileSync.TryGetAccount(out string uid)) return false;
        if (!SaveManager.IsRankingResetPending(uid)) return false;
        if (!SaveManager.Instance.IsGameplayStarted || !AuthManager.Instance.IsOnline) return false;

        // 클라우드 확인이 먼저 — 이 표시는 CloudSave 에게 "세이브 삭제로 새로 시작했다" 는 뜻이기도 함 (CloudSave 주석).
        // TODO(5단계 C — 클라우드 세이브): 뒤끝 클라우드 세이브로 바꾸면 LegacyFirestore 조건을 지움
        if (LegacyFirestore.Enabled && CloudSave.Instance != null && !CloudSave.Instance.IsCheckedFor(uid)) return false;

        if (StageRecord.HasRecord)
        {
            SaveManager.SetRankingResetPending(uid, false);
            GameLog.Info("[Ranking] 세이브를 지웠지만 이미 1-1 을 넘겨 랭킹 초기화를 건너뜁니다.");
            return false;
        }

        resetting = true;
        BackendTable.GetMyRow(tableName, (row, error) =>
        {
            if (this == null) return;
            if (error != null) { FailReset(error); return; }

            string inDate = BackendJson.Str(row, "inDate");
            if (string.IsNullOrEmpty(inDate)) { FinishReset(uid); return; }   // 올린 적 없음 — 지울 것 없음

            var param = new Param();
            param.Add(column, 0);
            BackendTable.Update(tableName, inDate, param, updateError =>
            {
                if (this == null) return;
                if (updateError != null) FailReset(updateError);
                else FinishReset(uid);
            });
        });
        return true;
    }

    private void FinishReset(string uid)
    {
        resetting = false;
        SaveManager.SetRankingResetPending(uid, false);
        dirty = true;   // 플레이 중 '세이브 삭제' 후 진행이 되돌아오는 경우 대비 (예전과 같은 이유)
        GameLog.Info("[Ranking] 세이브 삭제 후 1-1 — 랭킹 기록 초기화 완료");
    }

    private void FailReset(string error)
    {
        Debug.LogWarning($"[Ranking] 랭킹 초기화 실패 — {retryDelay}초 뒤 재시도: {error}");
        resetting   = false;
        nextTryTime = Time.unscaledTime + retryDelay;
    }

    // ───────────────────────────── 조회 ─────────────────────────────

    /// <summary>
    /// 이번 주 상위 topCount 명. 결과는 메인 스레드에서 onDone(목록, 오류 문구) 로 옵니다.
    /// 성공이면 오류 문구가 null, 실패면 목록이 null.
    /// </summary>
    public void FetchTop(Action<List<Entry>, string> onDone)
    {
        if (!CheckCanFetch(out string reason)) { onDone?.Invoke(null, reason); return; }

        BackendManager.Call(cb => Backend.URank.User.GetRankList(rankUuid, topCount, cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogError($"[Ranking] 조회 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(null, "랭킹을 불러오지 못했습니다.");
                return;
            }

            var list = new List<Entry>();
            JsonData rows = bro.FlattenRows();
            if (rows != null && rows.IsArray)
                foreach (JsonData row in rows) list.Add(ToEntry(row, list.Count + 1));

            onDone?.Invoke(list, null);
        });
    }

    /// <summary>이번 주 내 순위. onDone(내 줄 — 이번 주 기록이 없으면 null, 오류 문구)</summary>
    public void FetchMine(Action<Entry?, string> onDone)
    {
        if (!CheckCanFetch(out string reason)) { onDone?.Invoke(null, reason); return; }

        BackendManager.Call(cb => Backend.URank.User.GetMyRank(rankUuid, cb), bro =>
        {
            if (this == null) return;

            if (bro != null && bro.GetStatusCode() == "404") { onDone?.Invoke(null, null); return; }   // 이번 주 기록 없음
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Ranking] 내 순위 조회 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(null, "내 순위를 불러오지 못했습니다.");
                return;
            }

            JsonData rows = bro.FlattenRows();
            onDone?.Invoke(rows != null && rows.IsArray && rows.Count > 0 ? ToEntry(rows[0], 0) : (Entry?)null, null);
        });
    }

    private bool CheckCanFetch(out string reason)
    {
        reason = null;
        if (!IsConfigured)                                                   reason = "랭킹이 아직 준비되지 않았습니다.";
        else if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn) reason = "로그인하면 랭킹을 볼 수 있습니다.";
        else if (!AuthManager.Instance.IsOnline)                              reason = "서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";
        return reason == null;
    }

    private static Entry ToEntry(JsonData row, int fallbackRank)
    {
        string nickname = BackendJson.Str(row, "nickname");
        return new Entry
        {
            Rank     = (int)BackendJson.Long(row, "rank", fallbackRank),
            Uid      = BackendJson.Str(row, "gamerInDate"),
            Nickname = string.IsNullOrEmpty(nickname) ? PlayerProfile.ANONYMOUS_PREFIX : nickname,
            Progress = (int)BackendJson.Long(row, "score"),
        };
    }
}

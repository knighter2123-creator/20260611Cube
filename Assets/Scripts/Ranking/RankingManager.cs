using System;
using System.Collections.Generic;
using BackEnd;
using BackEnd.Leaderboard;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>주간 스테이지 랭킹: 기록이 바뀌면 dirty 표시 → uploadDelay 뒤 Update 에서 뒤끝 유저 리더보드에 BestProgress 를 올림 (주간 초기화라 주차가 바뀌면 다시 올림).</summary>
// [붙이는 위치] 로그인 씬 ManagerRoot 하위 DontDestroyOnLoad 싱글턴 (MailboxSync 도 Awake 에서 같이 붙임)
// [뒤끝 콘솔 준비] ① 테이블 RANK_DATA: progress(int) + world/stage(int)·clearedAt(dateTime) ② 유저 리더보드: 내림차순·주간 초기화(월 06:00) ③ 순위 보상 우편 ④ UUID → leaderboardUuid
// ⚠ 테이블 값이 더 높으면 유지(다른 기기 기록 보호) → 세이브 삭제 후 1-1 이면 테이블을 0 으로 내려야 예전 기록이 안 살아남 / 클라이언트 값을 그대로 믿음
public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance { get; private set; }


    [Header("뒤끝 리더보드 (클래스 주석 [뒤끝 콘솔 준비])")]
    [Tooltip("뒤끝 콘솔 > 리더보드 에서 만든 주간 유저 리더보드의 UUID")]
    [FormerlySerializedAs("rankUuid")]
    [SerializeField] private string leaderboardUuid = "01a1a011-278d-7957-8c07-c6f5b3dd141c";
    [Tooltip("랭킹이 읽는 게임 정보 테이블")]
    [SerializeField] private string tableName = "RANK_DATA";
    [Tooltip("점수 컬럼 (int) — StageRecord.BestProgress 를 넣음")]
    [SerializeField] private string column = "progress";

    /// <summary>점수와 함께 넣는 스테이지 정보 컬럼 — 정렬엔 안 쓰고 콘솔에서 읽기 쉽게 (스키마 테이블이면 콘솔에도 만들 것).</summary>
    public const string COL_WORLD      = "world";
    public const string COL_STAGE      = "stage";
    public const string COL_CLEARED_AT = "clearedAt";

    /// <summary>RANK_DATA 한 행 Param: progress 로 world/stage 를 계산하고, clearedAt 은 UTC 문자열로 (0 이하면 1970년이 안 들어가게 생략).</summary>
    // ⚠ 스키마 DateTime 컬럼은 콘솔 형식과 똑같은 문자열만 받음(아니면 400 bad time dataType) → 형식 바꾸면 CLEARED_AT_FORMAT 도 변경
    // ⚠ DateTime 을 Param 에 직접 넣지 말 것 — "11/03/2026 06:00:00" 으로 직렬화됨. 읽을 때는 BackendJson.DateMs
    public static Param BuildStageParam(string scoreColumn, int progress, long clearedAtMs)
    {
        StageRecord.Split(progress, out int world, out int stage);
        var p = new Param();
        p.Add(scoreColumn, progress);
        p.Add(COL_WORLD,   progress > 0 ? world : 0);
        p.Add(COL_STAGE,   progress > 0 ? stage : 0);
        if (clearedAtMs > 0) p.Add(COL_CLEARED_AT, ToClearedAtString(clearedAtMs));
        return p;
    }

    /// <summary>콘솔 RANK_DATA.clearedAt 의 DateTime 형식 "YYYY-MM-DD HH:mm:ss" 를 .NET 형식으로 쓴 것.</summary>
    public const string CLEARED_AT_FORMAT = "yyyy-MM-dd HH:mm:ss";

    /// <summary>UTC unix ms → "2026-11-03 06:00:00" (CLEARED_AT_FORMAT, UTC)</summary>
    public static string ToClearedAtString(long ms)
        => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
               .ToString(CLEARED_AT_FORMAT, System.Globalization.CultureInfo.InvariantCulture);

    [Tooltip("랭킹 화면에 보여 줄 인원")]
    [SerializeField] private int topCount = 50;

    [Tooltip("업로드 실패 시 다시 시도하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 30f;

    [Tooltip("올릴 일이 생긴 뒤 실제로 올리기까지 기다리는 시간(초). 스테이지 전환 프레임과 겹치지 않게 하고, 그 사이 바뀐 기록은 한 번에 올림")]
    [SerializeField] private float uploadDelay = 3f;

    [Tooltip("랭킹 주차가 바뀌었는지 확인하는 간격(초) — 매 프레임 확인할 필요 없음")]
    [SerializeField] private float weekCheckInterval = 30f;

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
    private float  dirtyAt = float.NegativeInfinity;   // dirty 가 처음 켜진 시각 — 앱 시작 직후 1회는 기다리지 않음
    private float  nextWeekCheckTime;
    private int    currentWeek = int.MinValue;          // weekCheckInterval 마다 갱신하는 RankingWeek.Current
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
    public void RequestUpload() => MarkDirty();

    /// <summary>올릴 일이 생겼음. 실제 업로드는 처음 표시한 때로부터 uploadDelay 뒤 (그 사이 표시는 한 번으로 합쳐짐).</summary>
    private void MarkDirty()
    {
        if (dirty) return;
        dirty   = true;
        dirtyAt = Time.unscaledTime;
    }

    private bool IsConfigured
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(leaderboardUuid)) return true;
            if (!warnedNoUuid)
            {
                warnedNoUuid = true;
                Debug.LogWarning("[Ranking] leaderboardUuid 가 비어 있어 랭킹을 쓰지 않습니다. RankingManager 인스펙터에 뒤끝 리더보드 UUID 를 넣으세요.", this);
            }
            return false;
        }
    }

    // ───────────────────────────── 업로드 ─────────────────────────────

    private void Update()
    {
        if (uploading || resetting || Time.unscaledTime < nextTryTime) return;
        if (!IsConfigured) return;

        // 초기화가 먼저(먼저 올리면 예전 기록 유지) — IsRankingResetPending 은 캐시라 매 프레임 불러도 가벼움
        if (TryResetAfterSaveDelete()) return;

        // 주차는 weekCheckInterval 마다만 다시 계산
        float now = Time.unscaledTime;
        if (now >= nextWeekCheckTime)
        {
            nextWeekCheckTime = now + weekCheckInterval;
            currentWeek       = RankingWeek.Current;
        }

        if (!CanUpload(out string uid)) return;

        // 이번 주에 아직 안 올렸으면 올림 (주간 랭킹은 매주 비워짐)
        if (uid != uploadedUid || currentWeek != uploadedWeek) MarkDirty();
        if (!dirty) return;

        // 스테이지 전환 직후 같은 바쁜 프레임을 피해 조금 기다렸다가 올림
        if (now - dirtyAt < uploadDelay) return;

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

        BackendTable.GetOrInsertMyRow(tableName,
            () => BuildStageParam(column, initialProgress, StageRecord.BestClearedAt),
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
        int week     = currentWeek = RankingWeek.Current;   // 올리는 순간의 주차 (주기 확인 값이 늦어서 한 번 더 올리지 않게)

        dirty     = false;   // 올리는 도중 또 바뀌면 다시 true → 끝난 뒤 한 번 더 올림
        uploading = true;

        EnsureRow(uid, progress, (inDate, tableProgress, error) =>
        {
            if (this == null) return;
            if (error != null) { FailUpload(error); return; }

            // 다른 기기에서 더 멀리 갔으면 그 기록을 유지 (진행도는 기기마다 따로일 수 있음 — 클라우드 세이브 전까지)
            int score = Mathf.Max(progress, tableProgress);

            // 이 기기 기록이 이기면 스테이지 정보까지 갱신, 테이블이 높으면 행의 world/stage 가 이미 맞으므로 점수만 보냄
            Param param;
            if (score == progress)
            {
                param = BuildStageParam(column, score, StageRecord.BestClearedAt);
            }
            else
            {
                param = new Param();
                param.Add(column, score);
            }

            BackendManager.Call<BackendReturnObject>(cb => Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(leaderboardUuid, tableName, inDate, param, cb), bro =>
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
        if (CloudSave.Instance != null && !CloudSave.Instance.IsCheckedFor(uid)) return false;

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

            var param = BuildStageParam(column, 0, 0);
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

    /// <summary>이번 주 상위 topCount 명을 메인 스레드에서 onDone(목록, 오류 문구)로 — 성공이면 오류가 null, 실패면 목록이 null.</summary>
    public void FetchTop(Action<List<Entry>, string> onDone)
    {
        if (!CheckCanFetch(out string reason)) { onDone?.Invoke(null, reason); return; }

        BackendManager.Call<BackendUserLeaderboardReturnObject>(
            cb => Backend.Leaderboard.User.GetLeaderboard(leaderboardUuid, topCount, cb), bro =>
        {
            if (this == null) return;

            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogError($"[Ranking] 조회 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(null, GameMessages.Ranking.fetchFailed);
                return;
            }

            var list = new List<Entry>();
            List<UserLeaderboardItem> items = bro.GetUserLeaderboardList();
            if (items != null)
                foreach (UserLeaderboardItem item in items) list.Add(ToEntry(item, list.Count + 1));

            onDone?.Invoke(list, null);
        });
    }

    /// <summary>이번 주 내 순위. onDone(내 줄 — 이번 주 기록이 없으면 null, 오류 문구)</summary>
    public void FetchMine(Action<Entry?, string> onDone)
    {
        if (!CheckCanFetch(out string reason)) { onDone?.Invoke(null, reason); return; }

        BackendManager.Call<BackendUserLeaderboardReturnObject>(
            cb => Backend.Leaderboard.User.GetMyLeaderboard(leaderboardUuid, cb), bro =>
        {
            if (this == null) return;

            if (bro != null && bro.GetStatusCode() == "404") { onDone?.Invoke(null, null); return; }   // 이번 주 기록 없음
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Ranking] 내 순위 조회 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(null, GameMessages.Ranking.mineFailed);
                return;
            }

            // gap 없이 부르면 내 줄 하나 — 혹시 앞뒤가 섞여 와도 내 gamerInDate 로 고름. 비었으면 이번 주 기록 없음
            List<UserLeaderboardItem> items = bro.GetUserLeaderboardList();
            UserLeaderboardItem mine = null;
            if (items != null)
                foreach (UserLeaderboardItem item in items)
                    if (mine == null || item.gamerInDate == Backend.UserInDate) mine = item;

            onDone?.Invoke(mine != null ? ToEntry(mine, 0) : (Entry?)null, null);
        });
    }

    private bool CheckCanFetch(out string reason)
    {
        reason = null;
        if (!IsConfigured)                                                   reason = GameMessages.Ranking.notConfigured;
        else if (AuthManager.Instance == null || !AuthManager.Instance.IsSignedIn) reason = GameMessages.Ranking.loginRequired;
        else if (!AuthManager.Instance.IsOnline)                              reason = GameMessages.Ranking.offline;
        return reason == null;
    }

    /// <summary>리더보드 항목은 숫자도 문자열로 옵니다 (rank/score). 점수 컬럼이 int 라 소수점은 없지만 "13.0" 같은 형식도 받아 줌.</summary>
    private static Entry ToEntry(UserLeaderboardItem item, int fallbackRank)
    {
        return new Entry
        {
            Rank     = ParseInt(item.rank, fallbackRank),
            Uid      = item.gamerInDate,
            Nickname = string.IsNullOrEmpty(item.nickname) ? PlayerProfile.ANONYMOUS_PREFIX : item.nickname,
            Progress = ParseInt(item.score, 0),
        };
    }

    private static int ParseInt(string s, int fallback)
    {
        if (int.TryParse(s, out int i)) return i;
        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double d)) return (int)d;
        return fallback;
    }
}

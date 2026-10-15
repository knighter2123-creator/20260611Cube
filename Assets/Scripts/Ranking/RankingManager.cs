using System;
using System.Collections.Generic;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// 스테이지 랭킹 — Firestore 에 내 기록을 올리고, 상위 기록을 받아 옵니다.
///
/// [붙이는 위치]
///   로그인 씬의 ManagerRoot 하위. 다른 매니저들과 같은 DontDestroyOnLoad 싱글턴이다.
///
/// [하는 일]  업로드 / 상위 N명 조회.  [안 하는 일]  UI — RankingPanel 이 FetchTop 을 불러 그립니다.
///
/// [Firestore 구조]
///   rankings/{Firebase UID}
///     nickname  : string  — PlayerProfile.DisplayName (이름이 없으면 "익명1234")
///     world     : int     — 최고 클리어 월드   (표시용)
///     stage     : int     — 최고 클리어 스테이지 (표시용)
///     progress  : int     — 누적 클리어 수 (정렬 기준, StageRecord 참고)
///     reachedAt : int     — 그 기록을 세운 시각 (UTC ms, 동점일 때 먼저 깬 사람이 위)
///     updatedAt : timestamp (서버 시각, 관리용)
///   문서 ID 가 UID 라서 계정당 한 줄만 생기고, 다시 올리면 덮어씁니다.
///
/// [언제 올리는가]  "올려야 함(dirty)" 표시만 해 두고 Update 에서 조건이 맞을 때 한 번에 올립니다.
///   - 최고 기록 갱신 (StageRecord.OnImproved)
///   - 닉네임 등록 / 변경 (PlayerProfile 이벤트)
///   - 로그인 상태 변화 (AuthManager.StateChanged) — 로그인·계정 전환 직후 내 기록을 맞춤
///   - 앱 시작 직후 1회
///   조건: Firebase 준비 + 로그인 + 게임 씬 진입(세이브 적용 후) + 깬 스테이지가 1개 이상
///
/// ⚠ 기록은 클라이언트가 올리는 값을 그대로 믿습니다. 보안 규칙은 "남의 문서는 못 쓴다" 까지만 막습니다.
/// </summary>
public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance { get; private set; }

    public const string COLLECTION = "rankings";

    [Tooltip("랭킹 화면에 보여 줄 인원")]
    [SerializeField] private int topCount = 50;

    [Tooltip("업로드 실패 시 다시 시도하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 30f;

    public int TopCount => topCount;

    /// <summary>랭킹 한 줄.</summary>
    public struct Entry
    {
        public int    Rank;
        public string Uid;
        public string Nickname;
        public int    Progress;   // StageRecord.Format(Progress) → "2-3"
    }

    // ── 업로드 상태 ──
    private bool  dirty = true;   // 앱 시작 직후 1회 동기화
    private bool  uploading;
    private float nextTryTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        StageRecord.OnImproved         += MarkDirty;
        PlayerProfile.OnNameRegistered += MarkDirtyName;
        PlayerProfile.OnNameChanged    += MarkDirtyName;
        AuthManager.StateChanged       += MarkDirty;
    }

    private void OnDisable()
    {
        StageRecord.OnImproved         -= MarkDirty;
        PlayerProfile.OnNameRegistered -= MarkDirtyName;
        PlayerProfile.OnNameChanged    -= MarkDirtyName;
        AuthManager.StateChanged       -= MarkDirty;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>다음 기회에 내 기록을 다시 올리게 합니다. (서버 기록 삭제가 실패했을 때 — ServerAccountData)</summary>
    public void RequestUpload() => dirty = true;

    private void MarkDirty()              => dirty = true;
    private void MarkDirtyName(string _)  => dirty = true;

    // ───────────────────────────── 업로드 ─────────────────────────────

    private void Update()
    {
        if (!dirty || uploading || Time.unscaledTime < nextTryTime) return;
        if (!CanUpload(out string uid)) return;

        Upload(uid);
    }

    private static bool IsFirebaseReady
        => AuthManager.Instance != null && AuthManager.Instance.IsReady;

    /// <summary>지금 내 기록을 올릴 수 있는가. 조건이 안 되면 dirty 를 유지한 채 기다립니다.</summary>
    private static bool CanUpload(out string uid)
    {
        // 로그인 + 지금 세이브의 주인이 그 계정 (다른 계정 세이브의 기록을 올리지 않게 — ProfileSync 주석 ★ 참고)
        if (!ProfileSync.TryGetAccount(out uid)) return false;

        // 서버 닉네임을 받아 오기 전에 올리면 새 기기의 "익명1234" 로 랭킹 이름을 덮어씀 → 받아 온 뒤에 올림
        if (ProfileSync.Instance != null && !ProfileSync.Instance.IsPulledFor(uid)) return false;

        // 게임 씬 진입 전에는 세이브가 매니저에 적용되기 전일 수 있음 (SaveManager.IsGameplayStarted 주석 참고)
        if (!SaveManager.Instance.IsGameplayStarted) return false;

        return StageRecord.HasRecord;   // 아직 깬 스테이지가 없으면 랭킹에 안 올림
    }

    private void Upload(string uid)
    {
        int progress = StageRecord.BestProgress;
        StageRecord.Split(progress, out int world, out int stage);

        string nickname = PlayerProfile.DisplayName;

        var data = new Dictionary<string, object>
        {
            { "nickname",  nickname },
            { "world",     world },
            { "stage",     stage },
            { "progress",  progress },
            { "reachedAt", StageRecord.BestClearedAt },
            { "updatedAt", FieldValue.ServerTimestamp },
        };
        var nameOnly = new Dictionary<string, object>
        {
            { "nickname",  nickname },
            { "updatedAt", FieldValue.ServerTimestamp },
        };

        dirty     = false;   // 올리는 도중 또 바뀌면 다시 true → 끝난 뒤 한 번 더 올림
        uploading = true;

        try
        {
            DocumentReference docRef = FirebaseFirestore.DefaultInstance.Collection(COLLECTION).Document(uid);

            // ★ 트랜잭션 — 서버 기록이 더 높으면 기록은 두고 닉네임만 바꿉니다.
            //   진행도는 기기마다 따로 저장되므로, 새 기기는 1-1 부터 시작합니다.
            //   그냥 덮어쓰면 다른 기기에서 세운 5-3 기록이 새 기기의 1-1 로 내려갑니다.
            //   (이 안의 코드는 백그라운드 스레드에서 돌 수 있어 Unity API 를 쓰지 않습니다 — 값은 위에서 미리 만듦)
            FirebaseFirestore.DefaultInstance.RunTransactionAsync(tx =>
                tx.GetSnapshotAsync(docRef).ContinueWith(snapTask =>
                {
                    DocumentSnapshot snap = snapTask.Result;   // 실패면 여기서 예외 → 트랜잭션 실패로 처리됨
                    if (snap.Exists && snap.TryGetValue("progress", out long serverProgress) && serverProgress > progress)
                        tx.Update(docRef, nameOnly);
                    else
                        tx.Set(docRef, data);
                }))
                .ContinueWithOnMainThread(task =>
                {
                    if (this == null) return;   // 기다리는 사이 파괴됨 (계정 삭제 등)
                    uploading = false;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogWarning($"[Ranking] 업로드 실패 — {retryDelay}초 뒤 재시도: {task.Exception}");
                        dirty       = true;
                        nextTryTime = Time.unscaledTime + retryDelay;
                        return;
                    }

                    GameLog.Info($"[Ranking] 업로드 완료: {nickname} / 이 기기 기록 {world}-{stage} (서버 기록이 더 높으면 닉네임만 갱신)");
                });
        }
        catch (Exception e)
        {
            // 호출하는 순간의 예외(드묾) — 콜백이 오지 않으므로 여기서 상태를 풀어 줌 (AuthManager 의 같은 방어)
            Debug.LogError($"[Ranking] 업로드 호출 실패: {e}");
            uploading   = false;
            dirty       = true;
            nextTryTime = Time.unscaledTime + retryDelay;
        }
    }

    // ───────────────────────────── 조회 ─────────────────────────────

    /// <summary>
    /// 상위 topCount 명을 받아 옵니다. 결과는 메인 스레드에서 onDone(목록, 오류 문구) 로 옵니다.
    /// 성공이면 오류 문구가 null, 실패면 목록이 null.
    ///
    /// ⚠ progress 내림차순 + reachedAt 오름차순은 Firestore '복합 색인' 이 필요합니다.
    ///   색인이 없으면 처음 조회 때 오류 로그에 색인 생성 링크가 찍힙니다 — 눌러서 만들면 됩니다.
    /// </summary>
    public void FetchTop(Action<List<Entry>, string> onDone)
    {
        if (!IsFirebaseReady)
        {
            onDone?.Invoke(null, "서버 연결 준비 중입니다. 잠시 후 다시 시도해 주세요.");
            return;
        }
        if (!AuthManager.Instance.IsSignedIn)
        {
            onDone?.Invoke(null, "로그인하면 랭킹을 볼 수 있습니다.");
            return;
        }

        try
        {
            FirebaseFirestore.DefaultInstance.Collection(COLLECTION)
                .OrderByDescending("progress")
                .OrderBy("reachedAt")
                .Limit(topCount)
                .GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (this == null) return;

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError($"[Ranking] 조회 실패: {task.Exception}");
                        onDone?.Invoke(null, "랭킹을 불러오지 못했습니다.");
                        return;
                    }

                    var list = new List<Entry>();
                    foreach (DocumentSnapshot doc in task.Result.Documents)
                    {
                        doc.TryGetValue("nickname", out string nickname);
                        doc.TryGetValue("progress", out long progress);   // Firestore 정수는 64비트로 옴

                        list.Add(new Entry
                        {
                            Rank     = list.Count + 1,
                            Uid      = doc.Id,
                            Nickname = string.IsNullOrEmpty(nickname) ? PlayerProfile.ANONYMOUS_PREFIX : nickname,
                            Progress = (int)progress,
                        });
                    }
                    onDone?.Invoke(list, null);
                });
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ranking] 조회 호출 실패: {e}");
            onDone?.Invoke(null, "랭킹을 불러오지 못했습니다.");
        }
    }
}

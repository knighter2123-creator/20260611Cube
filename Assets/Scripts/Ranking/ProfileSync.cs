using System;
using System.Collections.Generic;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// 닉네임 서버 동기화 — 같은 계정(이메일/구글)으로 다른 기기에서 로그인해도 같은 닉네임이 나오게 합니다.
///
/// [붙이는 위치]
///   로그인 씬의 ManagerRoot 하위. 다른 매니저들과 같은 DontDestroyOnLoad 싱글턴이다.
///
/// [Firestore 구조]
///   profiles/{Firebase UID}
///     name          : string — 정한 닉네임 ("" = 아직 안 정함)
///     anonymousName : string — "익명1234" (어느 기기에서나 같은 익명 이름)
///     nameUpdatedAt : int    — 이름을 정한 시각 (UTC ms)
///     updatedAt     : timestamp (서버 시각, 관리용)
///   랭킹(rankings)과 나눈 이유: 랭킹은 모두가 읽지만 이 문서는 본인만 읽고 씁니다.
///
/// [흐름]
///   1) 로그인 + 세이브 주인이 그 계정이 되면 서버 문서를 한 번 받아 옴 (Pull)
///      - 서버 이름이 더 최신이면(또는 이 기기에 이름이 없으면) 이 기기에 적용 — 보석 없음
///      - 서버 익명 이름이 있으면 그걸 씀
///   2) 이 기기 쪽이 더 최신이거나 서버에 없으면 올림 (Push)
///   3) 이후 이 기기에서 이름을 등록/변경할 때마다 올림
///
/// [★ 세이브 주인 확인]
///   SaveManager 는 계정마다 다른 세이브 파일을 씁니다(★ [계정별 세이브]).
///   로그인 직후 계정 전환(AccountSwitch)이 끝나기 전에는 "로그인한 계정" 과 "지금 읽고 있는 세이브" 가 다를 수 있어서,
///   그때 받아 온 이름을 넣으면 다른 계정(게스트 등) 세이브에 들어갑니다. 둘이 같아질 때까지 기다립니다.
/// </summary>
public class ProfileSync : MonoBehaviour
{
    public static ProfileSync Instance { get; private set; }

    public const string COLLECTION = "profiles";

    [Tooltip("실패 시 다시 시도하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 30f;

    private string pulledUid;     // 서버 값을 받아 와 합친 계정 (계정이 바뀌면 다시 받음)
    private bool   busy;
    private bool   pushDirty;
    private bool   applying;      // 서버 값을 적용하는 중 — 그로 인한 이름 이벤트는 다시 올리지 않음
    private float  nextTryTime;

    /// <summary>이 계정의 서버 이름을 이미 받아 왔는가. RankingManager 가 "옛 이름으로 랭킹을 덮어쓰지 않게" 기다릴 때 씁니다.</summary>
    public bool IsPulledFor(string uid) => !string.IsNullOrEmpty(uid) && uid == pulledUid;

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
        PlayerProfile.OnNameRegistered += OnLocalNameChanged;
        PlayerProfile.OnNameChanged    += OnLocalNameChanged;
    }

    private void OnDisable()
    {
        PlayerProfile.OnNameRegistered -= OnLocalNameChanged;
        PlayerProfile.OnNameChanged    -= OnLocalNameChanged;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnLocalNameChanged(string _)
    {
        if (!applying) pushDirty = true;
    }

    /// <summary>다음 기회에 내 닉네임을 다시 올리게 합니다. (서버 기록 삭제가 실패했을 때 — ServerAccountData)</summary>
    public void RequestPush() => pushDirty = true;

    private void Update()
    {
        if (busy || Time.unscaledTime < nextTryTime) return;
        if (!TryGetAccount(out string uid)) return;

        if (uid != pulledUid) Pull(uid);
        else if (pushDirty)   Push(uid);
    }

    /// <summary>로그인돼 있고, 지금 세이브의 주인이 그 계정일 때만 uid 를 돌려줍니다 (클래스 주석 ★ 참고).</summary>
    public static bool TryGetAccount(out string uid)
    {
        uid = null;

        AuthManager auth = AuthManager.Instance;
        if (auth == null || !auth.IsReady || !auth.IsSignedIn) return false;

        SaveManager sm = SaveManager.Instance;
        if (sm == null || sm.Current == null || SaveManager.IsSaveLocked) return false;

        // 계정 삭제로 서버 기록을 지우는 중 — 올리면 지운 문서가 되살아남 (ServerAccountData 참고)
        if (ServerAccountData.IsDeleting) return false;

        if (SaveManager.ActiveAccountId != auth.Uid) return false;

        uid = auth.Uid;
        return !string.IsNullOrEmpty(uid);
    }

    private static DocumentReference DocFor(string uid)
        => FirebaseFirestore.DefaultInstance.Collection(COLLECTION).Document(uid);

    // ───────────────────────────── 받아 오기 ─────────────────────────────

    private void Pull(string uid)
    {
        busy = true;
        try
        {
            // ★ Source.Server — 오프라인일 때 '비어 있는 캐시' 를 "서버에 이름 없음" 으로 착각하면,
            //   새 기기의 익명 이름을 올려 진짜 이름을 덮어씁니다. 서버에 못 닿으면 실패로 보고 나중에 다시 시도합니다.
            DocFor(uid).GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
            {
                if (this == null) return;
                busy = false;

                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[ProfileSync] 닉네임 받아 오기 실패 — {retryDelay}초 뒤 재시도: {task.Exception}");
                    nextTryTime = Time.unscaledTime + retryDelay;
                    return;
                }

                // 기다리는 사이 로그아웃·계정 전환이 있었으면 버림 (다음 Update 에서 새 계정으로 다시 받음)
                if (!TryGetAccount(out string nowUid) || nowUid != uid) return;

                Merge(uid, task.Result);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileSync] 받아 오기 호출 실패: {e}");
            busy        = false;
            nextTryTime = Time.unscaledTime + retryDelay;
        }
    }

    private void Merge(string uid, DocumentSnapshot snap)
    {
        string serverName = string.Empty;
        string serverAnon = string.Empty;
        long   serverAt   = 0;

        if (snap.Exists)
        {
            snap.TryGetValue("name",          out serverName);
            snap.TryGetValue("anonymousName", out serverAnon);
            snap.TryGetValue("nameUpdatedAt", out serverAt);
            serverName = serverName ?? string.Empty;
            serverAnon = serverAnon ?? string.Empty;
        }

        applying = true;
        try
        {
            // 이름: 서버에 있고, (이 기기에 이름이 없거나 서버 쪽이 더 최신)이면 서버 이름을 씀
            if (serverName.Length > 0 && serverName != PlayerProfile.Name &&
                (!PlayerProfile.HasName || serverAt > PlayerProfile.NameUpdatedAt))
            {
                PlayerProfile.ApplyFromServer(serverName, serverAt);
            }

            // 익명 이름: 서버에 있으면 항상 서버 것 (처음 만든 기기의 값을 모든 기기가 공유)
            if (serverAnon.Length > 0)
                PlayerProfile.ApplyAnonymousFromServer(serverAnon);
        }
        finally
        {
            applying = false;
        }

        pulledUid = uid;

        // 서버에 없거나 이 기기 쪽이 더 최신이면 올림
        pushDirty = !snap.Exists
                 || serverAnon != PlayerProfile.AnonymousName
                 || (PlayerProfile.HasName && serverName != PlayerProfile.Name);

        GameLog.Info($"[ProfileSync] 닉네임 동기화 완료: {PlayerProfile.DisplayName} (올릴 것 있음={pushDirty})");
    }

    // ───────────────────────────── 올리기 ─────────────────────────────

    private void Push(string uid)
    {
        var data = new Dictionary<string, object>
        {
            { "name",          PlayerProfile.Name },
            { "anonymousName", PlayerProfile.AnonymousName },
            { "nameUpdatedAt", PlayerProfile.NameUpdatedAt },
            { "updatedAt",     FieldValue.ServerTimestamp },
        };

        busy      = true;
        pushDirty = false;   // 올리는 도중 또 바뀌면 다시 true → 끝난 뒤 한 번 더 올림

        try
        {
            DocFor(uid).SetAsync(data).ContinueWithOnMainThread(task =>
            {
                if (this == null) return;
                busy = false;

                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"[ProfileSync] 닉네임 올리기 실패 — {retryDelay}초 뒤 재시도: {task.Exception}");
                    pushDirty   = true;
                    nextTryTime = Time.unscaledTime + retryDelay;
                    return;
                }

                GameLog.Info($"[ProfileSync] 닉네임 올리기 완료: {data["name"]} / {data["anonymousName"]}");
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProfileSync] 올리기 호출 실패: {e}");
            busy        = false;
            pushDirty   = true;
            nextTryTime = Time.unscaledTime + retryDelay;
        }
    }
}

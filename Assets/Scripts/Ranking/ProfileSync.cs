using System;
using UnityEngine;

/// <summary>닉네임 서버 동기화: 로그인 시 서버 이름을 받아(Pull) 서버가 기준으로 적용하고, 서버에 없으면 게스트 때 정한 이름을 올림(Push) — 기기가 달라도 같은 이름.</summary>
// [붙이는 위치] 로그인 씬 ManagerRoot 하위 DontDestroyOnLoad 싱글턴
// 뒤끝 닉네임은 전체 중복 불가 → 로그인 계정은 '서버 등록 성공 후 기기 적용', 올릴 때 중복이면 기기 이름을 지워 무료 최초 설정을 다시 하게 함 (게스트는 기기에만 저장)
// ★ 계정 전환 직후엔 로그인 계정 ≠ 읽고 있는 세이브일 수 있어 둘이 같아질 때까지 기다림 (TryGetAccount)
public class ProfileSync : MonoBehaviour
{
    public static ProfileSync Instance { get; private set; }


    [Tooltip("실패 시 다시 시도하기까지 기다리는 시간(초)")]
    [SerializeField] private float retryDelay = 30f;

    private string pulledUid;              // 서버 값을 받아 와 합친 계정 (계정이 바뀌면 다시 받음)
    private string serverName = string.Empty;   // pulledUid 계정의 서버 닉네임 (마지막으로 알고 있는 값)
    private bool   busy;
    private bool   pushDirty;
    private bool   applying;               // 서버 값을 적용하는 중 — 그로 인한 이름 이벤트는 다시 올리지 않음
    private float  nextTryTime;

    /// <summary>이 계정의 서버 닉네임을 이미 받아 왔는가. 이름을 정하거나 랭킹에 올리기 전에 기다릴 때 씁니다.</summary>
    public bool IsPulledFor(string uid) => !string.IsNullOrEmpty(uid) && uid == pulledUid;

    /// <summary>서버 요청 중인가 (받아 오기·올리기·이름 정하기).</summary>
    public bool IsBusy => busy;

    private static INicknameService Service => ServerServices.Nickname;

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
        // 서버에서 받은 이름을 적용하는 중이거나, 이미 서버와 같은 이름이면 올릴 것 없음
        if (applying) return;
        if (PlayerProfile.HasName && PlayerProfile.Name != serverName) pushDirty = true;
    }

    /// <summary>다음 기회에 내 닉네임을 다시 확인·올리게 합니다. (서버 기록 삭제가 실패했을 때 — ServerAccountData)</summary>
    public void RequestPush()
    {
        pulledUid = null;   // 서버 상태를 모르게 됐으므로 다시 받아 와서 판단
    }

    private void Update()
    {
        if (busy || Time.unscaledTime < nextTryTime) return;
        if (!TryGetAccount(out string uid)) return;
        if (!AuthManager.Instance.IsOnline) return;   // 오프라인 로그인 — 연결되면 이어서

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

    // ───────────────────────────── 받아 오기 ─────────────────────────────

    private void Pull(string uid)
    {
        busy = true;
        Service.FetchMine((name, error) =>
        {
            if (this == null) return;
            busy = false;

            if (error != null)
            {
                Debug.LogWarning($"[ProfileSync] 닉네임 받아 오기 실패 — {retryDelay}초 뒤 재시도: {error}");
                nextTryTime = Time.unscaledTime + retryDelay;
                return;
            }

            // 기다리는 사이 로그아웃·계정 전환이 있었으면 버림 (다음 Update 에서 새 계정으로 다시 받음)
            if (!TryGetAccount(out string nowUid) || nowUid != uid) return;

            Merge(uid, name ?? string.Empty);
        });
    }

    private void Merge(string uid, string server)
    {
        pulledUid  = uid;
        serverName = server;
        AuthManager.Instance?.SetServerNickname(server);

        if (server.Length > 0)
        {
            // 서버가 기준 — 다른 기기에서 정한·바꾼 이름을 이 기기에 적용 (보석 없음)
            if (server != PlayerProfile.Name)
            {
                applying = true;
                try   { PlayerProfile.ApplyFromServer(server, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()); }
                finally { applying = false; }
            }
            // 서버 이름이 이 게임 규칙에 안 맞아 적용하지 못했으면(콘솔에서 고친 경우 등) 이 기기 이름을 다시 올림
            pushDirty = PlayerProfile.HasName && PlayerProfile.Name != server;
        }
        else
        {
            // 서버에 없음 — 게스트 때 정한 이름이 있으면 올림
            pushDirty = PlayerProfile.HasName;
        }

        GameLog.Info($"[ProfileSync] 닉네임 동기화: 서버='{server}' / 이 기기='{PlayerProfile.DisplayName}' (올릴 것 있음={pushDirty})");
    }

    // ───────────────────────────── 올리기 (게스트 때 정한 이름) ─────────────────────────────

    private void Push(string uid)
    {
        string name = PlayerProfile.Name;
        if (string.IsNullOrEmpty(name) || name == serverName) { pushDirty = false; return; }

        busy      = true;
        pushDirty = false;   // 올리는 도중 또 바뀌면 다시 true → 끝난 뒤 한 번 더 올림

        Service.Set(name, serverName.Length > 0, result =>
        {
            if (this == null) return;
            busy = false;

            if (!TryGetAccount(out string nowUid) || nowUid != uid) return;   // 계정이 바뀜 — 새 계정은 다시 받아 옴

            if (result.Ok)
            {
                serverName = name;
                AuthManager.Instance?.SetServerNickname(name);
                GameLog.Info($"[ProfileSync] 닉네임 올리기 완료: {name}");
                return;
            }

            switch (result.Kind)
            {
                case NicknameResultKind.Duplicate:
                case NicknameResultKind.Invalid:
                    // 이 이름은 서버에 둘 수 없음 → 이 기기 이름을 지워 다시 정하게 함 (클래스 주석 [흐름] 2)
                    GameLog.Info($"[ProfileSync] '{name}' 을(를) 서버에 쓸 수 없어 닉네임을 다시 정하게 합니다: {result.Message}");
                    applying = true;
                    try   { PlayerProfile.ClearNameAfterServerConflict(); }
                    finally { applying = false; }
                    break;

                default:
                    Debug.LogWarning($"[ProfileSync] 닉네임 올리기 실패 — {retryDelay}초 뒤 재시도: {result.Message}");
                    pushDirty   = true;
                    nextTryTime = Time.unscaledTime + retryDelay;
                    break;
            }
        });
    }

    // ───────────────────────────── 이름 정하기 (PlayerProfile 전용) ─────────────────────────────

    /// <summary>이름을 정할 수 있는가 (온라인 + 서버 이름을 받아 온 뒤) — 안 되면 유저에게 보여 줄 문구를 error 로.</summary>
    public bool CanSetName(string uid, out string error)
    {
        error = null;
        if (AuthManager.Instance == null || !AuthManager.Instance.IsOnline)
            error = GameMessages.Nickname.needOnline;
        else if (!IsPulledFor(uid) || busy)
            error = GameMessages.Nickname.checking;
        return error == null;
    }

    /// <summary>서버에 이름을 정합니다. 성공하면 이 계정의 서버 이름으로 기억합니다. 이 기기 적용은 부른 쪽(PlayerProfile)이 합니다.</summary>
    public void SetNameOnServer(string uid, string name, Action<NicknameResult> onDone)
    {
        busy = true;
        Service.Set(name, serverName.Length > 0, result =>
        {
            if (this == null) return;
            busy = false;

            if (result.Ok && uid == pulledUid)
            {
                serverName = name;
                AuthManager.Instance?.SetServerNickname(name);
            }
            onDone?.Invoke(result);
        });
    }
}

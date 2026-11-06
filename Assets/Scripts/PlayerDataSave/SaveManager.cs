using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;   // ★ [게임 시작 전 저장 차단] sceneLoaded

/// <summary>세이브 입출력: Awake 에서 계정별 파일(게스트 save.json / 계정 save_UID.json)을 Current 로 읽고, Save() 때 매니저들의 CaptureTo 를 모아 JSON 으로 씀 (매니저는 자기 시점에 ApplyFrom).</summary>
// [붙이는 위치] LoginScene, DontDestroyOnLoad / CloudSave 를 같은 오브젝트에 붙이고 쓸 때마다 savedAt 버전을 올려 알림
// ★ 게임 시작 전 저장 차단: 게임 씬에 들어가기 전 Save() 는 매니저 초기값으로 세이브를 덮으므로 무시 (구글 로그인 창의 OnApplicationPause 등)
// ★ 저장 잠금(IsSaveLocked, static): 계정 삭제·전환 중 메모리의 옛 값이 지운 파일을 되살리지 않게 — 새 SaveManager Awake 에서 해제
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public SaveData Current { get; private set; }

    [Header("게임 시작 전 저장 차단")]
    [Tooltip("매니저들이 세이브를 적용(ApplyFrom)받기 전의 씬 이름들.\n" +
             "이 씬들만 거친 상태에서는 Save() 가 무시됩니다. 씬 이름을 바꾸면 여기도 같이 바꾸세요.")]
    [SerializeField] private string[] preGameScenes = { "LoginScene", "LoadingScene" };

    /// <summary>이 SaveManager 가 게임 씬에 한 번이라도 들어갔는가 — 로그인 화면으로 돌아와도 매니저 값은 진짜라 false 로 안 돌림, 재생성 시 새로 시작하도록 인스턴스 값.</summary>
    public bool IsGameplayStarted { get; private set; }

    // 지금 계정의 파일 경로 — 인스턴스 없이도 지울 파일을 알 수 있게 static (경로 규칙은 PathFor 한 곳)
    private static string SavePath => PathFor(ActiveAccountId);

    // ── 생명주기 ──

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // DontDestroyOnLoad 전에 태어난 씬을 기억 (부른 뒤엔 scene 이 DDOL 특수 씬으로 바뀜)
        Scene bornScene = gameObject.scene;

        DontDestroyOnLoad(gameObject);

        // 잠금 해제 지점 — 새 SaveManager 가 태어났다 = 옛 매니저가 모두 사라짐 (중복 제거 return 아래여야 복제본이 풀지 않음)
        if (IsSaveLocked)
        {
            IsSaveLocked = false;
            GameLog.Info("[SaveManager] 새 SaveManager 생성 — 계정 초기화 완료, 저장 잠금 해제");
        }

        Load();

        // 클라우드 세이브 — SaveManager 와 수명을 같이 합니다 (계정 삭제·전환 때 함께 파괴되고 새로 만들어짐)
        if (GetComponent<CloudSave>() == null) gameObject.AddComponent<CloudSave>();

        // 씬 로드마다 게임 씬인지 확인 (static 이벤트라 OnDestroy 에서 해제)
        ValidatePreGameScenes();
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 태어난 씬도 확인 — ⚠ GetActiveScene 을 쓰면 AccountReset 의 임시 빈 씬을 게임 씬으로 오인해 차단이 풀림
        CheckGameplayScene(bornScene);
    }

    // Instance 정리 — 안 하면 ?. 가 파괴를 못 걸러 죽은 인스턴스의 Save() 가 실행됨
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // 남으면 다음 씬 로드 때 죽은 오브젝트가 불림

        if (Instance == this) Instance = null;
    }

    // ── 미룬 저장 ───────────────────────────────────
    [Tooltip("RequestSave 후 실제로 저장할 때까지 기다리는 시간(초, 실제 시간) — 스테이지 클리어처럼 다른 일이 몰린 프레임을 피함")]
    [SerializeField] private float deferredSaveDelay = 1f;

    private float pendingSaveAt = -1f;   // 미룬 저장 예정 시각 (음수 = 없음)

    /// <summary>잠시 뒤 저장 — 클리어 프레임의 연출·증강·랭킹 처리와 JSON 직렬화·파일 쓰기가 겹쳐 튀지 않게 미루고, 그사이 요청은 한 번으로 합침.</summary>
    // ⚠ 씬을 옮기기 전에는 FlushPendingSave (SceneLoader 가 함) — 안 하면 새 씬 매니저가 ApplyFrom 전일 때 저장될 수 있음
    public void RequestSave()
    {
        if (pendingSaveAt < 0f) pendingSaveAt = Time.unscaledTime + deferredSaveDelay;
    }

    /// <summary>미룬 저장이 있으면 지금 저장.</summary>
    public void FlushPendingSave()
    {
        if (pendingSaveAt >= 0f) Save();
    }

    void Update()
    {
        if (pendingSaveAt >= 0f && Time.unscaledTime >= pendingSaveAt) Save();
    }

    // ── 저장 ───────────────────────────────────────
    public void Save()
    {
        pendingSaveAt = -1f;   // 지금 저장하면 미룬 저장은 필요 없음


        // 잠금 중이면 CaptureTo 전에 막음 (파괴 중인 매니저를 건드리지 않게)
        if (IsSaveLocked)
        {
            GameLog.Info("[SaveManager] 저장 잠금 중 — Save() 무시");
            return;
        }

        // ApplyFrom 전이면 CaptureTo 가 초기값을 모으므로 무시 (이때 바뀌는 이름은 WriteCurrentToDisk 로 이미 저장됨)
        if (!IsGameplayStarted)
        {
            GameLog.Info("[SaveManager] 게임 시작 전(매니저 ApplyFrom 전) — Save() 무시");
            return;
        }

        InstanceCaptureTo();
    }

    void InstanceCaptureTo()
    {
        SaveData data = Current ?? new SaveData();
        
        LevelUpManager.Instance?.CaptureTo(data);
        StageManager.Instance?.CaptureTo(data);
        PlayerBuffManager.Instance?.CaptureTo(data);
        CompanionManager.Instance?.CaptureTo(data);
        CurrencyManager.Instance?.CaptureTo(data);
        CompanionFragment.Instance?.CaptureTo(data);
        MissionManager.Instance?.CaptureTo(data);
        GuideQuestManager.Instance?.CaptureTo(data);
        
        Current = data;
        WriteToDisk(data);
    }

    /// <summary>CaptureTo 없이 Current 를 그대로 씀 — LoginScene 에서 이름처럼 SaveData 에 직접 넣은 값만 확정할 때 (Save 는 준비 안 된 매니저 기본값으로 덮을 수 있어서).</summary>
    public void WriteCurrentToDisk()
    {
        if (Current == null) Load();
        WriteToDisk(Current);
    }

    // 유일한 파일 쓰기 출입구 — contentChanged=false 는 클라우드 표시만 적을 때(버전 유지, 업로드 안 함)
    private void WriteToDisk(SaveData data, bool contentChanged = true)
    {
        // 모든 쓰기 경로가 여기로 모이므로 이 한 줄로 잠금을 보장
        if (IsSaveLocked)
        {
            GameLog.Info("[SaveManager] 저장 잠금 중 — 파일 쓰기 무시");
            return;
        }

        // 버전 갱신 — 같은 밀리초에 두 번 저장해도 값이 달라지게 최소 1 씩 올림 (CloudSave 가 "같은 버전인가" 로 비교)
        if (contentChanged)
            data.savedAt = System.Math.Max(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), data.savedAt + 1);

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            GameLog.Info($"[SaveManager] 저장 완료: {SavePath}");

            // 파일에 실제로 쓴 내용 그대로 클라우드에 올리도록 알림 (올릴지·언제 올릴지는 CloudSave 가 정함)
            if (contentChanged) CloudSave.Instance?.NotifySaved(json, data.savedAt);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 저장 실패: {e.Message}");
        }
    }

    /// <summary>클라우드와 맞춘 버전(cloudSyncedAt)만 파일에 바로 기록 (CloudSave 전용) — ★ 게임 중엔 Current 가 파일보다 앞서 있어 통째로 쓰면 '버전은 같고 내용은 다른' 파일이 되므로 그 값 하나만 고침.</summary>
    public void MarkCloudSynced(long cloudSavedAt)
    {
        if (Current == null || Current.cloudSyncedAt == cloudSavedAt) return;
        Current.cloudSyncedAt = cloudSavedAt;
        if (IsSaveLocked || !HasSave()) return;

        if (!IsGameplayStarted)
        {
            WriteToDisk(Current, contentChanged: false);
            return;
        }

        try
        {
            string json    = File.ReadAllText(SavePath);
            string patched = System.Text.RegularExpressions.Regex.Replace(
                json, "\"cloudSyncedAt\"\\s*:\\s*-?\\d+", $"\"cloudSyncedAt\": {cloudSavedAt}");
            if (patched != json) File.WriteAllText(SavePath, patched);
            // 키가 없으면(이 버전에서 한 번도 안 쓴 파일) 그대로 둠 — 게임 중 올리기는 이 버전이 파일을 쓴 뒤에만 일어나므로 보통 있음
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SaveManager] 클라우드 동기화 표시 기록 실패 (다음 저장 때 함께 기록됨): {e.Message}");
        }
    }

    /// <summary>클라우드 세이브를 그 계정 파일로 씀 (CloudSave 전용) — ⚠ 잠금을 무시: 호출 쪽이 잠근 뒤 쓰고 RestartFromLogin 으로 새 SaveManager 가 읽게 함.</summary>
    public static bool TryWriteDownloadedSave(string accountId, string json, out string error)
    {
        error = string.Empty;
        try
        {
            File.WriteAllText(PathFor(accountId), json);
            GameLog.Info($"[SaveManager] 클라우드 세이브를 파일로 기록: {PathFor(accountId)}");
            return true;
        }
        catch (System.Exception e)
        {
            error = e.Message;   // 외부 저장소라 try-catch (TryDeleteSaveFile 과 같은 기준)
            Debug.LogError($"[SaveManager] 클라우드 세이브 기록 실패: {e.Message}");
            return false;
        }
    }

    // ── 불러오기 ───────────────────────────────────

    public bool HasSave() => File.Exists(SavePath);

    public void Load()
    {
        if (!HasSave())
        {
            Current = new SaveData();   // 첫 실행 — 기본값
            GameLog.Info($"[SaveManager] 세이브 없음 — 새 데이터로 시작 (계정: {ActiveAccountLabel})");   // ★ [계정별 세이브]
            return;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            Current = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();

            // 구버전 세이브 호환 — 리스트 필드가 null로 역직렬화될 경우 방어
            if (Current.claimedEvolveRewards == null)
                Current.claimedEvolveRewards = new List<string>();
            if (Current.ownedCompanionIds == null)
                Current.ownedCompanionIds = new List<string>();
            if (Current.companionFragments == null)
                Current.companionFragments = new List<FragmentEntry>();
            if (Current.companionStars == null)
                Current.companionStars = new List<StarEntry>();
            if (Current.dailyDungeon == null)
                Current.dailyDungeon = new DailyDungeonSaveData();
            if (Current.dailyDungeon.records == null)
                Current.dailyDungeon.records = new List<DailyDungeonRecord>();

            // 튜토리얼 이관: 파일에 tutorialDone 키가 없으면 기능 이전 유저라 본 것으로 처리 (bool 은 null 검사가 안 돼 키 존재로 판단)
            if (!json.Contains("\"tutorialDone\""))
                Current.tutorialDone = true;

            GameLog.Info($"[SaveManager] 불러오기 완료 (계정: {ActiveAccountLabel})");   // ★ [계정별 세이브] 어느 계정 파일인지
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 불러오기 실패 — 기본값 사용: {e.Message}");
            Current = new SaveData();
        }
    }

    // ── 진화 보상 1회 지급 플래그 ──────────────────

    public bool IsEvolveRewardClaimed(string id)
        => Current != null && Current.claimedEvolveRewards.Contains(id);

    public void MarkEvolveRewardClaimed(string id)
    {
        if (Current == null) Load();
        if (!Current.claimedEvolveRewards.Contains(id))
        {
            Current.claimedEvolveRewards.Add(id);
            Save();
        }
    }

    /// <summary>에디터 테스트용(우클릭) 파일·Current 삭제 — 매니저는 그대로라 플레이 중 누르면 다음 Save 때 옛 값으로 다시 생김 (게임의 계정 삭제는 AccountReset).</summary>
    [ContextMenu("세이브 삭제")]
    public void DeleteSave()
    {
        // 설정의 '계정 삭제'(AccountReset.TryWipe)와 같은 세이브 삭제 경로 — 랭킹 초기화 표시도 여기서 함께 남습니다.
        if (TryDeleteSaveFile(out string error))
            Debug.Log($"[SaveManager] 세이브 삭제됨: {SavePath}");
        else
            Debug.LogError($"[SaveManager] 세이브 삭제 실패: {error}");
    }

    // ── 세이브 삭제 후 랭킹 초기화 표시 ─────────────────
    // 파일과 함께 지워지면 안 돼 PlayerPrefs 에 계정별로 두고, '파일이 없을 뿐'(새 기기)과 구분하려고 TryDeleteSaveFile 때만 켬
    private const string RANKING_RESET_PREF_KEY = "RANKING_RESET_AFTER_SAVE_DELETE_V1";

    // RankingManager.Update 가 매 프레임 물어서 메모리 캐시 (PlayerPrefs 레지스트리 읽기·문자열 할당 방지) — ⚠ 쓰기는 꼭 SetRankingResetPending 으로
    private static readonly Dictionary<string, bool> rankingResetCache = new Dictionary<string, bool>();

    /// <summary>이 계정이 세이브 삭제 후 서버 랭킹 초기화를 기다리는 중인가.</summary>
    public static bool IsRankingResetPending(string accountId)
    {
        string cacheKey = accountId ?? string.Empty;
        if (rankingResetCache.TryGetValue(cacheKey, out bool pending)) return pending;

        pending = PlayerPrefs.GetInt(RANKING_RESET_PREF_KEY + KeySuffixFor(accountId), 0) == 1;
        rankingResetCache[cacheKey] = pending;
        return pending;
    }

    public static void SetRankingResetPending(string accountId, bool on)
    {
        string key = RANKING_RESET_PREF_KEY + KeySuffixFor(accountId);
        if (on) PlayerPrefs.SetInt(key, 1);
        else    PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();   // 바로 앱이 꺼져도 표시가 남거나 지워지도록 즉시 기록
        rankingResetCache[accountId ?? string.Empty] = on;
    }

    // ── 자동 저장 ──────────────────────────────────

    void OnApplicationPause(bool paused)
    {
        // 백그라운드 전환(구글 로그인 창 포함) 시 저장 — 잠금·게임 시작 전이면 Save 가 무시
        if (paused) Save();
    }

    void OnApplicationQuit()
    {
        Save();
    }

    // ── ★ [게임 시작 전 저장 차단] ──

    // sceneLoaded 로 불리는 함수. 씬이 로드될 때마다 "게임 씬인가" 를 확인한다.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckGameplayScene(scene);
    }

    private void CheckGameplayScene(Scene scene)
    {
        if (IsGameplayStarted) return;              // 이미 true — 다시 볼 필요 없음
        if (IsPreGameScene(scene.name)) return;     // 로그인/로딩 — 아직 게임 시작 전

        IsGameplayStarted = true;
        GameLog.Info($"[SaveManager] 게임 씬 진입('{scene.name}') — 이제부터 Save() 허용");
    }

    /// <summary>preGameScenes 오타 경고 — 이름이 틀리면 그 씬이 게임 씬으로 취급돼 차단이 조용히 풀림.</summary>
    private void ValidatePreGameScenes()
    {
        if (preGameScenes == null || preGameScenes.Length == 0)
        {
            Debug.LogWarning("[SaveManager] preGameScenes 가 비어 있습니다 — 게임 시작 전 저장 차단이 동작하지 않습니다.");
            return;
        }
        foreach (string s in preGameScenes)
        {
            // Build Settings 에 등록된 씬 이름인지 확인 (등록 안 된 이름 = 오타일 가능성이 큼)
            if (!Application.CanStreamedLevelBeLoaded(s))
                Debug.LogWarning($"[SaveManager] preGameScenes 의 '{s}' 가 Build Settings 에 없습니다. 씬 이름을 확인하세요.");
        }
    }

    private bool IsPreGameScene(string sceneName)
    {
        if (preGameScenes == null) return false;
        foreach (string s in preGameScenes)
        {
            if (s == sceneName) return true;
        }
        return false;
    }

    // ── ★ [계정별 세이브] 계정 → 파일 ──

    // 마지막 계정 기억 — Awake 때는 로그인 확인 전이라, 기억해 두면 같은 계정으로 다시 켤 때 재시작 없이 맞는 파일을 읽음
    private const string ACTIVE_ACCOUNT_PREF_KEY = "SAVE_ACTIVE_ACCOUNT_V1";
    private const string GUEST_FILE_NAME = "save.json";   // 예전 파일 이름 그대로 — 기존 유저 데이터를 옮기지 않아도 됨

    private static string activeAccountId;
    private static bool   activeAccountLoaded;   // PlayerPrefs 에서 한 번 읽었는가 (매번 읽지 않게)

    /// <summary>지금 세이브 주인(""=게스트, 그 외 뒤끝 inDate) — 재생성된 SaveManager 가 Awake 에서 읽을 파일을 알도록 static.</summary>
    public static string ActiveAccountId
    {
        get
        {
            if (!activeAccountLoaded)
            {
                activeAccountId     = PlayerPrefs.GetString(ACTIVE_ACCOUNT_PREF_KEY, string.Empty);
                activeAccountLoaded = true;
            }
            return activeAccountId;
        }
    }

    /// <summary>로그 표시용 이름 ("게스트" 또는 UID 앞 6자리)</summary>
    public static string ActiveAccountLabel =>
        IsGuestAccount(ActiveAccountId) ? "게스트" : ActiveAccountId.Substring(0, System.Math.Min(6, ActiveAccountId.Length)) + "…";

    public static bool IsGuestAccount(string accountId) => string.IsNullOrEmpty(accountId);

    /// <summary>계정별 PlayerPrefs 키 꼬리표(게스트 "" = 예전 키, 계정 "_UID") — 세이브 파일 이름과 같은 규칙을 증강 저장에도 쓰려고 공개.</summary>
    public static string KeySuffixFor(string accountId) =>
        IsGuestAccount(accountId) ? string.Empty : "_" + SanitizeForFileName(accountId);

    /// <summary>계정의 세이브 파일 경로. 경로 규칙은 이 함수 한 곳에만 있습니다.</summary>
    public static string PathFor(string accountId)
    {
        string fileName = IsGuestAccount(accountId) ? GUEST_FILE_NAME : $"save{KeySuffixFor(accountId)}.json";
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    public static bool SaveFileExists(string accountId) => File.Exists(PathFor(accountId));

    /// <summary>사용 계정을 바꾸고 기억 — ⚠ 파일을 다시 읽지 않음 (메모리를 맞추는 건 AccountSwitch 몫).</summary>
    public static void SetActiveAccount(string accountId)
    {
        activeAccountId     = accountId ?? string.Empty;
        activeAccountLoaded = true;
        PlayerPrefs.SetString(ACTIVE_ACCOUNT_PREF_KEY, activeAccountId);
        PlayerPrefs.Save();   // 바로 앱이 꺼져도 다음 실행에 맞는 파일을 읽도록 즉시 기록
        GameLog.Info($"[SaveManager] 사용 계정 변경 → {ActiveAccountLabel} ({PathFor(activeAccountId)})");
    }

    /// <summary>게스트 진행을 계정 파일로 '이동'(File.Move 라 반쯤 복사된 파일이 안 생김) — 대상이 이미 있으면 덮지 않고 실패, 원본이 없으면 성공.</summary>
    public static bool TryMoveSaveFile(string fromAccountId, string toAccountId, out string error)
    {
        error = string.Empty;
        string from = PathFor(fromAccountId);
        string to   = PathFor(toAccountId);
        if (from == to) return true;

        try
        {
            if (File.Exists(to))
            {
                error = GameMessages.Account.targetSaveExists;
                return false;
            }
            if (File.Exists(from)) File.Move(from, to);
            return true;
        }
        catch (System.Exception e)
        {
            error = e.Message;   // 외부 저장소라 try-catch (TryDeleteSaveFile 과 같은 기준)
            return false;
        }
    }

    /// <summary>UID 의 ':' '.' 등을 '_' 로 바꿔 파일 이름에 안전하게 (못 쓰는 글자면 저장이 조용히 실패해서).</summary>
    private static string SanitizeForFileName(string id)
    {
        var sb = new System.Text.StringBuilder(id.Length);
        foreach (char c in id)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        return sb.ToString();
    }

    // ── ★ [계정 삭제] 저장 잠금 ──

    /// <summary>true 동안 모든 파일 쓰기 차단 — 지운 뒤에도 매니저의 옛 값이 Save 로 파일을 되살리므로, SaveManager 재생성 중에도 끊기지 않게 static.</summary>
    public static bool IsSaveLocked { get; private set; }

    /// <summary>Reload Domain 을 끈 에디터에서 이전 플레이의 static(잠금 등)이 남지 않게 플레이 시작 직전에 비움.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsSaveLocked = false;

        // ★ [계정별 세이브] 메모리 값은 비우고 다음에 PlayerPrefs 에서 다시 읽게 함 (실제 앱 재시작과 같은 상태)
        activeAccountId     = null;
        activeAccountLoaded = false;
        rankingResetCache.Clear();
    }

    /// <summary>저장을 잠급니다. 매니저를 새로 만드는 쪽(AccountReset — 계정 삭제·계정 전환)만 호출하세요.</summary>
    public static void LockForReset()
    {
        IsSaveLocked = true;
        GameLog.Info("[SaveManager] 저장 잠금 — 매니저 재시작(계정 삭제/전환) 시작");
    }

    /// <summary>잠금 해제 — 보통은 새 SaveManager.Awake 가 풀고, AccountReset 은 실패·안전망 때만 호출.</summary>
    public static void UnlockAfterReset()
    {
        if (!IsSaveLocked) return;
        IsSaveLocked = false;
        GameLog.Info("[SaveManager] 저장 잠금 해제");
    }

    /// <summary>세이브 파일 삭제 + Current 비움 + 클라우드 대기분 폐기 + 랭킹 초기화 표시, 실패하면 false + 사유.</summary>
    public static bool TryDeleteSaveFile(out string error)
    {
        error = string.Empty;

        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
        catch (System.Exception e)
        {
            // 외부 저장소는 통제할 수 없어 try-catch
            error = e.Message;
            Debug.LogError($"[SaveManager] 세이브 파일 삭제 실패: {e.Message}");
            return false;
        }

        // Current 도 비움 — Current 를 직접 읽는 코드(PlayerProfile.Name 등)에 옛 값이 남지 않게
        if (Instance != null)
            Instance.Current = new SaveData();
        CloudSave.Instance?.DiscardPending();   // 지우기 전 내용이 클라우드로 올라가지 않게

        // 1-1 부터 다시 하면 서버 랭킹도 0 으로 (RankingManager — 계정 삭제가 늦게 반영된 경우까지 정리하는 안전망)
        SetRankingResetPending(ActiveAccountId, true);

        GameLog.Info($"[SaveManager] 세이브 파일 삭제 완료: {SavePath}");
        return true;
    }
}

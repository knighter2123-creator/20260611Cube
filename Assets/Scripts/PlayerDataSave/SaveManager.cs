using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일 입출력 + 저장 오케스트레이션.
/// - 파일: Application.persistentDataPath/save.json (암호화 없음, JsonUtility)
/// - 저장: 살아있는 매니저들의 CaptureTo()를 모아 파일에 기록 (병합 방식)
/// - 불러오기: Awake에서 파일을 Current로 로드 → 각 매니저가 자기 시점에 ApplyFrom()으로 가져감
/// LoginScene에 두고 DontDestroyOnLoad로 세션 내내 유지하세요.
///
/// ★ [계정 삭제] 이번에 바뀐 곳 (전부 "★ [계정 삭제]" 로 표시)
///   1. 저장 잠금(IsSaveLocked) — 초기화 도중 옛 데이터가 파일로 되살아나는 것을 막는다
///   2. TryDeleteSaveFile() — 인스턴스가 없어도(에디터에서 MainScene 바로 실행) 파일을 지울 수 있게 static
///   3. SavePath 를 static 으로 — 2번이 쓰기 위해. 경로 문자열은 여전히 이 한 곳에만 있다
///   4. Awake 에서 잠금 해제 — "새 SaveManager 가 태어났다 = 초기화가 끝났다"
///   5. OnDestroy 에서 Instance 정리 — 다른 매니저들과 같은 패턴
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public SaveData Current { get; private set; }

    // ★ [계정 삭제] static 으로 변경.
    //   인스턴스 데이터를 하나도 안 쓰는 계산이라 static 이어도 의미가 같습니다.
    //   이렇게 해야 SaveManager 가 없는 상황에서도 "어느 파일을 지울지" 를 알 수 있습니다.
    //   ("save.json" 이라는 이름을 AccountReset 에 한 번 더 적으면, 나중에 한쪽만 바뀌는 순간 초기화가 조용히 고장납니다)
    private static string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    // ─────────────────────────────────────────────
    // ★ [계정 삭제] 저장 잠금
    // ─────────────────────────────────────────────

    /// <summary>
    /// true 인 동안에는 Save() / WriteCurrentToDisk() 가 파일에 아무것도 쓰지 않습니다.
    ///
    /// [왜 필요한가 — 이 기능의 핵심]
    ///   세이브 파일을 지워도, 매니저들은 옛 값(레벨·골드·동료)을 메모리에 들고 살아 있습니다.
    ///   그 상태에서 누군가 Save() 를 한 번이라도 부르면(앱 백그라운드 전환, 씬 정리 중의 OnDestroy 등)
    ///   CaptureTo() 가 옛 값을 모아 "방금 지운 파일을 그대로 다시 만듭니다".
    ///   에러도 안 나고, 유저 눈에는 "계정 삭제를 눌렀는데 아무것도 안 지워졌다" 로 보입니다.
    ///
    /// [왜 static 인가]
    ///   초기화 도중에 SaveManager 자신도 파괴되고 새로 만들어집니다.
    ///   인스턴스 필드에 두면 "옛 SaveManager 는 잠겼는데 새 SaveManager 는 안 잠긴" 틈이 생깁니다.
    ///   static 은 인스턴스가 바뀌어도 그대로 남으므로 잠금이 끊기지 않습니다.
    /// </summary>
    public static bool IsSaveLocked { get; private set; }

    /// <summary>
    /// Enter Play Mode Options 에서 "Reload Domain" 을 끄면 static 값이 이전 플레이에서 넘어옵니다.
    /// 초기화 도중에 플레이를 멈췄다면 잠긴 채로 다음 플레이가 시작되어 "저장이 영영 안 되는" 상태가 됩니다.
    /// 플레이 시작 직전에 비웁니다. (PlayerProfile.ResetStatics 와 같은 이유)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        IsSaveLocked = false;
    }

    /// <summary>저장을 잠급니다. 계정 초기화(AccountReset)만 호출하세요.</summary>
    public static void LockForReset()
    {
        IsSaveLocked = true;
        Debug.Log("[SaveManager] 저장 잠금 — 계정 초기화 시작");
    }

    /// <summary>
    /// 잠금을 풉니다. 정상 흐름에서는 새 SaveManager 의 Awake 가 풀어 주므로
    /// AccountReset 은 "초기화가 도중에 실패했을 때" 와 "안전망" 용도로만 부릅니다.
    /// </summary>
    public static void UnlockAfterReset()
    {
        if (!IsSaveLocked) return;
        IsSaveLocked = false;
        Debug.Log("[SaveManager] 저장 잠금 해제");
    }

    /// <summary>
    /// 세이브 파일을 지우고, 살아 있는 SaveManager 의 Current 도 빈 데이터로 바꿉니다.
    /// 실패하면 false + 사유. (파일을 못 지웠는데 성공이라고 넘어가면 다음 실행에 옛 데이터가 그대로 뜹니다)
    /// </summary>
    public static bool TryDeleteSaveFile(out string error)
    {
        error = string.Empty;

        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
        catch (System.Exception e)
        {
            // 외부 저장소는 우리가 통제할 수 없는 입력이라 try-catch 를 둡니다.
            // (AugmentManager.Load 의 "학습 포인트" 주석과 같은 기준)
            error = e.Message;
            Debug.LogError($"[SaveManager] 세이브 파일 삭제 실패: {e.Message}");
            return false;
        }

        // ★ 메모리 쪽도 비웁니다.
        //   PlayerProfile.Name 처럼 Current 를 직접 읽는 코드가 있어서,
        //   파일만 지우고 Current 를 두면 씬이 바뀌기 전까지 옛 이름이 계속 보입니다.
        //   (Instance 는 Unity 식 null 검사 — 파괴된 인스턴스도 걸러냅니다)
        if (Instance != null)
            Instance.Current = new SaveData();

        Debug.Log($"[SaveManager] 세이브 파일 삭제 완료: {SavePath}");
        return true;
    }

    // ─────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ★ [계정 삭제] 잠금 해제 지점.
        //   AccountReset 은 옛 매니저를 "전부 파괴한 뒤에" LoginScene 을 다시 불러옵니다.
        //   그러므로 새 SaveManager 가 여기까지 왔다는 건, 옛 데이터를 저장할 수 있는 주체가
        //   이미 하나도 남아 있지 않다는 뜻입니다. 이제 잠글 이유가 없습니다.
        //   (위의 중복 제거 return 보다 "아래" 에 있어야 합니다 — 곧 죽을 복제본이 풀면 안 되니까요)
        if (IsSaveLocked)
        {
            IsSaveLocked = false;
            Debug.Log("[SaveManager] 새 SaveManager 생성 — 계정 초기화 완료, 저장 잠금 해제");
        }

        Load();
    }

    // ★ [계정 삭제] 다른 매니저(ManagerRoot, HapticManager ...)와 같은 패턴.
    //   없으면 파괴된 인스턴스를 static 이 계속 가리킵니다.
    //   'SaveManager.Instance?.Save()' 의 ?. 는 파괴 여부를 검사하지 못하므로,
    //   정리해 두지 않으면 죽은 인스턴스의 Save() 가 그대로 실행됩니다.
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 저장 ───────────────────────────────────────
    public void Save()
    {
        // ★ [계정 삭제] CaptureTo() 를 돌기 "전에" 막습니다.
        //   아래 WriteToDisk 에도 같은 검사가 있지만, 여기서 먼저 막으면
        //   초기화 도중 파괴되는 매니저들의 CaptureTo 를 건드리지 않아도 됩니다.
        if (IsSaveLocked)
        {
            Debug.Log("[SaveManager] 저장 잠금 중 — Save() 무시");
            return;
        }

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

    /// <summary>
    /// 매니저들의 CaptureTo() 를 거치지 않고, 지금 Current 를 그대로 파일에 씁니다.
    /// 매니저들이 아직 세이브를 적용(ApplyFrom)하기 전인 LoginScene 에서
    /// 이름처럼 "SaveData 에 직접 넣은 값" 만 확정할 때 사용합니다. (PlayerProfile 참고)
    /// Save() 를 부르면 준비 안 된 매니저의 기본값이 파일을 덮어쓸 수 있기 때문입니다.
    /// </summary>
    public void WriteCurrentToDisk()
    {
        if (Current == null) Load();
        WriteToDisk(Current);
    }

    // 실제 파일 쓰기는 이 함수 한 곳에만 있음 (Save / WriteCurrentToDisk 공용)
    private void WriteToDisk(SaveData data)
    {
        // ★ [계정 삭제] 파일 쓰기의 "유일한 출입구" 에서 한 번 더 막습니다.
        //   Save() 를 거치지 않는 WriteCurrentToDisk() 경로(PlayerProfile.Persist)도 여기로 모이므로,
        //   이 한 줄이 모든 파일 쓰기를 덮습니다. 쓰기를 한 곳에 모아 둔 덕분입니다.
        if (IsSaveLocked)
        {
            Debug.Log("[SaveManager] 저장 잠금 중 — 파일 쓰기 무시");
            return;
        }

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            Debug.Log($"[SaveManager] 저장 완료: {SavePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SaveManager] 저장 실패: {e.Message}");
        }
    }

    // ── 불러오기 ───────────────────────────────────

    public bool HasSave() => File.Exists(SavePath);

    public void Load()
    {
        if (!HasSave())
        {
            Current = new SaveData();   // 첫 실행 — 기본값
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

            // ★ 튜토리얼 마이그레이션 — 세이브 파일은 있는데 tutorialDone 키가 없다
            //   = 튜토리얼 기능이 생기기 전부터 플레이하던 기존 유저 → 이미 본 것으로 처리.
            //   bool 은 null 이 될 수 없어서 위의 리스트처럼 "== null" 로는 구분이 안 됩니다.
            //   그래서 "JSON 문자열에 키가 있었는가"로 판단합니다.
            //   다음 저장부터는 키가 파일에 들어가므로 이 분기는 유저당 한 번만 탑니다.
            //   (계정 삭제 후에는 파일 자체가 없으므로 이 분기를 타지 않고 → 튜토리얼이 다시 뜹니다)
            if (!json.Contains("\"tutorialDone\""))
                Current.tutorialDone = true;

            Debug.Log("[SaveManager] 불러오기 완료");
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

    /// <summary>
    /// 에디터 테스트용 (컴포넌트 우클릭). 파일과 Current 만 지우고, 살아 있는 매니저는 그대로입니다.
    /// → 플레이 중에 누르면 다음 Save() 때 매니저의 옛 값으로 파일이 다시 생깁니다.
    ///   게임 안의 '계정 삭제' 는 이 함수가 아니라 AccountReset 을 씁니다.
    /// </summary>
    [ContextMenu("세이브 삭제")]
    public void DeleteSave()
    {
        if (File.Exists(SavePath)) File.Delete(SavePath);
        Current = new SaveData();
        Debug.Log($"[SaveManager] 세이브 삭제됨: {SavePath}");
    }

    // ── 자동 저장 ──────────────────────────────────

    void OnApplicationPause(bool paused)
    {
        if (paused) Save();   // 모바일: 백그라운드 전환 시 (잠금 중이면 Save() 가 알아서 무시)
    }

    void OnApplicationQuit()
    {
        Save();
    }
}
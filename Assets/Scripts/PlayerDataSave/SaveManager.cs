using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 세이브 파일 입출력 + 저장 오케스트레이션.
/// - 파일: Application.persistentDataPath/save.json (암호화 없음, JsonUtility)
/// - 저장: 살아있는 매니저들의 CaptureTo()를 모아 파일에 기록 (병합 방식)
/// - 불러오기: Awake에서 파일을 Current로 로드 → 각 매니저가 자기 시점에 ApplyFrom()으로 가져감
/// LoginScene에 두고 DontDestroyOnLoad로 세션 내내 유지하세요.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }
    
    public SaveData Current { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "save.json");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    // ── 저장 ───────────────────────────────────────
    public void Save()
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
        if (paused) Save();   // 모바일: 백그라운드 전환 시
    }

    void OnApplicationQuit()
    {
        Save();
    }
}
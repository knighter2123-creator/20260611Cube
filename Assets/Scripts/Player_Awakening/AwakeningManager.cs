using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>각성 단계별 영구 강화 관리 — 단계를 따로 저장하지 않고 세이브의 claimedEvolveRewards 에서 매번 계산(마이그레이션 불필요·두 값 어긋남 없음·클리어 순서 무관, 티어 몇 개라 비용 ≈ 0, 캐시 후 Refresh).</summary>
// [붙이는 위치] LoginScene 의 Managers 루트 자식
public class AwakeningManager : MonoBehaviour
{
    public static AwakeningManager Instance { get; private set; }

    [Header("보상 정의")]
    [Tooltip("AwakeningRewardTable 에셋을 연결하세요. 없으면 각성 효과가 전부 0입니다.")]
    [SerializeField] private AwakeningRewardTable table;

    [Header("디버그")]
    [Tooltip("각성 단계가 갱신될 때마다 상세 로그를 남깁니다.")]
    [SerializeField] private bool verboseLog = true;

    // ── 런타임 상태 (전부 Refresh()가 다시 계산) ──

    /// <summary>보상 테이블 — 입장 패널이 여기서 빌려 씀 (연결 지점을 하나로 둬 교체 시 어긋남 방지).</summary>
    public AwakeningRewardTable Table => table;

    /// <summary>달성한 각성 단계 수 (0 ~ TierCount).</summary>
    public int Level { get; private set; }

    /// <summary>각성으로 늘어난 '추가' 발사체 수. 기본 1발에 더해집니다.</summary>
    public int ExtraProjectiles { get; private set; }

    /// <summary>각성으로 늘어난 '추가' 연사 횟수. 기본 1회에 더해집니다.</summary>
    public int ExtraBurstShots { get; private set; }

    /// <summary>현재 단계에서 입어야 할 플레이어 스프라이트. null이면 기본 모습.</summary>
    public Sprite CurrentSprite { get; private set; }

    /// <summary>한 번의 공격에서 동시에 나가는 발사체 수 (최소 1).</summary>
    public int ProjectilesPerVolley => 1 + ExtraProjectiles;

    /// <summary>한 번의 공격에서 연달아 쏘는 횟수 (최소 1).</summary>
    public int VolleysPerAttack => 1 + ExtraBurstShots;

    // 연결 누락 경고를 한 번씩만 남기기 위한 플래그
    private bool warnedNoTable;
    private bool warnedNoSave;

    // ── 각성 축하 연출 예약 — 클리어 직후 씬이 바로 바뀌어 거기서 재생하면 안 보이므로, 깃발만 세우고 메인 스테이지의 새 플레이어가 꺼내 재생 ──
    private bool pendingCelebration;

    // 첫 Refresh()인지 여부. 세이브 복원과 실제 달성을 구분하는 데 씁니다.
    private bool initialized;

    /// <summary>미재생 축하 연출이 있으면 true 를 돌려주고 예약 삭제 (한 번만 재생).</summary>
    public bool ConsumeCelebration()
    {
        bool value = pendingCelebration;
        pendingCelebration = false;
        return value;
    }

    /// <summary>각성 상태 변경 이벤트 — 매니저는 숫자만 들고 화면은 구독자가 처리 (씬 오브젝트 참조는 씬 전환에 죽음).</summary>
    public event Action OnChanged;

    // ── 라이프사이클 ──

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 씬 로드마다 재계산 — 보상 기록·클리어 보고 순서가 어긋나도 도착 시점엔 맞는 값으로 자가 복구
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Start 에서 — SaveManager 가 Awake 에서 Load 하므로 같은 Awake 단계면 Current 가 null 일 수 있음
        Refresh();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    // ── 외부에서 부르는 지점 ──

    /// <summary>각성 클리어 시 호출 — 값을 올리지 않고 세이브에서 다시 읽어 중복 호출에도 안전.</summary>
    public void ReportEvolveCleared(string stageId)
    {
        if (verboseLog)
            GameLog.Info($"[각성] 클리어 보고 수신 — {stageId ?? "(id 없음)"}");

        Refresh();
    }

    /// <summary>세이브를 기준으로 각성 효과 전체를 다시 계산합니다. 몇 번을 불러도 결과가 같습니다.</summary>
    public void Refresh()
    {
        int    level       = 0;
        int    projectiles = 0;
        int    bursts      = 0;
        Sprite sprite      = null;

        SaveManager save = SaveManager.Instance;

        if (CanRead(save))
        {
            for (int i = 0; i < table.TierCount; i++)
            {
                if (!IsTierCleared(i, save)) continue;   // 연결 안 된 칸이거나 아직 못 깬 단계

                level++;
                AwakeningRewardTable.Tier tier = table.GetTier(i);
                projectiles += tier.addProjectiles;
                bursts      += tier.addBurstShots;

                // 스프라이트는 마지막 지정값이 이김 (null 이면 이전 모습 유지)
                if (tier.playerSprite != null) sprite = tier.playerSprite;
            }
        }

        // 값이 그대로면 이벤트 생략 (씬 로드마다 Refresh 가 돌아서)
        bool changed = level       != Level
                    || projectiles != ExtraProjectiles
                    || bursts      != ExtraBurstShots
                    || sprite      != CurrentSprite;

        // 축하 예약은 단계가 '올라갔고' '첫 계산이 아닐' 때만 (첫 계산의 0→3 은 로드라 축하하면 접속마다 연출이 터짐)
        if (initialized && level > Level) pendingCelebration = true;
        initialized = true;

        Level            = level;
        ExtraProjectiles = projectiles;
        ExtraBurstShots  = bursts;
        CurrentSprite    = sprite;

        if (!changed) return;

        if (verboseLog)
            GameLog.Info($"[각성] 갱신 — {Level}단계 / 발사체 {ProjectilesPerVolley}발 " +
                      $"× 연사 {VolleysPerAttack}회 = 공격당 {ProjectilesPerVolley * VolleysPerAttack}발");

        OnChanged?.Invoke();
    }

    /// <summary>테이블·세이브가 다 있어야 계산 가능 — 없으면 경고는 한 번만 (씬 로드마다 돌아 도배 방지).</summary>
    private bool CanRead(SaveManager save)
    {
        if (table == null)
        {
            if (!warnedNoTable)
            {
                warnedNoTable = true;
                Debug.LogWarning("[각성] 보상 테이블이 연결되지 않았습니다. 각성 효과가 적용되지 않습니다.", this);
            }
            return false;
        }

        if (save == null)
        {
            if (!warnedNoSave)
            {
                warnedNoSave = true;
                Debug.LogWarning("[각성] SaveManager가 없어 각성 단계를 읽을 수 없습니다.", this);
            }
            return false;
        }

        return true;
    }

    /// <summary>i번 티어를 깼는가. 스테이지가 연결되지 않은 칸은 영원히 false 입니다.</summary>
    private bool IsTierCleared(int index, SaveManager save)
    {
        string id = table.GetStageId(index);
        return !string.IsNullOrEmpty(id) && save.IsEvolveRewardClaimed(id);
    }

    // ── 에디터 테스트 ──

    [ContextMenu("테스트: 지금 상태 다시 계산")]
    private void TestRefresh() => Refresh();

    [ContextMenu("테스트: 현재 각성 상태 출력")]
    private void TestDump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[각성] 현재 {Level}단계");
        sb.AppendLine($"  동시 발사체 : {ProjectilesPerVolley}발 (추가 +{ExtraProjectiles})");
        sb.AppendLine($"  연사 횟수   : {VolleysPerAttack}회 (추가 +{ExtraBurstShots})");
        sb.AppendLine($"  공격당 총알 : {ProjectilesPerVolley * VolleysPerAttack}발");
        sb.AppendLine($"  스프라이트  : {(CurrentSprite != null ? CurrentSprite.name : "기본")}");

        if (table != null && SaveManager.Instance != null)
        {
            sb.AppendLine("  ── 티어별 ──");
            for (int i = 0; i < table.TierCount; i++)
            {
                string id = table.GetStageId(i);
                bool   ok = IsTierCleared(i, SaveManager.Instance);
                sb.AppendLine($"   {i}. {id ?? "(연결 안 됨)"} → {(ok ? "클리어" : "미클리어")}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
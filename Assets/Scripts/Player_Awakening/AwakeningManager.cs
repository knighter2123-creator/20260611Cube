using System;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 각성(진화) 단계에 따른 영구 강화 효과를 관리합니다.
/// LoginScene의 Managers 루트 아래에 자식 오브젝트로 두세요.
/// (이 프로젝트의 다른 매니저들과 같은 배치 — 자식으로 둬야 하나를 꺼도 루트가 안 죽습니다)
///
/// ══════════════════════════════════════════════════════════════════
///  ★ 설계 핵심 — "상태를 저장하지 않고 매번 계산한다"
/// ══════════════════════════════════════════════════════════════════
///
/// 이 매니저는 각성 단계를 **SaveData에 따로 저장하지 않습니다.**
/// 세이브에 이미 있는 claimedEvolveRewards(보상 지급 완료된 티어 id 목록)에서
/// 매번 다시 계산합니다.
///
/// 왜 이렇게 했는가:
///
///  1) 세이브 마이그레이션이 필요 없습니다.
///     SaveData에 awakeningLevel 같은 필드를 새로 추가하면, 기존 플레이어의
///     세이브에는 그 필드가 없어서 0으로 역직렬화됩니다. 이미 각성 3단계까지
///     깬 사람이 0단계로 되돌아가요. learnings.md에 적어두신
///     "int → long 타입 변경이 값을 조용히 0으로 만든 사고"와 같은 종류입니다.
///     기존 필드에서 파생시키면 이 위험 자체가 사라집니다.
///
///  2) 두 값이 어긋날 수 없습니다.
///     같은 사실(=3단계 깼다)을 두 군데 저장하면 반드시 언젠가 갈라집니다.
///     보상은 지급됐는데 레벨은 안 오른 상태 같은 게 생기죠.
///     "하나의 사실은 한 곳에만(Single Source of Truth)"이 원칙입니다.
///
///  3) 순서에 의존하지 않습니다.
///     각 티어의 클리어 여부를 개별로 확인하므로, 플레이어가 3단계를
///     1단계보다 먼저 깨도 보상 계산이 정확합니다.
///
/// 대가는 "계산 비용"인데, 티어 5개 반복이라 사실상 0입니다.
/// 그마저도 캐시해 두고 필요할 때만 Refresh()합니다.
/// ══════════════════════════════════════════════════════════════════
/// </summary>
public class AwakeningManager : MonoBehaviour
{
    public static AwakeningManager Instance { get; private set; }

    [Header("보상 정의")]
    [Tooltip("AwakeningRewardTable 에셋을 연결하세요. 없으면 각성 효과가 전부 0입니다.")]
    [SerializeField] private AwakeningRewardTable table;

    [Header("디버그")]
    [Tooltip("각성 단계가 갱신될 때마다 상세 로그를 남깁니다.")]
    [SerializeField] private bool verboseLog = true;

    // ══════════════════════════════════════════════
    //  런타임 상태 (전부 Refresh()가 다시 계산)
    // ══════════════════════════════════════════════

    /// <summary>
    /// 보상 테이블. 입장 패널(EvolveStageEntry)이 보상 미리보기에 씁니다.
    ///
    /// ★ EvolveStageEntry 에 테이블을 따로 연결하지 않고 여기서 빌려가게 한 이유:
    ///   같은 에셋을 두 곳에 연결해 두면, 나중에 테이블을 교체할 때 한 곳만 바꾸고
    ///   다른 쪽은 옛 에셋을 계속 가리킵니다. 연결 지점은 하나일수록 좋습니다.
    /// </summary>
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

    // ══════════════════════════════════════════════════════════════════
    //  ★ 각성 축하 연출 예약
    //
    //  각성 클리어는 EvolveScene에서 일어나는데, 클리어 처리 직후
    //  ReturnToStage()가 **즉시 씬을 전환합니다.** 그 자리에서 연출을 재생하면
    //  한두 프레임 만에 씬과 함께 사라져서 플레이어는 아무것도 못 봅니다.
    //
    //  그래서 "축하할 일이 생겼다"는 사실만 여기 남겨두고, 메인 스테이지에
    //  도착한 새 플레이어가 그걸 가져가 재생합니다.
    //
    //  ★ 일반화: 연출의 '발생 시점'과 '재생 시점'이 다를 수 있습니다.
    //    씬을 넘어 살아남는 매니저에 깃발을 세워두고, 넘어간 쪽이
    //    꺼내 쓰는(그리고 꺼내면 사라지는) 패턴은 아주 자주 쓰입니다.
    // ══════════════════════════════════════════════════════════════════
    private bool pendingCelebration;

    // 첫 Refresh()인지 여부. 세이브 복원과 실제 달성을 구분하는 데 씁니다.
    private bool initialized;

    /// <summary>
    /// 아직 재생하지 않은 각성 축하 연출이 있으면 true를 돌려주고 예약을 지웁니다.
    /// 한 번 가져가면 사라지므로 두 곳에서 동시에 재생될 일이 없습니다.
    /// </summary>
    public bool ConsumeCelebration()
    {
        bool value = pendingCelebration;
        pendingCelebration = false;
        return value;
    }

    /// <summary>
    /// 각성 상태가 바뀌었을 때 발생. 스프라이트/UI가 구독합니다.
    ///
    /// ★ 매니저가 SpriteRenderer를 직접 들고 바꾸지 않는 이유
    ///   learnings.md의 "데이터/UI 분리" 교훈 그대로입니다. 매니저가 씬 오브젝트
    ///   참조를 들면, 씬이 바뀔 때 그 참조가 죽어서 매니저 전체가 고장납니다.
    ///   매니저는 "숫자"만 들고, 화면은 이벤트를 듣는 쪽이 알아서 합니다.
    /// </summary>
    public event Action OnChanged;

    // ══════════════════════════════════════════════
    //  라이프사이클
    // ══════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // ★ 씬이 로드될 때마다 다시 계산합니다.
        //
        //   각성 클리어 처리는 EvolveBoss(보상 지급) → EvolveStageManager(클리어 보고)
        //   → 씬 전환 순으로 일어나는데, 앞의 두 개는 서로 다른 스크립트라
        //   실행 순서를 100% 보장하기 어렵습니다. 씬 로드 시점에 한 번 더 계산해 두면
        //   순서가 어떻든 메인 스테이지에 도착했을 때는 반드시 맞는 값이 됩니다.
        //
        //   "정답을 한 번에 맞히려고 애쓰기"보다 "틀려도 곧 스스로 고쳐지게 만들기"가
        //   이런 다중 스크립트 상황에서는 훨씬 튼튼합니다.
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // Awake가 아니라 Start인 이유: SaveManager가 Awake에서 Load()를 합니다.
        // 같은 Awake 단계에서 읽으면 실행 순서에 따라 Current가 아직 null일 수 있어요.
        Refresh();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Refresh();

    // ══════════════════════════════════════════════
    //  외부에서 부르는 지점
    // ══════════════════════════════════════════════

    /// <summary>
    /// 각성 스테이지를 클리어했을 때 호출 (EvolveStageManager.ReportBossKill).
    /// 값을 직접 올리지 않고 세이브에서 다시 읽습니다 — 중복 호출돼도 안전합니다.
    /// </summary>
    public void ReportEvolveCleared(string stageId)
    {
        if (verboseLog)
            Debug.Log($"[각성] 클리어 보고 수신 — {stageId ?? "(id 없음)"}");

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

        // ★ 경고는 한 번만 남깁니다.
        //   Refresh()는 씬이 로드될 때마다 돌기 때문에, 그냥 LogWarning을 두면
        //   연결 하나 빠뜨렸을 때 콘솔이 같은 줄로 가득 차서 정작 봐야 할
        //   다른 에러가 스크롤 밖으로 밀려납니다.
        //   "주기적으로 실행되는 코드 안의 로그"는 항상 이 점을 의심해야 합니다.
        if (table == null)
        {
            if (!warnedNoTable)
            {
                warnedNoTable = true;
                Debug.LogWarning("[각성] 보상 테이블이 연결되지 않았습니다. 각성 효과가 적용되지 않습니다.", this);
            }
        }
        else if (save == null)
        {
            if (!warnedNoSave)
            {
                warnedNoSave = true;
                Debug.LogWarning("[각성] SaveManager가 없어 각성 단계를 읽을 수 없습니다.", this);
            }
        }
        else
        {
            for (int i = 0; i < table.TierCount; i++)
            {
                string id = table.GetStageId(i);
                if (string.IsNullOrEmpty(id)) continue;            // 연결 안 된 칸은 건너뜀
                if (!save.IsEvolveRewardClaimed(id)) continue;     // 아직 못 깬 단계

                level++;
                AwakeningRewardTable.Tier tier = table.GetTier(i);
                projectiles += tier.addProjectiles;
                bursts      += tier.addBurstShots;

                // 스프라이트는 '마지막으로 지정된 것'이 이깁니다.
                // 중간 단계에 아트가 없으면(null) 이전 모습이 그대로 유지돼요.
                if (tier.playerSprite != null) sprite = tier.playerSprite;
            }
        }

        // 값이 그대로면 이벤트를 쏘지 않습니다.
        // (씬 로드마다 Refresh가 돌기 때문에, 안 그러면 구독자가 매번 헛일을 합니다)
        bool changed = level       != Level
                    || projectiles != ExtraProjectiles
                    || bursts      != ExtraBurstShots
                    || sprite      != CurrentSprite;

        // ★ 축하 연출 예약은 두 조건을 모두 만족할 때만:
        //     ① 단계가 '올라갔을 때' (세이브 삭제로 내려가는 건 축하할 일이 아님)
        //     ② '첫 계산'이 아닐 때
        //
        //   ②가 없으면 심각한 오작동이 납니다.
        //   각성 3단계까지 깬 플레이어가 게임을 껐다 켜면 첫 Refresh에서
        //   0 → 3 으로 올라가는 것처럼 보여, 아무것도 안 했는데 축하 연출이 터집니다.
        //
        //   "처음 불러온 것"과 "지금 막 달성한 것"은 값만 봐서는 구분되지 않습니다.
        //   이건 ApplyFrom에서 OnLevelUp을 쏴서 접속하자마자 레벨업 연출이
        //   터졌던 것과 완전히 같은 함정이에요.
        if (initialized && level > Level) pendingCelebration = true;
        initialized = true;

        Level            = level;
        ExtraProjectiles = projectiles;
        ExtraBurstShots  = bursts;
        CurrentSprite    = sprite;

        if (!changed) return;

        if (verboseLog)
            Debug.Log($"[각성] 갱신 — {Level}단계 / 발사체 {ProjectilesPerVolley}발 " +
                      $"× 연사 {VolleysPerAttack}회 = 공격당 {ProjectilesPerVolley * VolleysPerAttack}발");

        OnChanged?.Invoke();
    }

    // ══════════════════════════════════════════════
    //  에디터 테스트
    // ══════════════════════════════════════════════

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
                bool   ok = !string.IsNullOrEmpty(id) && SaveManager.Instance.IsEvolveRewardClaimed(id);
                sb.AppendLine($"   {i}. {id ?? "(연결 안 됨)"} → {(ok ? "클리어" : "미클리어")}");
            }
        }

        Debug.Log(sb.ToString());
    }
}
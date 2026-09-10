using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 시스템의 중앙 허브 — 코어.
///
/// 이 클래스는 partial 로 세 파일에 나뉘어 있습니다.
///   AugmentManager.cs        ← 지금 이 파일. 싱글턴 / 등장 조건 / 영구 스택
///   AugmentManager.Buffs.cs  ← 지속시간이 있는 임시 버프
///   AugmentManager.Save.cs   ← 저장 / 복구
///
/// 프로젝트의 StageManager.cs + StageManager.Save.cs,
/// Enemy.cs + Enemy.Debuffs.cs 와 같은 나누기 방식입니다.
///
/// [다른 스크립트가 쓰는 방법 — 이게 전부입니다]
///   float dmg     = baseDamage * AugmentManager.Attack;
///   float critMul = baseCritMultiplier + AugmentManager.CritDamage;
///   float def     = enemyDefence * AugmentManager.EnemyDefense;
///   float delay   = respawnDelay * AugmentManager.SpawnDelay;
///
/// 전부 static 프로퍼티라 매니저가 씬에 없어도 NullReference 없이
/// 기본값(1 또는 0)이 돌아옵니다. 그래서 기존 코드를 안전하게 고칠 수 있습니다.
/// </summary>
public partial class AugmentManager : MonoBehaviour
{
    public static AugmentManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────
    //  인스펙터 설정
    // ─────────────────────────────────────────────────────────
    [Header("연결")]
    [SerializeField] private AugmentPool     pool;
    [SerializeField] private AugmentSelectUI ui;

    [Header("등장 규칙")]
    [Tooltip("이 스테이지 번호를 클리어하면 카드가 나옵니다. 기본 {10} = 보스 스테이지마다")]
    [SerializeField] private int[] triggerStages = { 10 };

    [Tooltip("한 번에 보여줄 카드 장수")]
    [Min(1)]
    [SerializeField] private int cardCount = 3;

    [Tooltip("스테이지 클리어 후 몇 초 뒤에 카드창을 띄울지. " +
             "클리어 알림 연출이 가려지지 않게 살짝 늦추는 용도입니다. " +
             "StageManager 의 스테이지 전환 대기시간보다 짧게 두세요.")]
    [SerializeField] private float openDelay = 1.0f;

    [Header("디버그")]
    [SerializeField] private bool logEnabled = true;

    // ─────────────────────────────────────────────────────────
    //  런타임 상태
    // ─────────────────────────────────────────────────────────

    /// <summary>영구 카드 ID → 몇 번 골랐는지</summary>
    private readonly Dictionary<string, int> permanentStacks = new Dictionary<string, int>();

    // 계산된 영구 배율. 매 프레임 다시 계산하지 않고 변할 때만 갱신합니다.
    // (방치형은 매 프레임 비용이 곧 배터리입니다)
    private float attackMultiplier = 1f;
    private float critDamageBonus  = 0f;

    private int currentWorld = 1;
    private int currentStage = 1;

    /// <summary>증강 값이 바뀔 때마다 호출됩니다. UI 갱신 등에 연결하세요.</summary>
    public event Action OnChanged;

    // ─────────────────────────────────────────────────────────
    //  외부에서 쓰는 static 접근자 (매니저가 없어도 안전)
    // ─────────────────────────────────────────────────────────
    public static float Attack       => Instance != null ? Instance.attackMultiplier : 1f;
    public static float CritDamage   => Instance != null ? Instance.critDamageBonus  : 0f;
    public static float SpawnDelay   => Instance != null ? Instance.spawnDelayMul    : 1f;   // Buffs.cs
    public static float EnemyDefense => Instance != null ? Instance.enemyDefenseMul  : 1f;   // Buffs.cs

    // 인스펙터 디버깅용 인스턴스 프로퍼티
    public float AttackMultiplier => attackMultiplier;
    public float CritDamageBonus  => critDamageBonus;

    // ─────────────────────────────────────────────────────────
    //  라이프사이클
    // ─────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (ui == null) ui = FindFirstObjectByType<AugmentSelectUI>();

        Load();                  // → Save.cs
        RecalculatePermanent();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // partial 로 나뉘어 있어도 Update 는 클래스당 하나만 있을 수 있습니다.
        // 그래서 코어에 두고, 각 파트의 갱신 함수를 여기서 부릅니다.
        TickTempBuffs();         // → Buffs.cs
    }

    // ─────────────────────────────────────────────────────────
    //  스테이지 연동 — StageManager 가 부르는 함수
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 스테이지를 하나 클리어했을 때 호출하세요. 조건에 맞으면 카드 선택창이 뜹니다.
    /// </summary>
    /// <param name="world">방금 클리어한 월드 번호</param>
    /// <param name="stage">방금 클리어한 스테이지 번호 (1~10)</param>
    public void OnStageCleared(int world, int stage)
    {
        currentWorld = world;
        currentStage = stage;

        ClearStageScopedBuffs();   // → Buffs.cs

        if (!IsTriggerStage(stage)) return;

        if (openDelay > 0f) StartCoroutine(OpenAfterDelay(openDelay));
        else                Open();
    }

    /// <summary>스테이지가 바뀔 때(클리어가 아니어도) 현재 위치를 알려줍니다.</summary>
    public void SetCurrentStage(int world, int stage)
    {
        currentWorld = world;
        currentStage = stage;
    }

    private bool IsTriggerStage(int stage)
    {
        if (triggerStages == null) return false;

        for (int i = 0; i < triggerStages.Length; i++)
            if (triggerStages[i] == stage) return true;

        return false;
    }

    private IEnumerator OpenAfterDelay(float delay)
    {
        // WaitForSecondsRealtime 을 쓰는 이유:
        // 다른 연출이 timeScale 을 건드리고 있어도 대기 시간이 늘어지지 않게 하기 위해서입니다.
        yield return new WaitForSecondsRealtime(delay);
        Open();
    }

    /// <summary>카드 선택창을 엽니다. (테스트나 광고 보상용으로도 쓸 수 있습니다)</summary>
    public void Open()
    {
        if (pool == null) { Debug.LogError("[Augment] pool 이 비어 있습니다."); return; }
        if (ui   == null) { Debug.LogError("[Augment] AugmentSelectUI 를 찾지 못했습니다."); return; }

        // 지금 스택 수를 알려주는 함수를 넘겨서, 꽉 찬 카드는 후보에서 빠지게 합니다.
        var picks = pool.Draw(cardCount, StackOf, currentWorld);

        if (picks.Count == 0)
        {
            if (logEnabled) Debug.LogWarning("[Augment] 뽑을 카드가 없습니다. 풀을 확인하세요.");
            return;
        }

        // 화면에 보이는 장수와 실제로 뽑은 장수가 다르면 UI 쪽 문제입니다.
        // 이 로그가 있으면 "코드가 몇 장을 준 건지"를 즉시 구분할 수 있습니다.
        if (logEnabled)
            Debug.Log($"[Augment] 카드 {picks.Count}장 표시 (요청 {cardCount}장) — {currentWorld}-{currentStage}");

        ui.Show(picks, OnCardChosen);
    }

    /// <summary>플레이어(또는 자동선택 타이머)가 카드를 고른 뒤 불립니다.</summary>
    private void OnCardChosen(AugmentCard card)
    {
        if (card == null) return;

        card.Apply(this, isRestore: false);

        // 영구 카드였다면 스택이 늘었으니 배율을 다시 계산합니다.
        if (card.IsPermanent)
        {
            RecalculatePermanent();
            Save();                // → Save.cs
        }

        if (logEnabled)
            Debug.Log($"[Augment] 선택: {card.DisplayName} / 공격력 x{attackMultiplier:0.##}, " +
                      $"치명타 +{critDamageBonus * 100f:0.#}%p");

        OnChanged?.Invoke();
    }

    // ─────────────────────────────────────────────────────────
    //  영구 스택
    // ─────────────────────────────────────────────────────────

    public int StackOf(AugmentCard card)
    {
        if (card == null) return 0;
        return permanentStacks.TryGetValue(card.Id, out int n) ? n : 0;
    }

    /// <summary>영구 카드가 Apply 안에서 부르는 함수. 스택만 1 올립니다.</summary>
    public void AddPermanentStack(AugmentCard card)
    {
        if (card == null) return;

        permanentStacks.TryGetValue(card.Id, out int n);
        permanentStacks[card.Id] = n + 1;
    }

    /// <summary>
    /// 영구 배율을 0부터 다시 계산합니다.
    ///
    /// [왜 매번 처음부터 다시 계산하나]
    /// "누적해서 더하기"는 편해 보이지만, 저장 복구·환생(리셋)·밸런스 패치 때
    /// 값이 어긋나기 시작하면 원인을 찾을 수 없습니다.
    /// 스택 수만 진실로 삼고 배율은 항상 다시 계산하면, 어떤 경로로 와도 결과가 같습니다.
    /// 이걸 '단일 진실 공급원(single source of truth)' 이라고 부릅니다.
    ///
    /// Enemy.ApplyStatMultiplier() 의 "누적(*=)이 아니라 대입(=)" 과 같은 이야기예요.
    /// </summary>
    public void RecalculatePermanent()
    {
        attackMultiplier = 1f;
        critDamageBonus  = 0f;

        if (pool == null) return;

        foreach (var kv in permanentStacks)
        {
            var card = pool.FindById(kv.Key);
            if (card == null) continue;   // 에셋이 삭제된 경우 조용히 건너뜀

            for (int i = 0; i < kv.Value; i++)
                card.ContributePermanent(this);
        }

        OnChanged?.Invoke();
    }

    // ContributePermanent 안에서만 쓰이는 누산 함수들
    public void MultiplyAttack(float mul)    => attackMultiplier *= mul;
    public void AddAttackAdditive(float add) => attackMultiplier += add;
    public void AddCritDamage(float add)     => critDamageBonus  += add;

    /// <summary>모든 증강 초기화. 환생/전직 시스템에서 부르세요.</summary>
    public void ResetAll()
    {
        permanentStacks.Clear();
        ClearAllTempBuffs();     // → Buffs.cs
        RecalculatePermanent();
        Save();                  // → Save.cs
    }

    // ─────────────────────────────────────────────────────────
    //  에디터 테스트
    // ─────────────────────────────────────────────────────────
    [ContextMenu("테스트: 카드 선택창 열기")]
    private void TestOpen() => Open();

    [ContextMenu("테스트: 보너스 전체 초기화")]
    private void TestReset() => ResetAll();

    [ContextMenu("테스트: 현재 상태 출력")]
    private void TestDump()
    {
        Debug.Log($"[Augment] 공격력 x{attackMultiplier:0.###} / 치명타 +{critDamageBonus * 100f:0.#}%p " +
                  $"/ 스폰주기 x{spawnDelayMul:0.###} / 적방어 x{enemyDefenseMul:0.###} " +
                  $"/ 영구 {permanentStacks.Count}종");
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 시스템의 중앙 허브.
///
/// 하는 일은 크게 넷입니다.
///   1) 스테이지 클리어 신호를 받아 카드 선택창을 띄운다
///   2) 영구 카드 스택을 모아 최종 배율(공격력·치명타)을 계산한다
///   3) 임시 버프의 남은 시간을 관리하고 만료시키다
///   4) 영구 스택을 저장/복구한다
///
/// [다른 스크립트가 쓰는 방법 — 이게 전부입니다]
///   float dmg = baseDamage * AugmentManager.Attack;
///   float critMul = baseCritMultiplier + AugmentManager.CritDamage;
///   float def = enemyDefence * AugmentManager.EnemyDefense;
///   float delay = respawnDelay * AugmentManager.SpawnDelay;
///
/// 전부 static 프로퍼티라 매니저가 씬에 없어도 NullReference 없이
/// 기본값(1 또는 0)이 돌아옵니다. 그래서 기존 코드를 안전하게 고칠 수 있습니다.
/// </summary>
public class AugmentManager : MonoBehaviour
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
             "0이면 즉시. 클리어 알림 연출이 가려지지 않게 살짝 늦추는 용도입니다. " +
             "StageManager 의 스테이지 전환 대기시간보다 짧게 두세요.")]
    [SerializeField] private float openDelay = 1.0f;

    [Header("저장")]
    [Tooltip("영구 증강을 PlayerPrefs 에 저장합니다")]
    [SerializeField] private bool saveEnabled = true;

    [SerializeField] private string saveKey = "AUGMENT_SAVE_V1";

    [Header("디버그")]
    [SerializeField] private bool logEnabled = true;

    // ─────────────────────────────────────────────────────────
    //  런타임 상태
    // ─────────────────────────────────────────────────────────

    /// <summary>영구 카드 ID → 몇 번 골랐는지</summary>
    private readonly Dictionary<string, int> permanentStacks = new Dictionary<string, int>();

    /// <summary>지속시간이 있는 임시 버프 하나</summary>
    private class TempBuff
    {
        public AugmentBuffKind kind;
        public float multiplier;
        public float endTime;      // Time.time 기준 만료 시각. 0 이하 duration 이면 float.MaxValue
        public bool  stageScoped;  // 스테이지가 바뀌면 사라지는가
    }

    private readonly List<TempBuff> tempBuffs = new List<TempBuff>();

    // 계산된 최종 값들 (매 프레임 다시 계산하지 않고 변할 때만 갱신 — 방치형은 성능이 곧 배터리입니다)
    private float attackMultiplier = 1f;
    private float critDamageBonus  = 0f;
    private float spawnDelayMul    = 1f;
    private float enemyDefenseMul  = 1f;

    private int currentWorld = 1;
    private int currentStage = 1;

    /// <summary>증강 값이 바뀔 때마다 호출됩니다. UI 갱신 등에 연결하세요.</summary>
    public event Action OnChanged;

    // ─────────────────────────────────────────────────────────
    //  외부에서 쓰는 static 접근자 (매니저가 없어도 안전)
    // ─────────────────────────────────────────────────────────
    public static float Attack       => Instance != null ? Instance.attackMultiplier : 1f;
    public static float CritDamage   => Instance != null ? Instance.critDamageBonus  : 0f;
    public static float SpawnDelay   => Instance != null ? Instance.spawnDelayMul    : 1f;
    public static float EnemyDefense => Instance != null ? Instance.enemyDefenseMul  : 1f;

    // 인스턴스 버전 (인스펙터 디버깅용)
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

        Load();
        RecalculatePermanent();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 임시 버프 만료 검사.
        // Time.time 을 쓰기 때문에 선택창이 떠서 timeScale = 0 인 동안에는
        // 버프 시간이 흐르지 않습니다. (플레이어가 고민하는 동안 손해 보면 안 되니까)
        if (tempBuffs.Count == 0) return;

        bool changed = false;
        for (int i = tempBuffs.Count - 1; i >= 0; i--)
        {
            if (Time.time >= tempBuffs[i].endTime)
            {
                tempBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed) RecalculateTemp();
    }

    // ─────────────────────────────────────────────────────────
    //  스테이지 연동 — StageManager 가 부르는 함수
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 스테이지를 하나 클리어했을 때 호출하세요.
    /// 조건에 맞으면 카드 선택창이 뜹니다.
    /// </summary>
    /// <param name="world">방금 클리어한 월드 번호</param>
    /// <param name="stage">방금 클리어한 스테이지 번호 (1~10)</param>
    public void OnStageCleared(int world, int stage)
    {
        currentWorld = world;
        currentStage = stage;

        // 스테이지 한정 버프는 여기서 정리합니다.
        ClearStageScopedBuffs();

        if (!IsTriggerStage(stage)) return;

        if (openDelay > 0f) StartCoroutine(OpenAfterDelay(openDelay));
        else                Open();
    }

    private IEnumerator OpenAfterDelay(float delay)
    {
        // WaitForSecondsRealtime 을 쓰는 이유:
        // 다른 연출이 timeScale 을 건드리고 있어도 대기 시간이 늘어지지 않게 하기 위해서입니다.
        yield return new WaitForSecondsRealtime(delay);
        Open();
    }

    /// <summary>스테이지가 바뀔 때(클리어가 아니어도) 불러주면 스테이지 한정 버프가 정리됩니다.</summary>
    public void SetCurrentStage(int world, int stage)
    {
        currentWorld = world;
        currentStage = stage;
    }

    private bool IsTriggerStage(int stage)
    {
        if (triggerStages == null || triggerStages.Length == 0) return false;
        for (int i = 0; i < triggerStages.Length; i++)
            if (triggerStages[i] == stage) return true;
        return false;
    }

    /// <summary>카드 선택창을 강제로 엽니다. (테스트나 광고 보상용으로도 쓸 수 있습니다)</summary>
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
            Save();
        }

        if (logEnabled)
            Debug.Log($"[Augment] 선택: {card.DisplayName} / 공격력 x{attackMultiplier:0.##}, 치명타 +{critDamageBonus * 100f:0.#}%p");

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
    public void MultiplyAttack(float mul)      => attackMultiplier *= mul;
    public void AddAttackAdditive(float add)   => attackMultiplier += add;
    public void AddCritDamage(float add)       => critDamageBonus  += add;

    // ─────────────────────────────────────────────────────────
    //  임시 버프
    // ─────────────────────────────────────────────────────────

    /// <param name="duration">0 이하면 스테이지가 끝날 때까지 유지</param>
    public void AddTempBuff(AugmentBuffKind kind, float multiplier, float duration)
    {
        bool stageScoped = duration <= 0f;

        tempBuffs.Add(new TempBuff
        {
            kind        = kind,
            multiplier  = Mathf.Clamp(multiplier, 0.01f, 10f),
            endTime     = stageScoped ? float.MaxValue : Time.time + duration,
            stageScoped = stageScoped
        });

        RecalculateTemp();
    }

    private void ClearStageScopedBuffs()
    {
        bool changed = false;
        for (int i = tempBuffs.Count - 1; i >= 0; i--)
        {
            if (tempBuffs[i].stageScoped) { tempBuffs.RemoveAt(i); changed = true; }
        }
        if (changed) RecalculateTemp();
    }

    /// <summary>같은 종류의 버프는 곱으로 누적됩니다. (0.7 × 0.7 = 0.49)</summary>
    private void RecalculateTemp()
    {
        spawnDelayMul   = 1f;
        enemyDefenseMul = 1f;

        for (int i = 0; i < tempBuffs.Count; i++)
        {
            switch (tempBuffs[i].kind)
            {
                case AugmentBuffKind.SpawnDelay:   spawnDelayMul   *= tempBuffs[i].multiplier; break;
                case AugmentBuffKind.EnemyDefense: enemyDefenseMul *= tempBuffs[i].multiplier; break;
            }
        }

        OnChanged?.Invoke();
    }

    /// <summary>남은 시간이 가장 긴 버프의 잔여 초. UI 표시용. 없으면 0</summary>
    public float GetRemaining(AugmentBuffKind kind)
    {
        float best = 0f;
        for (int i = 0; i < tempBuffs.Count; i++)
        {
            if (tempBuffs[i].kind != kind) continue;
            if (tempBuffs[i].stageScoped) continue;
            best = Mathf.Max(best, tempBuffs[i].endTime - Time.time);
        }
        return Mathf.Max(0f, best);
    }

    // ─────────────────────────────────────────────────────────
    //  저장 / 복구
    // ─────────────────────────────────────────────────────────

    // JsonUtility 는 Dictionary 를 직렬화하지 못합니다.
    // 그래서 ID 배열과 개수 배열 두 개로 펼쳐서 저장합니다. (같은 인덱스끼리 짝)
    [Serializable]
    private class SaveData
    {
        public string[] ids;
        public int[]    counts;
    }

    public void Save()
    {
        if (!saveEnabled) return;

        var data = new SaveData
        {
            ids    = new string[permanentStacks.Count],
            counts = new int[permanentStacks.Count]
        };

        int i = 0;
        foreach (var kv in permanentStacks)
        {
            data.ids[i]    = kv.Key;
            data.counts[i] = kv.Value;
            i++;
        }

        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public void Load()
    {
        permanentStacks.Clear();
        if (!saveEnabled) return;
        if (!PlayerPrefs.HasKey(saveKey)) return;

        try
        {
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(saveKey));
            if (data?.ids == null || data.counts == null) return;

            int n = Mathf.Min(data.ids.Length, data.counts.Length);
            for (int i = 0; i < n; i++)
            {
                if (string.IsNullOrEmpty(data.ids[i])) continue;
                permanentStacks[data.ids[i]] = data.counts[i];
            }
        }
        catch (Exception e)
        {
            // 저장 파일이 깨져도 게임은 계속 돌아가야 합니다.
            Debug.LogWarning($"[Augment] 저장 데이터를 읽지 못했습니다: {e.Message}");
            permanentStacks.Clear();
        }
    }

    /// <summary>모든 영구 증강 초기화. 환생/전직 시스템에서 부르세요.</summary>
    public void ResetAll()
    {
        permanentStacks.Clear();
        tempBuffs.Clear();
        RecalculatePermanent();
        RecalculateTemp();
        Save();
    }

    // ─────────────────────────────────────────────────────────
    //  에디터 테스트
    // ─────────────────────────────────────────────────────────
    [ContextMenu("테스트: 카드 선택창 열기")]
    private void TestOpen() => Open();

    [ContextMenu("테스트: 증강 전체 초기화")]
    private void TestReset() => ResetAll();

    [ContextMenu("테스트: 현재 상태 출력")]
    private void TestDump()
    {
        Debug.Log($"[Augment] 공격력 x{attackMultiplier:0.###} / 치명타 +{critDamageBonus * 100f:0.#}%p " +
                  $"/ 스폰주기 x{spawnDelayMul:0.###} / 적방어 x{enemyDefenseMul:0.###} / 영구 {permanentStacks.Count}종");
    }
}

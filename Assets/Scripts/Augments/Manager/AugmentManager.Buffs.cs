using System.Collections.Generic;
using UnityEngine;

/// <summary>증강 임시 버프("적 생성 주기 30% 감소, 45초간" 등): 종류별 배율(×0.7)로 다루고 만료·스테이지 종료 때 제거 — 뺄셈이면 후반에 무의미하고 음수가 될 수 있어서.</summary>
public partial class AugmentManager
{
    /// <summary>진행 중인 임시 버프 하나.</summary>
    private class TempBuff
    {
        public AugmentBuffKind kind;
        public float multiplier;
        public float endTime;      // Time.time 기준 만료 시각
        public bool  stageScoped;  // 스테이지가 바뀌면 사라지는가
    }

    private readonly List<TempBuff> tempBuffs = new List<TempBuff>();

    // 계산된 최종 배율. 코어의 static 접근자(SpawnDelay / EnemyDefense)가 이 값을 읽습니다.
    private float spawnDelayMul   = 1f;
    private float enemyDefenseMul = 1f;

    // ── 갱신 — 코어의 Update 에서 매 프레임 호출됩니다 ──
    private void TickTempBuffs()
    {
        if (tempBuffs.Count == 0) return;   // 대부분의 프레임은 여기서 끝납니다

        // Time.time — 카드 선택창(timeScale 0) 동안 버프 시간이 흐르지 않게 (unscaledTime 이면 흘러감)
        float now = Time.time;
        bool  changed = false;

        // 역순으로 지움 (앞에서 지우면 당겨진 원소를 건너뜀)
        for (int i = tempBuffs.Count - 1; i >= 0; i--)
        {
            if (now < tempBuffs[i].endTime) continue;

            tempBuffs.RemoveAt(i);
            changed = true;
        }

        if (changed) RecalculateTemp();
    }

    // ── 등록 / 해제 ──

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

    /// <summary>'이번 스테이지 동안' 버프만 정리합니다. 스테이지 클리어 시 호출.</summary>
    private void ClearStageScopedBuffs()
    {
        bool changed = false;

        for (int i = tempBuffs.Count - 1; i >= 0; i--)
        {
            if (!tempBuffs[i].stageScoped) continue;

            tempBuffs.RemoveAt(i);
            changed = true;
        }

        if (changed) RecalculateTemp();
    }

    /// <summary>모든 임시 버프 제거. ResetAll 에서 호출.</summary>
    private void ClearAllTempBuffs()
    {
        if (tempBuffs.Count == 0) return;

        tempBuffs.Clear();
        RecalculateTemp();
    }

    // ── 계산 ──

    /// <summary>같은 종류는 곱으로 누적(0.7 × 0.7 = 0.49), 매번 1부터 다시 계산해 붙고 떨어지는 순서와 무관하게 같은 결과.</summary>
    private void RecalculateTemp()
    {
        spawnDelayMul   = 1f;
        enemyDefenseMul = 1f;

        for (int i = 0; i < tempBuffs.Count; i++)
        {
            switch (tempBuffs[i].kind)
            {
                case AugmentBuffKind.SpawnDelay:
                    spawnDelayMul *= tempBuffs[i].multiplier;
                    break;

                case AugmentBuffKind.EnemyDefense:
                    enemyDefenseMul *= tempBuffs[i].multiplier;
                    break;
            }
        }

        OnChanged?.Invoke();
    }

}

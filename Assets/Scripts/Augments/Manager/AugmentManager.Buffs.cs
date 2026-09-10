using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 시스템 — 지속시간이 있는 임시 버프.
///
/// "적 생성 주기 30% 감소, 45초간" 같은 카드가 여기서 관리됩니다.
///
/// [설계 원칙 — 배율 레이어]
/// 값을 직접 빼지 않고 배율(×0.7)로 다룹니다.
/// 뺄셈으로 하면 적이 강해질수록 효과가 무의미해지고 음수가 되는 사고도 생깁니다.
/// 배율은 어느 구간에서든 체감이 일정합니다.
/// 프로젝트의 TargetMove 가 baseSpeed × spawnSpeedMult × slowMultiplier 로
/// 레이어를 쌓는 것과 똑같은 방식입니다.
/// </summary>
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

    // ─────────────────────────────────────────────────────────
    //  갱신 — 코어의 Update 에서 매 프레임 호출됩니다
    // ─────────────────────────────────────────────────────────
    private void TickTempBuffs()
    {
        if (tempBuffs.Count == 0) return;   // 대부분의 프레임은 여기서 끝납니다

        // ★ Time.time 을 쓰는 이유
        //   카드 선택창이 떠서 timeScale = 0 인 동안에는 Time.time 도 멈춥니다.
        //   즉 플레이어가 고민하는 시간만큼 버프가 손해 보지 않습니다.
        //   (unscaledTime 을 쓰면 멈춘 동안에도 버프가 흘러가 버립니다)
        float now = Time.time;
        bool  changed = false;

        // ─── 역순 순회 (학습 포인트) ────────────────────────────────
        // 앞에서부터 돌면서 지우면, 지운 자리로 뒤 원소가 당겨오면서 한 칸씩 건너뜁니다.
        // 뒤에서부터 지우면 아직 방문하지 않은 앞쪽 인덱스가 흔들리지 않습니다.
        // StageManager.NextStage() 가 Enemy.Active 를 도는 방식과 같습니다.
        // ────────────────────────────────────────────────────────
        for (int i = tempBuffs.Count - 1; i >= 0; i--)
        {
            if (now < tempBuffs[i].endTime) continue;

            tempBuffs.RemoveAt(i);
            changed = true;
        }

        if (changed) RecalculateTemp();
    }

    // ─────────────────────────────────────────────────────────
    //  등록 / 해제
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

    // ─────────────────────────────────────────────────────────
    //  계산
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 같은 종류의 버프는 곱으로 누적됩니다. (0.7 × 0.7 = 0.49)
    ///
    /// 여기서도 "누적하지 않고 매번 1부터 다시 계산" 원칙을 지킵니다.
    /// 버프가 붙고 떨어지는 순서가 어떻든 결과가 항상 같아집니다.
    /// </summary>
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

    // ─────────────────────────────────────────────────────────
    //  조회 (HUD 표시용)
    // ─────────────────────────────────────────────────────────

    /// <summary>남은 시간이 가장 긴 버프의 잔여 초. 없으면 0.</summary>
    public float GetRemaining(AugmentBuffKind kind)
    {
        float best = 0f;
        float now  = Time.time;

        for (int i = 0; i < tempBuffs.Count; i++)
        {
            if (tempBuffs[i].kind != kind) continue;
            if (tempBuffs[i].stageScoped)  continue;

            best = Mathf.Max(best, tempBuffs[i].endTime - now);
        }

        return Mathf.Max(0f, best);
    }

    /// <summary>지금 이 종류의 버프가 걸려 있는가. HUD 아이콘 표시에 씁니다.</summary>
    public bool HasBuff(AugmentBuffKind kind)
    {
        for (int i = 0; i < tempBuffs.Count; i++)
            if (tempBuffs[i].kind == kind) return true;

        return false;
    }
}

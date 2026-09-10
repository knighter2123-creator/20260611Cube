using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 증강 카드 선택창 — "어떻게 움직이는가".
///
/// 등장 연출, 선택 연출, 자동선택 카운트다운이 모여 있습니다.
///
/// ★ 이 파일의 모든 시간 계산은 Time.unscaledDeltaTime 을 씁니다.
///   창이 떠 있는 동안 Time.timeScale 이 0이라, deltaTime 을 쓰면
///   0이 계속 더해져 연출이 첫 프레임에서 영원히 멈춥니다.
///   "일시정지 중에도 움직여야 하는 것"은 전부 unscaled 라고 기억해 두세요.
/// </summary>
public partial class AugmentSelectUI
{
    // 연출 타이밍 (초)
    private const float IntroFadeTime     = 0.15f;   // 전체 화면이 밝아지는 시간
    private const float IntroCardTime     = 0.28f;   // 카드 한 장이 팝업되는 시간
    private const float IntroCardStagger  = 0.07f;   // 카드끼리의 시간차
    private const float IntroStartScale   = 0.86f;   // 카드가 이 크기에서 시작

    private const float PickPopTime       = 0.18f;   // 고른 카드가 커졌다 돌아오는 시간
    private const float PickPopAmount     = 0.08f;   // 얼마나 커지는지 (8%)
    private const float PickFadeTime      = 0.15f;   // 창이 사라지는 시간
    private const float UnpickedAlpha     = 0.25f;   // 안 고른 카드가 흐려지는 정도

    // ─────────────────────────────────────────────────────────
    //  등장 연출
    // ─────────────────────────────────────────────────────────
    private IEnumerator PlayIntro()
    {
        rootGroup.alpha = 0f;

        // 카드를 살짝 작고 투명한 상태로 만들어 둡니다.
        var rts   = new List<RectTransform>(spawnedCards.Count);
        var group = new List<CanvasGroup>(spawnedCards.Count);

        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null) continue;

            var rt = spawnedCards[i].GetComponent<RectTransform>();
            rt.localScale = Vector3.one * IntroStartScale;
            rts.Add(rt);

            var cg = spawnedCards[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
            group.Add(cg);
        }

        float elapsed = 0f;
        float total   = IntroCardTime + IntroCardStagger * rts.Count;

        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;

            rootGroup.alpha = Mathf.Clamp01(elapsed / IntroFadeTime);

            for (int i = 0; i < rts.Count; i++)
            {
                // 카드마다 시작 시각을 조금씩 늦춰 순서대로 튀어나오게 합니다.
                float t = Mathf.Clamp01((elapsed - IntroCardStagger * i) / IntroCardTime);

                rts[i].localScale = Vector3.one *
                    Mathf.Lerp(IntroStartScale, 1f, AugmentUIFactory.EaseOutBack(t));

                if (group[i] != null) group[i].alpha = t;
            }
            yield return null;
        }

        // 코루틴이 중간에 끊겨도 최종 상태가 남도록 마지막에 확정값을 씁니다.
        rootGroup.alpha = 1f;
        for (int i = 0; i < rts.Count; i++)
        {
            rts[i].localScale = Vector3.one;
            if (group[i] != null) group[i].alpha = 1f;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  선택 연출 → 닫기 → 콜백
    // ─────────────────────────────────────────────────────────
    private IEnumerator PlayPickAndClose(AugmentCard card, GameObject chosenGo)
    {
        FadeUnchosenCards(chosenGo);

        var rt = chosenGo != null ? chosenGo.GetComponent<RectTransform>() : null;

        // 고른 카드가 커졌다가 돌아옵니다.
        float t = 0f;
        while (t < PickPopTime)
        {
            t += Time.unscaledDeltaTime;

            // Sin 곡선으로 0 → 1 → 0. 별도 조건문 없이 '튕기는' 움직임이 나옵니다.
            float k = Mathf.Sin(t / PickPopTime * Mathf.PI);
            if (rt != null) rt.localScale = Vector3.one * (1f + PickPopAmount * k);

            yield return null;
        }
        if (rt != null) rt.localScale = Vector3.one;

        // 전체 페이드 아웃
        t = 0f;
        while (t < PickFadeTime)
        {
            t += Time.unscaledDeltaTime;
            rootGroup.alpha = 1f - t / PickFadeTime;
            yield return null;
        }

        Close();

        // ★ 콜백은 창을 닫고 timeScale 을 되돌린 뒤에 부릅니다.
        //   그래야 카드 효과 안에서 연출(레벨업 이펙트 등)을 재생해도 정상 속도로 보입니다.
        //   순서를 바꾸면 "레벨업 연출이 멈춘 채로 떠 있는" 버그가 납니다.
        onChosen?.Invoke(card);
        onChosen = null;
    }

    private void FadeUnchosenCards(GameObject chosenGo)
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] == null || spawnedCards[i] == chosenGo) continue;

            var cg = spawnedCards[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = UnpickedAlpha;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  자동 선택 카운트다운
    // ─────────────────────────────────────────────────────────
    private IEnumerator AutoPickCountdown(List<AugmentCard> cards)
    {
        SetTimerVisible(true);

        float remain = autoPickSeconds;
        while (remain > 0f)
        {
            remain -= Time.unscaledDeltaTime;

            if (timerFill != null)
                timerFill.fillAmount = Mathf.Clamp01(remain / autoPickSeconds);

            if (timerLabel != null)
                timerLabel.text = $"{Mathf.CeilToInt(Mathf.Max(0f, remain))}초 후 자동 선택";

            yield return null;
        }

        timerRoutine = null;
        if (picked) yield break;

        // 시간이 다 되면 랜덤으로 한 장 고릅니다.
        // 방치형이라 "플레이어가 자리에 없다"가 정상 상황이므로,
        // 게임이 멈춘 채 방치되지 않게 하는 안전장치입니다.
        int idx = Random.Range(0, cards.Count);
        GameObject go = idx < spawnedCards.Count ? spawnedCards[idx] : null;
        OnClickCard(cards[idx], go);
    }

    private void SetTimerVisible(bool on)
    {
        // timerFill 의 부모가 게이지 배경입니다. 배경째로 켜고 끕니다.
        if (timerFill  != null) timerFill.transform.parent.gameObject.SetActive(on);
        if (timerLabel != null) timerLabel.gameObject.SetActive(on);
    }
}

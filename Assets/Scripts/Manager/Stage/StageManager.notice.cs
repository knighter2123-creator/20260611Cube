using System.Collections;
using UnityEngine;
using TMPro;

// StageManager의 스테이지 알림 패널(페이드 인/아웃) 전용 partial
public partial class StageManager
{
    [Header("스테이지 알림 패널")]
    [SerializeField] private CanvasGroup      noticeGroup;   // 패널 루트의 CanvasGroup
    [SerializeField] private TextMeshProUGUI  noticeText;    // 패널 안 텍스트

    [SerializeField] private float noticeFadeIn  = 0.25f;    // 페이드 인 시간
    [SerializeField] private float noticeHold    = 1f;     // 유지 시간
    [SerializeField] private float noticeFadeOut = 0.35f;    // 페이드 아웃 시간

    // 알림 1회 전체 재생 시간 (다음 스테이지 지연에 사용)
    private float NoticeTotal => noticeFadeIn + noticeHold + noticeFadeOut;

    private Coroutine noticeRoutine;

    /// <summary>알림 패널을 페이드 인 → 유지 → 페이드 아웃으로 표시</summary>
    private void ShowNotice(string message, Color color)
    {
        if (noticeGroup == null) return;

        // 이전 알림이 재생 중이면 중단하고 새 알림으로 교체
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(NoticeRoutine(message, color));
    }

    private IEnumerator NoticeRoutine(string message, Color color)
    {
        if (noticeText != null)
        {
            noticeText.text  = message;
            noticeText.color = color;
        }

        noticeGroup.gameObject.SetActive(true);
        noticeGroup.alpha          = 0f;

        // 페이드 인
        yield return Fade(0f, 1f, noticeFadeIn);

        // 유지
        yield return new WaitForSeconds(noticeHold);

        // 페이드 아웃
        yield return Fade(1f, 0f, noticeFadeOut);

        noticeGroup.gameObject.SetActive(false);
        noticeRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            noticeGroup.alpha = to;
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            noticeGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        noticeGroup.alpha = to;
    }

    // ── 상황별 알림 ──────────────────────────────

    private void ShowStageStartNotice()
        => ShowNotice($"{currentWorld}-{currentStage} 시작", Color.white);

    private void ShowStageClearNotice()
        => ShowNotice($"{currentWorld}-{currentStage} 클리어!", new Color(1f, 0.85f, 0.2f));
    
    private void ShowBossNotice()
        => ShowNotice("보스 출현!", new Color(1f, 0.3f, 0.3f));   // 붉은색

    private void ShowStageFailNotice()
        => ShowNotice("스테이지 실패", new Color(1f, 0.35f, 0.35f));           // 붉은색

    /// <summary>알림이 끝난 뒤 다음 스테이지로 넘어감</summary>
    private IEnumerator NextStageDelayed()
    {
        yield return new WaitForSeconds(NoticeTotal);
        NextStage();
    }
}
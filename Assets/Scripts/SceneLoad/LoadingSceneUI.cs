using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// LoadingScene에 배치합니다.
/// SceneLoader.GoToStage() 대신 SceneLoader.GoToStageWithLoading()을 호출하면
/// 이 씬을 거쳐 StageScene으로 이동합니다.
/// </summary>
public class LoadingSceneUI : MonoBehaviour
{
    [Header("UI 참조")]
    [SerializeField] private Slider          progressBar;
    [SerializeField] private TextMeshProUGUI progressText;  // "Loading... 87%"
    [SerializeField] private TextMeshProUGUI tipText;       // 로딩 팁 (선택)

    [Header("로딩 팁 목록")]
    [SerializeField] private string[] tips = {
        "동료를 배치하면 전투가 훨씬 수월해집니다.",
        "가챠에서 전설 등급 동료를 노려보세요!",
        "스테이지가 높아질수록 적의 스탯이 강해집니다.",
        "치명타 확률을 높이면 데미지가 크게 오릅니다.",
    };

    void Start()
    {
        if (tipText != null && tips.Length > 0)
            tipText.text = tips[Random.Range(0, tips.Length)];
        // StartCoroutine(DelayTime());
        StartCoroutine(LoadStageScene());
    }

     // private IEnumerator DelayTime()
     // {
     //     yield return new WaitForSecondsRealtime(2);
     // }

    private IEnumerator LoadStageScene()
    {
        // allowSceneActivation = false 로 설정해 progress 0.9에서 대기
        AsyncOperation op = SceneManager.LoadSceneAsync(SceneLoader.STAGE_SCENE);
        op.allowSceneActivation = false;

        while (!op.isDone)
        {
            // Unity AsyncOperation은 0 ~ 0.9 까지만 progress를 올림
            float displayProgress = Mathf.Clamp01(op.progress / 0.9f);

            if (progressBar != null)
                progressBar.value = displayProgress;

            if (progressText != null)
                progressText.text = $"Loading... {Mathf.RoundToInt(displayProgress * 100)}%";

            // 로딩 완료 (0.9 도달) → 씬 활성화
            if (op.progress >= 0.9f)
            {
                if (progressBar != null)  progressBar.value   = 1f;
                if (progressText != null) progressText.text   = "Loading... 100%";

                // 한 프레임 대기 후 전환 (100% UI가 잠깐 보이도록)
                yield return null;
                op.allowSceneActivation = true;
            }
            yield return null;
        }
    }
}
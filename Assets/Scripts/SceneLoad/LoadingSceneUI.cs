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
        StartCoroutine(LoadStageScene());
    }

     private IEnumerator LoadStageScene()
     {
         AsyncOperation op = SceneManager.LoadSceneAsync(SceneLoader.STAGE_SCENE);
         op.allowSceneActivation = false;

         // 로딩 진행하면서 progress 표시
         while (op.progress < 0.9f)
         {
             float displayProgress = Mathf.Clamp01(op.progress / 0.9f);

             if (progressBar != null)
                 progressBar.value = displayProgress;

             if (progressText != null)
                 progressText.text = $"Loading... {Mathf.RoundToInt(displayProgress * 100)}%";

             yield return null;
         }

         // 로딩 완료 후 2초 고정 대기
         if (progressBar != null)  progressBar.value  = 1f;
         if (progressText != null) progressText.text  = "Loading... 100%";

         yield return new WaitForSecondsRealtime(2f);

         op.allowSceneActivation = true;
     }
}
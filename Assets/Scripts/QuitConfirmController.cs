using UnityEngine;

/// <summary>로그인 씬 종료 확인 패널: Escape 감지는 GameManager 가 하고 여기선 핸들러만 등록 — Escape 로 패널 열기/닫기, [예]면 종료.</summary>
// ⚠ 로그인 씬 Canvas 하위 항상 켜진 오브젝트에 붙일 것 — 패널 자신이면 꺼진 동안 OnEnable 이 안 불려 등록이 안 됨
public class QuitConfirmController : MonoBehaviour
{
    [Header("연결할 UI")]
    [SerializeField] private GameObject quitPanel;

    [Header("옵션")]
    [Tooltip("씬 전환(로딩)이 시작되면 true. 로딩 중 Escape를 막는다.")]
    [SerializeField] private bool blockInput = false;

    private bool isQuitting = false;

    private void Awake()
    {
        // 인스펙터에서 켜둔 채 저장하는 실수를 코드로 방어.
        if (quitPanel != null)
            quitPanel.SetActive(false);
        else
            Debug.LogError("[QuitConfirm] quitPanel이 연결되지 않았습니다.");
    }

    // 등록/해제는 OnEnable/OnDisable 짝으로 (씬 언로드 때 자동 정리)
    private void OnEnable()
    {
        GameManager.Instance?.RegisterEscapeHandler(OnEscape);
    }

    private void OnDisable()
    {
        GameManager.Instance?.UnregisterEscapeHandler(OnEscape);
    }

    /// <summary>GameManager 가 Escape 때 호출 — true = 내가 처리함(다른 핸들러로 안 넘김).</summary>
    private bool OnEscape()
    {
        if (isQuitting) return true;

        // 로딩 중엔 소비만 (false 면 다른 핸들러가 반응할 수 있음)
        if (blockInput) return true;

        if (quitPanel.activeSelf)
            CloseQuitPanel();   // 열려 있으면 Escape = "아니오"
        else
            OpenQuitPanel();

        return true;
    }

    // ───────────────────────── 패널 제어 ─────────────────────────

    private void OpenQuitPanel()
    {
        quitPanel.SetActive(true);
        GameLog.Info("[QuitConfirm] 종료 확인 패널 열림");
    }

    /// <summary>"아니오" 버튼 OnClick에 연결.</summary>
    public void CloseQuitPanel()
    {
        quitPanel.SetActive(false);
        GameLog.Info("[QuitConfirm] 종료 취소");
    }

    /// <summary>"예" 버튼 OnClick에 연결.</summary>
    public void OnClickQuit()
    {
        if (isQuitting) return;   // 연타 방지
        isQuitting = true;

        GameLog.Info("[QuitConfirm] 게임 종료");

#if UNITY_EDITOR
        // 에디터에서는 Application.Quit()이 아무 일도 하지 않는다.
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>로그인 버튼 → LoadSceneAsync 직전에 SetInputBlocked(true) 호출.</summary>
    public void SetInputBlocked(bool blocked)
    {
        blockInput = blocked;
        if (blocked && quitPanel != null && quitPanel.activeSelf)
            quitPanel.SetActive(false);
    }
}

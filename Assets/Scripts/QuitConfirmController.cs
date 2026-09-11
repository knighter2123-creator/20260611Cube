using UnityEngine;

/// <summary>
/// 로그인(타이틀) 씬 전용 - 종료 확인 패널.
///
/// [바뀐 점]
///   더 이상 스스로 Update()에서 키를 감시하지 않는다.
///   Escape 감지는 GameManager가 전담하고, 이 스크립트는
///   "로그인 씬에서 Escape가 눌리면 이렇게 해주세요"를 등록만 한다.
///
/// [붙이는 위치]
///   로그인 씬 Canvas 하위의 "항상 켜져 있는" 오브젝트.
///   ★ 종료 패널 자기 자신에 붙이면 안 된다.
///     패널이 꺼져 있으면 OnEnable이 안 불려서 핸들러 등록 자체가 안 된다.
/// </summary>
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

    // 등록/해제는 반드시 짝으로. OnEnable에서 등록, OnDisable에서 해제하면
    // 씬이 언로드될 때 자동으로 정리되므로 누수가 생기지 않는다.
    private void OnEnable()
    {
        GameManager.Instance?.RegisterEscapeHandler(OnEscape);
    }

    private void OnDisable()
    {
        GameManager.Instance?.UnregisterEscapeHandler(OnEscape);
    }

    /// <summary>
    /// Escape가 눌렸을 때 GameManager가 불러주는 함수.
    /// 반환값 true = "내가 처리했다"는 뜻이라 다른 핸들러로 넘어가지 않는다.
    /// </summary>
    private bool OnEscape()
    {
        if (isQuitting) return true;

        // 로딩 중이면 "소비만 하고 아무것도 안 함".
        // false를 반환하면 다른 핸들러가 대신 반응해버릴 수 있어서 true가 맞다.
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
        Debug.Log("[QuitConfirm] 종료 확인 패널 열림");
    }

    /// <summary>"아니오" 버튼 OnClick에 연결.</summary>
    public void CloseQuitPanel()
    {
        quitPanel.SetActive(false);
        Debug.Log("[QuitConfirm] 종료 취소");
    }

    /// <summary>"예" 버튼 OnClick에 연결.</summary>
    public void OnClickQuit()
    {
        if (isQuitting) return;   // 연타 방지
        isQuitting = true;

        Debug.Log("[QuitConfirm] 게임 종료");

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

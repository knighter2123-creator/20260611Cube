using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LoginScene 버튼 연결.
///
/// [왜 이 스크립트가 필요한가]
///   LoginScene의 버튼을 인스펙터에서 SceneLoader 오브젝트에 직접 드래그해 연결하면,
///   Login → Main → Login 으로 돌아왔을 때 버튼이 죽습니다.
///
///   돌아온 LoginScene은 SceneLoader를 새로 하나 만들지만,
///   DontDestroyOnLoad로 살아남은 기존 것이 이미 있으므로 새 것은 Awake에서 스스로를 지웁니다.
///   버튼의 인스펙터 참조는 그 "방금 지워진 새 오브젝트"를 가리키고 있어서,
///   눌러도 아무 일도 일어나지 않습니다. 에러조차 나지 않습니다.
///
///   이 스크립트는 인스펙터 참조 대신 SceneLoader.Instance(항상 살아 있는 쪽)를
///   코드로 찾아 연결하므로, 몇 번을 오가도 끊기지 않습니다.
///
/// [붙이는 위치]
///   LoginScene의 Canvas 또는 그 아래 아무 오브젝트.
///   씬에 속한 평범한 오브젝트면 되고, DontDestroyOnLoad를 걸면 안 됩니다.
///
/// [인스펙터 정리 — 이걸 안 하면 두 번 실행됩니다]
///   각 버튼의 On Click () 목록을 전부 비워주세요.
///   RemoveAllListeners()는 코드로 등록한 것만 지우고, 인스펙터에 저장된 항목은 못 지웁니다.
/// </summary>
public class LoginSceneUI : MonoBehaviour
{
    [Header("버튼")]
    [Tooltip("게임 시작 / 이어하기")]
    [SerializeField] private Button startButton;

    [Header("디버그")]
    [SerializeField] private bool logInteractions = true;

    private void Start()
    {
        // Start에서 연결하는 이유
        //   SceneLoader.Instance는 SceneLoader의 Awake에서 채워집니다.
        //   Awake끼리는 실행 순서가 보장되지 않으므로, 모든 Awake가 끝난 뒤인
        //   Start에서 접근해야 안전합니다.
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[LoginUI] SceneLoader.Instance 가 null 입니다. " +
                           "LoginScene에 SceneLoader가 있는지 확인하세요.", this);
            return;
        }

        BindButton(startButton, OnClickStart, "startButton");
    }

    /// <summary>
    /// 버튼 연결 공통 처리.
    /// RemoveAllListeners를 먼저 부르는 건, 씬을 다시 로드했을 때
    /// 이전 등록이 남아 중복 실행되는 걸 막기 위한 습관입니다.
    /// (이 스크립트는 씬과 함께 새로 생기므로 실제로 남아 있진 않지만,
    ///  같은 패턴을 어디서나 쓰는 게 실수를 줄입니다)
    /// </summary>
    private void BindButton(Button button, UnityEngine.Events.UnityAction action, string fieldName)
    {
        if (button == null)
        {
            // 선택 항목이므로 경고만 남기고 넘어갑니다.
            Debug.LogWarning($"[LoginUI] {fieldName} 이(가) 연결되지 않았습니다.", this);
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    // ───────────────────────── 버튼 처리 ─────────────────────────

    private void OnClickStart()
    {
        if (logInteractions) Debug.Log("[LoginUI] 게임 시작");

        // ★ 인스펙터 참조가 아니라 Instance를 통해 부르는 것이 핵심입니다.
        //   Instance는 항상 살아 있는 SceneLoader를 가리킵니다.
        SceneLoader.Instance?.GoToStageWithLoading();
    }
}

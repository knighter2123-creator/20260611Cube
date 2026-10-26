using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 클라우드 세이브 충돌 팝업 — "이 기기 데이터 / 클라우드 데이터" 중 어느 쪽으로 할지 플레이어가 고릅니다.
///
/// [붙이는 위치]  LoginScene 최상위의 CloudSaveConflictCanvas (항상 켜 둠). 창(panel)만 열고 닫을 때 켜고 끕니다.
///   처음 만들거나 모양을 다시 만들 때는 LoginScene 을 열고 Tools/UI 굽기/저장 데이터 선택 팝업 메뉴를 누르세요 (UIBaker) —
///   캔버스를 씬 최상위에 만들고 아래 칸을 모두 채웁니다. 그 뒤 씬을 저장하면 됩니다.
///   CloudSave 는 DontDestroyOnLoad 라 씬 오브젝트를 직접 연결할 수 없어서, 이 컴포넌트가 Instance 로 자신을 알립니다.
///
///   CloudSaveConflictCanvas (Canvas 3000, 이 컴포넌트)
///     Panel                      ← panel. 꺼 둠
///       Dim                      ← 뒤 화면 터치만 막음 (닫히지 않음)
///       Window
///         Title / Desc
///         이 기기  / Summary / Button / Label     ← localSummary, localButton, localLabel
///         클라우드 / Summary / Button / Label     ← cloudSummary, cloudButton, cloudLabel
///         Notice                 ← noticeText
///
/// [두 번 확인]  고르지 않은 쪽은 덮어써져 되돌릴 수 없으므로, 한 번 누르면 "정말 ~ 로?" 로 바뀌고 한 번 더 눌러야 확정합니다.
///   어두운 배경을 눌러도 닫히지 않습니다 — 고르지 않고 넘어가는 길이 없어야 합니다.
/// </summary>
public class CloudSaveConflictPopup : MonoBehaviour
{
    public static CloudSaveConflictPopup Instance { get; private set; }

    [Header("UI (Tools/UI 굽기/저장 데이터 선택 팝업 이 채움)")]
    [Tooltip("Dim + Window 를 묶은 오브젝트. 열고 닫을 때 켜고 끕니다.")]
    [SerializeField] private GameObject      panel;
    [SerializeField] private TextMeshProUGUI localSummary;
    [SerializeField] private TextMeshProUGUI cloudSummary;
    [SerializeField] private Button          localButton;
    [SerializeField] private Button          cloudButton;
    [SerializeField] private TextMeshProUGUI localLabel;
    [SerializeField] private TextMeshProUGUI cloudLabel;
    [SerializeField] private TextMeshProUGUI noticeText;

    [Header("굽기 설정 (UIBaker 가 UI 를 만들 때만 씀)")]
    [Tooltip("한글이 들어 있는 TMP 폰트. 비우면 굽기 메뉴가 씬의 다른 글자에서 찾아 씁니다.")]
    [SerializeField] private TMP_FontAsset font;

    public TMP_FontAsset Font => font;

    // 버튼 글자는 확인 문구에서 되돌릴 때도 쓰므로 여기 두고, UIBaker 도 같은 값으로 굽습니다
    public const string LocalButtonText = "이 기기 데이터 사용";
    public const string CloudButtonText = "클라우드 데이터 사용";

    private Action<bool> onChoose;     // true = 클라우드
    private int          armed = -1;   // 한 번 누른 쪽 (0 = 이 기기, 1 = 클라우드, -1 = 없음)
    private bool         ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        ready = panel != null && localSummary != null && cloudSummary != null && localButton != null &&
                cloudButton != null && localLabel != null && cloudLabel != null && noticeText != null;
        if (!ready)
        {
            Debug.LogError("[CloudSaveConflictPopup] UI 칸이 비어 있습니다. LoginScene 을 열고 Tools/UI 굽기/저장 데이터 선택 팝업 메뉴로 만든 뒤 씬을 저장하세요.", this);
            return;
        }

        Instance = this;

        // 버튼은 코드에서 연결합니다 (씬의 OnClick 칸은 비워 둠)
        localButton.onClick.AddListener(() => Choose(0));
        cloudButton.onClick.AddListener(() => Choose(1));
        panel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// 팝업을 띄웁니다. 고르면 onChoose(클라우드를 골랐는가) 가 한 번 불리고 팝업은 닫힙니다.
    /// 씬에 팝업이 없으면 false — 호출한 쪽이 고르지 않은 채로 넘어가는 방법을 정합니다 (CloudSave).
    /// </summary>
    public static bool TryShow(string localText, string cloudText, Action<bool> onChoose)
    {
        if (Instance == null || Instance.onChoose != null) return false;   // 없음 / 이미 떠 있음
        Instance.Open(localText, cloudText, onChoose);
        return true;
    }

    private void Open(string localText, string cloudText, Action<bool> callback)
    {
        onChoose = callback;
        armed    = -1;

        localSummary.text = localText;
        cloudSummary.text = cloudText;
        localLabel.text   = LocalButtonText;
        cloudLabel.text   = CloudButtonText;
        noticeText.text   = string.Empty;
        panel.SetActive(true);
    }

    private void Choose(int side)
    {
        if (onChoose == null) return;

        if (armed != side)
        {
            // 첫 번째 누름 — 확인 문구로 바꾸기만 함
            armed = side;
            localLabel.text = side == 0 ? "기기 데이터로 시작" : LocalButtonText;
            cloudLabel.text = side == 1 ? "클라우드 데이터로 시작" : CloudButtonText;
            noticeText.text = side == 0
                ? "<color=#FF8080>클라우드 데이터가 이 기기 데이터로 덮어써집니다.</color> 한 번 더 누르면 확정합니다."
                : "<color=#FF8080>이 기기 데이터가 클라우드 데이터로 덮어써집니다.</color> 한 번 더 누르면 확정합니다.";
            return;
        }

        Action<bool> callback = onChoose;
        onChoose = null;
        panel.SetActive(false);
        callback.Invoke(side == 1);
    }
}

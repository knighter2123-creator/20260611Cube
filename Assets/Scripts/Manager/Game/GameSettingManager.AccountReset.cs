using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameSettingManager 의 '계정 삭제' 전담 partial 파일.
/// 설정 패널 안의 [계정 삭제] 버튼 → 확인 팝업 → [삭제] 시 AccountReset 호출.
///
/// 이 파일은 버튼과 팝업만 다룬다. 실제로 무엇을 어떤 순서로 지우는지는 AccountReset.cs 의 책임이다.
/// (Bindings.cs 가 "UI 맞추기만" 하고 열기/닫기는 본체가 하는 것과 같은 분리)
///
/// [흐름]
///   [계정 삭제] ──→ 확인 팝업 표시
///                     ├─ [취소] / 뒤로가기 → 팝업만 닫힘 (설정 패널은 그대로)
///                     └─ [삭제]
///                          ├─ AccountReset.TryWipe()   실패 → 팝업에 안내, 아무것도 안 바뀜
///                          ├─ 패널 정리 (리스너 해제, 진동 취소, 블룸 요청 비우기)
///                          └─ AccountReset.RestartFromLogin() → LoginScene (첫 실행 상태)
/// </summary>
public partial class GameSettingManager
{
    [Header("계정 삭제")]
    [Tooltip("설정 패널 안의 '계정 삭제' 버튼. 비워 두면 기능만 꺼지고 에러는 나지 않습니다.")]
    [SerializeField] private Button deleteAccountButton;

    [Tooltip("'정말 삭제하시겠습니까?' 확인 팝업의 루트. ★ 설정 패널의 자식, 맨 아래(=맨 위에 그려짐)에 두세요.")]
    [SerializeField] private GameObject deleteConfirmPopup;

    [Tooltip("팝업 안의 [삭제] 버튼")]
    [SerializeField] private Button deleteConfirmButton;

    [Tooltip("팝업 안의 [취소] 버튼. 어두운 배경(Dim)에도 Button 을 달아 같은 버튼으로 쓰려면 아래 칸에 넣으세요.")]
    [SerializeField] private Button deleteCancelButton;

    [Tooltip("선택 - 팝업의 어두운 배경을 눌러도 취소되게 하려면 연결")]
    [SerializeField] private Button deleteDimButton;

    [Tooltip("선택 - '삭제 중...' / 실패 사유를 보여줄 텍스트 (팝업 안)")]
    [SerializeField] private TMP_Text deleteStatusText;

    private bool accountResetReady;

    /// <summary>확인 팝업이 떠 있는가. OnEscape 가 "팝업만 닫기" 를 판단할 때 쓴다.</summary>
    private bool IsDeleteConfirmOpen => deleteConfirmPopup != null && deleteConfirmPopup.activeSelf;

    // ─────────────────────────────────────────────
    // 초기화 (Start 에서 호출)
    // ─────────────────────────────────────────────

    private void SetupAccountReset()
    {
        // 씬에 켜 둔 채 저장했더라도 처음엔 항상 닫힌 상태로 시작 (Login_Name 의 namePopup 과 같은 이유)
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);

        if (deleteAccountButton == null)
        {
            Debug.LogWarning("[Setting] deleteAccountButton 미할당 — '계정 삭제' 가 비활성화됩니다.", this);
            return;
        }

        // ★ 확인 팝업이 없으면 버튼을 "막는다".
        //   되돌릴 수 없는 동작을 확인 없이 실행하는 경로는 아예 만들지 않는 것이 원칙입니다.
        //   (연결을 깜빡한 빌드가 나가도, 최악의 결과가 '버튼이 안 눌림' 이지 '한 번 터치로 계정 삭제' 가 아니게)
        accountResetReady = deleteConfirmPopup != null && deleteConfirmButton != null && deleteCancelButton != null;
        if (!accountResetReady)
        {
            Debug.LogError("[Setting] 계정 삭제 확인 팝업 연결이 빠졌습니다 " +
                           $"(popup={deleteConfirmPopup != null}, 삭제={deleteConfirmButton != null}, 취소={deleteCancelButton != null}). " +
                           "안전을 위해 '계정 삭제' 버튼을 비활성화합니다.", this);
            deleteAccountButton.interactable = false;
            return;
        }

        // 기존 버튼들과 같은 규칙: 코드가 리스너를 건다. 인스펙터 On Click 은 비워 둘 것.
        // (둘 다 걸려 있으면 한 번 누름에 두 번 처리 — 삭제 버튼에서는 특히 피해야 함)
        deleteAccountButton.onClick.RemoveAllListeners();
        deleteConfirmButton.onClick.RemoveAllListeners();
        deleteCancelButton.onClick.RemoveAllListeners();

        deleteAccountButton.onClick.AddListener(OnClickDeleteAccount);
        deleteConfirmButton.onClick.AddListener(OnClickConfirmDelete);
        deleteCancelButton.onClick.AddListener(HideDeleteConfirm);

        if (deleteDimButton != null)
        {
            deleteDimButton.onClick.RemoveAllListeners();
            deleteDimButton.onClick.AddListener(HideDeleteConfirm);
        }
    }

    // ─────────────────────────────────────────────
    // 팝업 열기 / 닫기
    // ─────────────────────────────────────────────

    private void OnClickDeleteAccount()
    {
        if (!accountResetReady || !IsOpen) return;
        if (AccountReset.IsRunning) return;

        LogWhoClicked("계정 삭제 확인 팝업 열기");

        deleteConfirmPopup.SetActive(true);
        SetDeleteStatus(string.Empty);
        SetDeleteButtonsInteractable(true);   // 이전에 실패해서 꺼 둔 적이 있을 수 있으므로 매번 되살림
    }

    /// <summary>팝업만 닫는다. 설정 패널은 그대로 둔다. (Close() 와 OnEscape 에서도 부름)</summary>
    private void HideDeleteConfirm()
    {
        if (AccountReset.IsRunning) return;   // 삭제가 시작된 뒤에는 팝업을 유지 ('삭제 중...' 표시)
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);
    }

    // ─────────────────────────────────────────────
    // 삭제 실행
    // ─────────────────────────────────────────────

    private void OnClickConfirmDelete()
    {
        if (AccountReset.IsRunning) return;

        LogWhoClicked("계정 삭제 실행");

        // 연타 방지 — 첫 누름에서 바로 막는다
        SetDeleteButtonsInteractable(false);
        SetDeleteStatus("삭제 중...");

        // ① 지우기 — 실패할 수 있는 일을 먼저. 실패하면 게임은 아무 일 없던 것처럼 계속된다.
        if (!AccountReset.TryWipe(LOGIN_SCENE, out string error))
        {
            SetDeleteStatus(error);
            SetDeleteButtonsInteractable(true);
            return;
        }

        // ② 여기부터는 되돌릴 수 없다. 패널 정리 — ReturnToLogin() 과 같은 이유로 같은 것들을 정리한다.
        //
        //   ★ ReturnToLogin 과 다른 점 두 가지
        //   - SaveManager.Save() 를 부르지 않는다.
        //       방금 지운 데이터를 도로 저장하는 꼴이다. (어차피 잠겨 있어 무시되지만, 의도가 반대라 적지 않는다)
        //   - LevelUpManager.ResetStat() 을 부르지 않는다.
        //       ReturnToLogin 에서는 LevelUpManager 가 LoginScene 까지 살아남으므로 MainScene 의 스탯 참조를 끊어야 한다.
        //       계정 삭제에서는 LevelUpManager 자체가 파괴되고 새로 만들어지므로 끊을 대상이 없다.
        //
        //   ★ [검토 후 수정] try / finally
        //     TryWipe 가 성공한 순간 파일은 지워졌고 저장은 잠겼습니다. 저장 잠금을 푸는 건
        //     RestartFromLogin() 이 만드는 새 SaveManager 뿐입니다.
        //     아래 정리 코드에서 예외가 하나라도 나면 RestartFromLogin() 까지 도달하지 못하고,
        //     게임은 아무 일 없던 것처럼 계속되는데 "다시는 저장되지 않는" 상태가 됩니다. (가장 찾기 힘든 종류)
        //     finally 는 try 안에서 예외가 나도 반드시 실행되므로, 정리가 실패해도 초기화는 끝까지 갑니다.
        //     (정리가 실패하면 씬이 내려갈 때 OnDisable 이 같은 정리를 한 번 더 시도합니다)
        try
        {
            if (IsOpen)
            {
                UnbindAll();      // 슬라이더·토글 리스너 해제 — 등록과 해제는 항상 짝으로
                IsOpen = false;   // 씬이 내려갈 때 OnDisable 이 같은 정리(UnbindAll, Bloom Pop)를 한 번 더 하지 않게
                                  // (ReturnToLogin 과 같은 처리)
            }

            HapticManager.Instance?.Cancel();
            BloomController.Instance?.ClearRequests();   // refCount 를 0 으로 — Pop 을 따로 안 해도 됨
        }
        finally
        {
            // ③ 새로 시작 — 씬을 내리고 매니저를 전부 새로 만든 뒤 LoginScene 으로
            AccountReset.RestartFromLogin();
        }
    }

    // ─────────────────────────────────────────────
    // 작은 도우미
    // ─────────────────────────────────────────────

    private void SetDeleteButtonsInteractable(bool on)
    {
        if (deleteConfirmButton != null) deleteConfirmButton.interactable = on;
        if (deleteCancelButton  != null) deleteCancelButton.interactable  = on;
        if (deleteDimButton     != null) deleteDimButton.interactable     = on;
    }

    private void SetDeleteStatus(string message)
    {
        if (deleteStatusText == null) return;
        deleteStatusText.text = message;
        deleteStatusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }
}
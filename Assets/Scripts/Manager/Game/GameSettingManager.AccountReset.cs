using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>설정 패널의 계정 삭제 버튼·확인 팝업(무엇을 지우는지는 AccountReset 책임): [삭제] → 서버 데이터 삭제 → AccountReset.TryWipe → 패널 정리 → RestartFromLogin.</summary>
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

    // 서버 기록(랭킹·닉네임) 삭제를 기다리는 중 — 이 동안 팝업을 닫거나 다시 누를 수 없음
    private bool serverDeleteInProgress;

    // 서버 데이터 삭제(탈퇴)는 끝났는데 로컬 지우기가 실패한 상태 — 재시도 때 서버 단계를 건너뜀 (탈퇴 후엔 본인 로그인 확인을 통과할 수 없어서)
    private bool serverDataDeleted;

    /// <summary>확인 팝업이 떠 있는가. OnEscape 가 "팝업만 닫기" 를 판단할 때 쓴다.</summary>
    private bool IsDeleteConfirmOpen => deleteConfirmPopup != null && deleteConfirmPopup.activeSelf;

    // ── 초기화 (Start 에서 호출) ──

    private void SetupAccountReset()
    {
        // 씬에 켜 둔 채 저장했더라도 처음엔 항상 닫힌 상태로 시작 (Login_Name 의 namePopup 과 같은 이유)
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);

        if (deleteAccountButton == null)
        {
            Debug.LogWarning("[Setting] deleteAccountButton 미할당 — '계정 삭제' 가 비활성화됩니다.", this);
            return;
        }

        // ★ 확인 팝업 연결이 빠지면 버튼을 막음 — 되돌릴 수 없는 동작을 확인 없이 실행하는 경로를 만들지 않음
        accountResetReady = deleteConfirmPopup != null && deleteConfirmButton != null && deleteCancelButton != null;
        if (!accountResetReady)
        {
            Debug.LogError("[Setting] 계정 삭제 확인 팝업 연결이 빠졌습니다 " +
                           $"(popup={deleteConfirmPopup != null}, 삭제={deleteConfirmButton != null}, 취소={deleteCancelButton != null}). " +
                           "안전을 위해 '계정 삭제' 버튼을 비활성화합니다.", this);
            deleteAccountButton.interactable = false;
            return;
        }

        // 리스너는 코드가 검 — 인스펙터 On Click 은 비울 것 (둘 다면 한 번에 두 번 처리)
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

    // ── 팝업 열기 / 닫기 ──

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
        if (AccountReset.IsRunning || serverDeleteInProgress) return;   // 삭제가 시작된 뒤에는 팝업을 유지 ('삭제 중...' 표시)
        if (deleteConfirmPopup != null) deleteConfirmPopup.SetActive(false);
    }

    // ── 삭제 실행 ──

    private void OnClickConfirmDelete()
    {
        if (AccountReset.IsRunning || serverDeleteInProgress) return;

        LogWhoClicked("계정 삭제 실행");

        // 연타 방지 — 첫 누름에서 바로 막는다
        SetDeleteButtonsInteractable(false);
        SetDeleteStatus("삭제 중...");

        // 서버 쪽은 이미 지웠고 로컬만 실패했던 재시도 — 탈퇴로 로그아웃돼 서버 삭제는 다시 할 수 없으므로 로컬만
        if (serverDataDeleted)
        {
            WipeLocalAndRestart();
            return;
        }

        // ⓪ 서버 데이터 삭제를 로컬보다 먼저(실패 가능) — 실패하면 로컬도 안 지움, 게스트면 바로 성공
        serverDeleteInProgress = true;
        ServerAccountData.DeleteForCurrentAccount((ok, serverError) =>
        {
            if (this == null) return;   // 기다리는 사이 씬이 바뀜
            serverDeleteInProgress = false;

            if (!ok)
            {
                SetDeleteStatus(serverError);
                SetDeleteButtonsInteractable(true);
                return;
            }

            serverDataDeleted = true;
            WipeLocalAndRestart();
        });
    }

    /// <summary>서버 기록 삭제가 끝난 뒤 — 로컬 진행 데이터를 지우고 LoginScene 부터 다시 시작.</summary>
    private void WipeLocalAndRestart()
    {
        // ① 로컬 지우기 (실패하면 게임은 그대로 계속)
        if (!AccountReset.TryWipe(LOGIN_SCENE, out string error))
        {
            // 서버 계정은 이미 탈퇴(로그아웃)돼 다시 올릴 곳이 없음 — [삭제]를 다시 누르면 로컬 지우기만 재시도 (serverDataDeleted)
            SetDeleteStatus(error);
            SetDeleteButtonsInteractable(true);
            return;
        }

        // ② 되돌릴 수 없는 구간 — ReturnToLogin 과 같은 정리 (Save·ResetStat 은 안 함: 지운 데이터 저장 금지, 매니저째 재생성)
        // ★ try/finally — 정리 중 예외로 RestartFromLogin 을 못 가면 저장이 잠긴 채 '다시는 저장 안 되는' 상태가 되므로 반드시 진행
        try
        {
            if (IsOpen)
            {
                UnbindAll();      // 슬라이더·토글 리스너 해제 — 등록과 해제는 항상 짝으로
                IsOpen = false;   // OnDisable 이 같은 정리를 한 번 더 하지 않게
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

    // ── 작은 도우미 ──

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
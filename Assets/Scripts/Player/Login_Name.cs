using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// LoginScene 의 "게임 시작" 버튼 처리.
///
/// 흐름 (이름 확인은 LoadingScene 으로 가기 "전"에 끝남)
///   [게임 시작] ─┬─ 이름 있음 → LoadingScene → MainScene
///                └─ 이름 없음 → 이름 입력 팝업
///                                 ├─ [확인] 검사 통과 → 등록 → LoadingScene → MainScene
///                                 │         검사 실패 → 팝업에 오류 문구
///                                 └─ [취소] 팝업 닫기
///
/// ★ 시작 버튼의 인스펙터 OnClick 목록은 비워 두세요. 리스너는 코드(Start)에서 등록합니다.
///   예전 PlayToMain 이 OnClick 에 연결돼 있었다면, 이름 확인을 건너뛰고 로딩으로 가는 구멍이 됩니다.
///
/// ★ 이 스크립트는 팝업(namePopup) "밖"의 항상 켜진 오브젝트에 붙이세요.
///   팝업 안에 붙이면 namePopup.SetActive(false) 때 자기 자신도 꺼져서 버튼이 반응하지 않습니다.
/// </summary>
public class Login_Name : MonoBehaviour
{
    [Header("시작 버튼")]
    // 필드 이름을 submitButton → startButton 으로 바꿨습니다.
    // FormerlySerializedAs 가 있으면 인스펙터에 연결해 둔 기존 버튼 참조가 끊기지 않습니다.
    [FormerlySerializedAs("submitButton")]
    [SerializeField] private Button startButton;

    [Header("이름 입력 팝업")]
    [SerializeField] private GameObject     namePopup;      // 팝업 루트 (시작 시 자동으로 꺼짐)
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button         confirmButton;
    [SerializeField] private Button         cancelButton;   // 없으면 비워둬도 됨
    [SerializeField] private TMP_Text       errorText;      // 없으면 비워둬도 됨
    [SerializeField] private TMP_Text       guideText;      // 없으면 비워둬도 됨

    // 로딩을 시작했는지. 버튼 연타로 씬 로딩이 두 번 호출되는 것을 막습니다.
    private bool isLoading;

    private void Awake()
    {
        // 팝업은 처음에 항상 닫힌 상태로 시작 (씬에서 켜둔 채 저장해도 안전)
        if (namePopup != null) namePopup.SetActive(false);
    }

    private void Start()
    {
        // 예전 PlayerPrefs 에 이름이 있던 유저 → SaveData 로 1회 이관
        // (SaveManager.Awake 가 먼저 끝나 있으므로 Start 에서 호출하면 안전)
        PlayerProfile.MigrateLegacyIfNeeded();

        if (startButton   != null) startButton.onClick.AddListener(OnClickStart);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.AddListener(ClosePopup);

        if (nameInput != null)
        {
            // 글자 수 제한을 PlayerProfile 의 상수와 맞춤 → 숫자가 두 곳에 따로 적히지 않음
            nameInput.characterLimit = PlayerProfile.MAX_LENGTH;
            nameInput.onValueChanged.AddListener(OnInputChanged);
            nameInput.onSubmit.AddListener(OnInputSubmit);   // 키보드의 Enter / 완료 키
        }

        if (guideText != null)
            guideText.text =
                $"{PlayerProfile.MIN_LENGTH}~{PlayerProfile.MAX_LENGTH}자 (한글·영문·숫자)\n" +
                "<color=#FF8080>정한 이름은 이후 변경할 수 없습니다.</color>";
    }

    private void OnDestroy()
    {
        // 이름 있는 메서드로 구독했기 때문에 -= / RemoveListener 로 정확히 해제할 수 있습니다.
        if (startButton   != null) startButton.onClick.RemoveListener(OnClickStart);
        if (confirmButton != null) confirmButton.onClick.RemoveListener(OnClickConfirm);
        if (cancelButton  != null) cancelButton.onClick.RemoveListener(ClosePopup);
        if (nameInput != null)
        {
            nameInput.onValueChanged.RemoveListener(OnInputChanged);
            nameInput.onSubmit.RemoveListener(OnInputSubmit);
        }
    }

    // ───────── 버튼 ─────────

    /// <summary>게임 시작 버튼. (Start 에서 코드로 연결됨 — 인스펙터에 또 연결하면 두 번 호출됨)</summary>
    private void OnClickStart()
    {
        if (isLoading) return;

        if (PlayerProfile.HasName)
            PlayToMain();          // 이미 이름이 있으면 팝업 없이 바로 시작
        else
            OpenPopup();
    }

    private void OnClickConfirm()
    {
        if (isLoading) return;

        string raw = nameInput != null ? nameInput.text : string.Empty;

        if (!PlayerProfile.TryRegister(raw, out string error))
        {
            ShowError(error);
            return;
        }

        ClosePopup();
        PlayToMain();
    }

    private void OnInputSubmit(string _) => OnClickConfirm();

    // 다시 입력하기 시작하면 이전 오류 문구를 지움
    private void OnInputChanged(string _) => ShowError(string.Empty);

    // ───────── 팝업 ─────────

    private void OpenPopup()
    {
        if (namePopup == null)
        {
            Debug.LogError("[Login_Name] namePopup 이 연결되지 않았습니다.");
            return;
        }

        namePopup.SetActive(true);
        ShowError(string.Empty);

        if (nameInput != null)
        {
            nameInput.text = string.Empty;
            // SetActive(true) 직후에 바로 활성화하면 무시될 수 있어 한 프레임 뒤에 처리
            StartCoroutine(FocusInputNextFrame());
        }
    }

    private IEnumerator FocusInputNextFrame()
    {
        yield return null;   // 한 프레임 대기
        if (nameInput != null && IsPopupOpen)
            nameInput.ActivateInputField();   // 모바일: 가상 키보드가 올라옴
    }

    /// <summary>팝업 닫기. 안드로이드 뒤로가기 핸들러에 연결할 때도 이 함수를 쓰면 됩니다.</summary>
    public void ClosePopup()
    {
        if (namePopup != null) namePopup.SetActive(false);
    }

    public bool IsPopupOpen => namePopup != null && namePopup.activeSelf;

    private void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(!string.IsNullOrEmpty(message));
    }

    // ───────── 씬 이동 (기존 코드 유지) ─────────

    // private 인 이유: 이름 확인(OnClickStart / OnClickConfirm)을 거치지 않고는 호출될 수 없게
    private void PlayToMain()
    {
        isLoading = true;
        if (startButton != null) startButton.interactable = false;   // 연타 방지 (시각적으로도)

        if (SceneLoader.Instance == null)
        {
            GameObject obj = new GameObject("SceneLoader");
            obj.AddComponent<SceneLoader>();
        }

        SceneLoader.Instance.GoToStageWithLoading();   // LoadingScene → MainScene
    }

#if UNITY_EDITOR
    // 인스펙터에서 컴포넌트 우클릭 → 메뉴로 실행 (플레이 모드에서)
    [ContextMenu("테스트: 저장된 이름 지우기")]
    private void DebugClearName() => PlayerProfile.DebugClear();
#endif
}
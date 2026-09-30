using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;   // ★ 신규: 클릭/터치 감지(IPointerClickHandler)에 필요


/// <summary>
/// MainScene 네임플레이트. 이름 표시 + (선택) 클릭 시 닉네임 팝업 열기.
///
/// ★ [닉네임 인게임 이동] 이번 변경
///   이제 이름 없이 MainScene 을 플레이하는 것이 정상 흐름이라, 이름 없을 때 표시가 자주 보입니다.
///   자동 팝업(NicknamePrompt)을 '나중에' 로 닫은 유저가 언제든 이름을 정할 수 있도록
///   네임플레이트 클릭 → NicknameChangePanel.Open() 을 (선택 연결로) 넣었습니다.
///   패널이 이름 유무를 보고 '최초 설정(무료)' / '변경(보석)' 을 스스로 고르므로 여기서는 Open() 만 부릅니다.
///   namePanel 을 비워 두면 예전과 똑같이 표시만 합니다.
///   (이미 다른 방법 — 예: Button 의 On Click() — 으로 패널을 열고 있다면 둘 중 하나만 쓰세요.
///    둘 다 있어도 Open() 이 두 번째 호출을 무시하므로 고장은 나지 않습니다)
/// </summary>
public class PlayerName : MonoBehaviour, IPointerClickHandler
{
    // 이름이 없을 때 보여줄 값 (이제는 "특정 스테이지 전 신규 유저" 의 정상 상태)
    private const string DefaultName = "플레이어";

    [Header("닉네임 표시")]
    [SerializeField] private TMP_Text nickNameText;   // "유저 : OOO" 텍스트

    [Tooltip("{0} 자리에 이름이 들어갑니다. 예: \"유저 : {0}\"")]
    [SerializeField] private string format = "{0}";

    [Header("클릭 (선택)")]
    [Tooltip("네임플레이트를 누르면 열 닉네임 팝업. 비워 두면 클릭해도 아무 일도 없습니다.\n" +
             "⚠ 클릭을 받으려면 nickNameText(또는 이 오브젝트의 Image)의 Raycast Target 이 켜져 있어야 합니다.")]
    [SerializeField] private NicknameChangePanel namePanel;

    // IPointerClickHandler: EventSystem 이 이 오브젝트(또는 자식 그래픽)에서 클릭/터치를 감지하면 호출
    public void OnPointerClick(PointerEventData eventData)
    {
        if (namePanel != null) namePanel.Open();
    }

    private void OnEnable()
    {
        PlayerProfile.OnNameRegistered += HandleNameRegistered;
        PlayerProfile.OnNameChanged    += HandleNameRegistered;   // ★ 신규: 개명 시에도 같은 방식으로 새로고침
        Refresh();   // 켜질 때마다 새로 읽음 → UI 가 이름 사본을 들고 있지 않음
    }

    private void OnDisable()
    {
        // 람다가 아닌 이름 있는 메서드라 정확히 해제됨
        PlayerProfile.OnNameRegistered -= HandleNameRegistered;
        PlayerProfile.OnNameChanged    -= HandleNameRegistered;   // ★ 신규
    }

    private void HandleNameRegistered(string _) => Refresh();

    public void Refresh()
    {
        if (nickNameText == null) return;

        // 변수 이름을 name 으로 하면 MonoBehaviour 의 name(오브젝트 이름)과 헷갈리므로 다른 이름 사용
        string displayName = PlayerProfile.HasName ? PlayerProfile.Name : DefaultName;
        try
        {
            nickNameText.text = string.Format(format, displayName);
        }
        catch (System.FormatException)
        {
            // format 에 "{" 만 있거나 "{1}" 처럼 잘못 적으면 string.Format 이 예외를 던집니다.
            // 인스펙터 오타 하나로 화면 갱신이 멈추지 않게 이름만 표시하고 경고를 남깁니다.
            nickNameText.text = displayName;
            Debug.LogWarning($"[PlayerName] format 문자열이 잘못됐습니다: '{format}' (예: \"유저 : {{0}}\")", this);
        }
    }
}
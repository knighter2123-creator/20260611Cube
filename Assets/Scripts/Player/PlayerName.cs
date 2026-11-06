using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;   // 클릭/터치 감지(IPointerClickHandler)
/// <summary>MainScene 네임플레이트: 이름(없으면 익명) 표시 + 클릭 시 닉네임 패널 Open (모드는 패널이 정함, namePanel 이 비면 표시만).</summary>
public class PlayerName : MonoBehaviour, IPointerClickHandler
{
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
        PlayerProfile.OnNameRegistered += HandleNameUpdated;
        PlayerProfile.OnNameChanged    += HandleNameUpdated;
        Refresh();   // 켜질 때마다 새로 읽음 → UI 가 이름 사본을 들고 있지 않음
    }

    private void OnDisable()
    {
        // 람다가 아닌 이름 있는 메서드라 정확히 해제됨
        PlayerProfile.OnNameRegistered -= HandleNameUpdated;
        PlayerProfile.OnNameChanged    -= HandleNameUpdated;
    }

    // 최초 등록 · 개명 모두 같은 방식으로 새로고침
    private void HandleNameUpdated(string _) => Refresh();

    public void Refresh()
    {
        if (nickNameText == null) return;

        // 이름이 없으면 익명 이름 (변수명은 MonoBehaviour.name 과 헷갈리지 않게)
        string displayName = PlayerProfile.DisplayName;
        try
        {
            nickNameText.text = string.Format(format, displayName);
        }
        catch (System.FormatException)
        {
            // 잘못된 format(인스펙터 오타)이면 예외로 갱신이 멈추지 않게 이름만 표시하고 경고
            nickNameText.text = displayName;
            Debug.LogWarning($"[PlayerName] format 문자열이 잘못됐습니다: '{format}' (예: \"유저 : {{0}}\")", this);
        }
    }
}
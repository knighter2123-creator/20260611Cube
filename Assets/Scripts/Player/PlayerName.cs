using TMPro;
using UnityEngine;

/// <summary>
/// MainScene 의 닉네임 "표시 전용" 컴포넌트. (기존 PlayerName.cs 를 덮어쓰기)
///
/// 바뀐 점
///   - 이름 변경 팝업·버튼 기능을 전부 제거했습니다. 이름은 LoginScene 에서 한 번만 정합니다.
///   - PlayerPrefs 대신 PlayerProfile(= SaveData.playerName)에서 읽습니다.
///   - nickNameText 필드 이름은 그대로라 인스펙터 연결이 유지됩니다.
///     (지운 필드들 — 버튼·팝업 — 의 연결값은 유니티가 알아서 무시합니다)
/// </summary>
public class PlayerName : MonoBehaviour
{
    // 이름이 없을 때(에디터에서 MainScene 을 바로 실행한 경우 등) 보여줄 값
    private const string DefaultName = "플레이어";

    [Header("닉네임 표시")]
    [SerializeField] private TMP_Text nickNameText;   // "유저 : OOO" 텍스트

    [Tooltip("{0} 자리에 이름이 들어갑니다. 예: \"유저 : {0}\"")]
    [SerializeField] private string format = "{0}";

    private void OnEnable()
    {
        PlayerProfile.OnNameRegistered += HandleNameRegistered;
        Refresh();   // 켜질 때마다 새로 읽음 → UI 가 이름 사본을 들고 있지 않음
    }

    private void OnDisable()
    {
        // 람다가 아닌 이름 있는 메서드라 정확히 해제됨
        PlayerProfile.OnNameRegistered -= HandleNameRegistered;
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
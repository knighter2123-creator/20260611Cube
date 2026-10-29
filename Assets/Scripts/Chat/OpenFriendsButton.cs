using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 친구 창을 여는 버튼 (StageScene 의 랭킹 버튼 옆). 받은 친구 요청이 있으면 글자 옆에 개수를 붙입니다.
/// Tools/UI 굽기/채팅 탭·친구 창 이 랭킹 버튼을 복제해 만들고 칸을 채웁니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpenFriendsButton : MonoBehaviour
{
    [SerializeField] private FriendPanel friendPanel;
    [Tooltip("버튼 글자 (비워 두면 개수 표시 안 함)")]
    [SerializeField] private TextMeshProUGUI label;

    private const string Text      = "친구";
    private const string BadgeHex  = "#FF7B7B";

    private int shownCount = -1;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (friendPanel != null) friendPanel.Open();
        });
    }

    private void Update()
    {
        if (label == null) return;

        ChatManager chat = ChatManager.Instance;
        int incoming = chat != null ? chat.CountFriends(FriendState.Incoming) : 0;
        if (incoming == shownCount) return;

        shownCount = incoming;
        label.text = incoming > 0 ? $"{Text} <color={BadgeHex}>{incoming}</color>" : Text;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>친구 창 열기 버튼 — 받은 요청이 있으면 글자 옆에 개수 (Tools/UI 굽기/채팅 탭·친구 창 이 만듦).</summary>
[RequireComponent(typeof(Button))]
public class OpenFriendsButton : MonoBehaviour
{
    [SerializeField] private FriendPanel friendPanel;
    [Tooltip("버튼 글자 (비워 두면 개수 표시 안 함)")]
    [SerializeField] private TextMeshProUGUI label;

    [Tooltip("버튼 글자. 받은 친구 요청이 있으면 뒤에 개수가 붙습니다.")]
    [SerializeField] private string text = "친구";
    [Tooltip("받은 친구 요청 개수 색")]
    [SerializeField] private Color countColor = ChatText.UnreadColor;

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
        label.text = incoming > 0 ? $"{text} {ChatText.Colored(countColor, incoming.ToString())}" : text;
    }
}

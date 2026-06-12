using TMPro;
using UnityEngine;

public class PlayerName : MonoBehaviour
{
    [SerializeField] private TMP_Text nickNameText;

    void Start()
    {
        Refresh();
    }

    public void Refresh()
    {
        string savedName = PlayerPrefs.GetString("이름 : " + "PlayerName", "이름 : " + "플레이어");
        if (nickNameText != null)
            nickNameText.text = savedName;
    }
}
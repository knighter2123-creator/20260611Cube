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
        // ✅ 저장 키 "PlayerName"과 일치 + 표시 형식 분리
        string savedName = PlayerPrefs.GetString("PlayerName", "플레이어");
        if (nickNameText != null)
            nickNameText.text = "유저 : " + savedName;
    }
}
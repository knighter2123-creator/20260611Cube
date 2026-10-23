using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 랭킹 창의 한 줄 — 순위 / 닉네임 / 스테이지. 머리줄도 같은 모양을 씁니다.
/// RankingPanel 이 목록을 그릴 때 rowTemplate 을 복제해 Set() 으로 채웁니다.
/// (UI 는 Tools/UI 굽기/랭킹 창 메뉴가 만들고 아래 칸을 채웁니다 — UIBaker)
/// </summary>
public class RankingRow : MonoBehaviour
{
    [SerializeField] private Image           background;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI stageText;

    public void Set(string rank, string nickname, string stage, Color bg, Color textColor)
    {
        if (background != null) background.color = bg;
        SetCell(rankText,  rank,     textColor);
        SetCell(nameText,  nickname, textColor);
        SetCell(stageText, stage,    textColor);
    }

    private static void SetCell(TextMeshProUGUI t, string value, Color color)
    {
        if (t == null) return;
        t.text  = value;
        t.color = color;
    }
}

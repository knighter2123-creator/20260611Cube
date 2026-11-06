using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>랭킹 한 줄(순위/닉네임/스테이지): 처음 Set 때 템플릿의 평소 색을 기억하고, 내 줄은 배경·상위 순위는 글자를 강조 색으로 칠함.</summary>
// 색은 Row 템플릿(RankingCanvas > … > Content > Row)에서 고치면 모든 줄이 따라감
public class RankingRow : MonoBehaviour
{
    [SerializeField] private Image           background;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("강조 색")]
    [Tooltip("내 기록 줄의 배경색")]
    [SerializeField] private Color myRowBackground = RankingPanel.MyRowColor;
    [Tooltip("상위 순위 줄의 글자 색 (몇 위까지인지는 RankingPanel 의 Top Highlight)")]
    [SerializeField] private Color topTextColor = RankingPanel.GoldColor;

    // 하이어라키에서 정한 평소 색 — 처음 Set 할 때 읽어 둠 (복제본은 템플릿 색으로 태어남)
    private bool  captured;
    private Color normalBackground;
    private Color normalRank, normalName, normalStage;

    public void Set(string rank, string nickname, string stage, bool mine, bool top)
    {
        Capture();
        if (background != null) background.color = mine ? myRowBackground : normalBackground;
        SetCell(rankText,  rank,     top ? topTextColor : normalRank);
        SetCell(nameText,  nickname, top ? topTextColor : normalName);
        SetCell(stageText, stage,    top ? topTextColor : normalStage);
    }

    private void Capture()
    {
        if (captured) return;
        captured = true;
        normalBackground = background != null ? background.color : Color.clear;
        normalRank  = rankText  != null ? rankText.color  : Color.white;
        normalName  = nameText  != null ? nameText.color  : Color.white;
        normalStage = stageText != null ? stageText.color : Color.white;
    }

    private static void SetCell(TextMeshProUGUI t, string value, Color color)
    {
        if (t == null) return;
        t.text  = value;
        t.color = color;
    }
}

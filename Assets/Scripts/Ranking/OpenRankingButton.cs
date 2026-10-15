using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenRankingButton : MonoBehaviour
{
    [SerializeField] private RankingPanel rankingPanel;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (rankingPanel != null) rankingPanel.Open();
        });
    }
}

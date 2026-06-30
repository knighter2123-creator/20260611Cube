using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GachaUIManager : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject gachaPanel;
    [SerializeField] private GameObject resultPanel;

    [Header("뽑기 버튼")]
    [SerializeField] private Button draw1Button;
    [SerializeField] private Button draw10Button;
    [SerializeField] private Button draw100Button;

    [Header("이동 버튼")]
    [SerializeField] private Button closeResultButton;

    [Header("뽑기 결과")]
    [SerializeField] private Transform  resultContent;
    [SerializeField] private GameObject resultItemPrefab;

    void Start()
    {
        // ── 뽑기 버튼 ──────────────────────────────
        draw1Button.onClick.AddListener(()   => OnDraw(GachaSystem.Instance.DrawOne()));
        draw10Button.onClick.AddListener(()  => OnDraw(GachaSystem.Instance.DrawTen()));
        draw100Button.onClick.AddListener(() => OnDraw(GachaSystem.Instance.DrawHundred()));

        // ── 이동 버튼 ──────────────────────────────
        closeResultButton.onClick.AddListener(() => ShowGachaPanel());

        // ── 초기 상태 ──────────────────────────────
        ShowGachaPanel();
    }

    private void ShowGachaPanel()
    {
        gachaPanel.SetActive(true);
        resultPanel.SetActive(false);
    }

    private void ShowResultPanel()
    {
        gachaPanel.SetActive(false);
        resultPanel.SetActive(true);
    }

    private void OnDraw(List<GachaSystem.GachaResult> results)
    {
        if (results.Count == 0) return;

        foreach (Transform child in resultContent)
            Destroy(child.gameObject);

        foreach (GachaSystem.GachaResult result in results)
        {
            GameObject item = Instantiate(resultItemPrefab, resultContent);
            GachaResultItem ui = item.GetComponent<GachaResultItem>();
            ui?.Setup(result);
        }

        ShowResultPanel();
    }
}
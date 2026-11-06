using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>StageScene 배속 토글 버튼 — Button.OnClick 에 OnSpeedButtonClicked 를 연결.</summary>
public class GameSpeedButton : MonoBehaviour
{
    [Header("버튼 텍스트 (TextMeshPro)")]
    [SerializeField] private TextMeshProUGUI label;

    [Header("배속별 표시 텍스트")]
    [SerializeField] private string[] speedLabels = { "1x", "2x", "3x" };

    void Start()
    {
        RefreshLabel();
    }

    /// <summary>버튼 OnClick에 연결하세요.</summary>
    public void OnSpeedButtonClicked()
    {
        GameSpeedManager.Instance?.CycleSpeed();
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (label == null || GameSpeedManager.Instance == null) return;

        float current = GameSpeedManager.Instance.CurrentSpeed;

        // 현재 속도로 레이블 인덱스를 찾아 표시 (speedLabels 순서 = speedSteps 순서 가정)
        int index = System.Array.FindIndex(
            new float[] { 1f, 2f, 3f },
            s => Mathf.Approximately(s, current)
        );

        label.text = (index >= 0 && index < speedLabels.Length)
            ? speedLabels[index]
            : $"{current}x";
    }
}

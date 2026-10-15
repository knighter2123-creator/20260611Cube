using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// StageScene의 배속 토글 버튼을 제어합니다.
/// Button 컴포넌트의 OnClick에 OnSpeedButtonClicked()를 연결하세요.
/// </summary>
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

        // speedLabels 배열 순서가 speedSteps와 같다고 가정
        // 현재 속도 값으로 레이블 인덱스를 찾아 표시
        int index = System.Array.FindIndex(
            new float[] { 1f, 2f, 3f },
            s => Mathf.Approximately(s, current)
        );

        label.text = (index >= 0 && index < speedLabels.Length)
            ? speedLabels[index]
            : $"{current}x";
    }
}

using TMPro;
using UnityEngine;

/// <summary>소환권 보유 수 표시(ShopScene 전용) — 공용 CurrencyHUD 에 칸을 넣는 대신 보일 씬에만 붙임.</summary>
// [붙이는 위치] ShopScene 골드/보석 옆 아이콘+TMP 오브젝트(또는 항상 켜진 부모)에 붙이고 Count Text 연결, StageScene 엔 붙이지 말 것
public class GachaTicketHUD : MonoBehaviour
{
    [Tooltip("보유 수를 표시할 텍스트")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("{0} 자리에 보유 수가 들어갑니다. 예: \"x{0}\"")]
    [SerializeField] private string format = "{0}";

    private void OnEnable()
    {
        GachaTicket.OnChanged += UpdateCount;
        UpdateCount(GachaTicket.Count);   // 구독 전에 바뀐 값도 놓치지 않게 한 번 읽음
    }

    private void OnDisable()
    {
        GachaTicket.OnChanged -= UpdateCount;   // static 이벤트는 반드시 해제
    }

    private void UpdateCount(int count)
    {
        if (countText == null) return;

        // 골드·보석과 같은 표기 규칙 (CurrencyHUD 참고)
        string number = NumberFormat.Short(count);
        try
        {
            countText.text = string.Format(format, number);
        }
        catch (System.FormatException)
        {
            // 인스펙터 format 오타 하나로 표시가 멈추지 않게 숫자만 표시합니다.
            countText.text = number;
            Debug.LogWarning($"[GachaTicketHUD] format 문자열이 잘못됐습니다: '{format}' (예: \"x{{0}}\")", this);
        }
    }
}

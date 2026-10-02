using TMPro;
using UnityEngine;

/// <summary>
/// 소환권 보유 수 표시. ShopScene 에만 둡니다.
///
/// [왜 CurrencyHUD 에 칸을 추가하지 않았나]
///   CurrencyHUD 는 StageScene · EvolveScene · ShopScene 이 함께 쓰는 골드/보석 표시입니다.
///   소환권은 "상점에서만 보인다" 가 규칙이라, 보여 줄 씬에만 이 컴포넌트를 붙이는 쪽이
///   씬마다 칸을 비워 두는 것보다 실수할 여지가 적습니다. (StageScene 에는 붙이지 마세요)
///
/// [붙이는 법]
///   ShopScene 의 골드/보석 표시 옆에 소환권 아이콘 + TMP 텍스트를 두고,
///   이 컴포넌트를 그 오브젝트(또는 항상 켜진 부모)에 붙여 Count Text 를 연결합니다.
/// </summary>
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
        string number = KoreanNumberFormatter.Format(count);
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

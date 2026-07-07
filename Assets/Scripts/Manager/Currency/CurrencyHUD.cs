using UnityEngine;
using TMPro;

public class CurrencyHUD : MonoBehaviour   // ← 독립 클래스, MonoBehaviour 상속
{
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI gemText;

    private void OnEnable()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;

        cm.OnGoldChanged += UpdateGold;
        cm.OnGemChanged  += UpdateGem;

        UpdateGold(cm.Gold);
        UpdateGem(cm.Gem);
    }

    private void OnDisable()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return;
        cm.OnGoldChanged -= UpdateGold;
        cm.OnGemChanged  -= UpdateGem;
    }

    private void UpdateGold(int value) { if (goldText != null) goldText.text = Format(value); }
    private void UpdateGem(int value)  { if (gemText  != null) gemText.text  = Format(value); }

    private string Format(int money)
    {
        if (money < 1000) return money.ToString();
        string[] units = { "", "K", "M", "G", "T" };
        int i = 0; double d = money;
        while (d >= 1000 && i < units.Length - 1) { d /= 1000; i++; }
        return d.ToString("F1") + units[i];
    }
}
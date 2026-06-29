// CurrencyManager의 세이브 연동 partial.
// 골드/젬을 SaveData와 주고받는다.
// ※ CurrencyManager는 DontDestroyOnLoad가 아니므로(씬 HUD 참조 유지),
//   씬마다 Start에서 ApplyFrom으로 세이브 값을 다시 읽어온다.
public partial class CurrencyManager
{
    /// <summary>현재 골드/젬을 SaveData에 기록 (저장 시 SaveManager가 호출).</summary>
    public void CaptureTo(SaveData d)
    {
        d.gold = gold;
        d.gem  = gem;
    }

    /// <summary>SaveData의 골드/젬을 반영하고 UI/이벤트 갱신 (씬 진입 시 Start에서 호출).</summary>
    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;

        gold = d.gold;
        gem  = d.gem;

        UpdateGoldUI();
        UpdateGemUI();
        OnGoldChanged?.Invoke(gold);
        OnGemChanged?.Invoke(gem);
    }
}
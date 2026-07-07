public partial class CurrencyManager
{
    public void CaptureTo(SaveData d)
    {
        d.gold = gold;
        d.gem  = gem;
    }

    public void ApplyFrom(SaveData d)
    {
        if (d == null) return;

        gold = d.gold;
        gem  = d.gem;

        // UI 직접 호출 제거 — 이벤트만 발행하면 CurrencyHUD가 갱신함
        OnGoldChanged?.Invoke(gold);
        OnGemChanged?.Invoke(gem);
    }
}
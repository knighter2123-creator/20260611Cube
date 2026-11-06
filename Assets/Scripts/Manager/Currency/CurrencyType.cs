namespace Manager.currency
{
    /// <summary>재화 종류 — ⚠ 에셋에 숫자로 저장되므로 새 재화는 맨 뒤에 추가 (중간에 끼우면 기존 상품이 한 칸씩 밀림).</summary>
    public enum CurrencyType
    {
        Gold        = 0,
        Gem         = 1,
        Cash        = 2,   // 추후 IAP(현금 결제)용. 지금은 스텁.
        GachaTicket = 3,   // 소환권 — 보관·증감은 GachaTicket 이 담당
    }
}

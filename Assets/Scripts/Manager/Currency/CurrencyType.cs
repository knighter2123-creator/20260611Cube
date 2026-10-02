namespace Manager.currency
{
    /// <summary>
    /// 재화 종류.
    /// ★ 에셋(상점 상품 등)에는 이름이 아니라 숫자로 저장됩니다.
    ///   새 재화는 반드시 '맨 뒤' 에 추가하세요. 중간에 끼우면 기존 상품의 재화 종류가 한 칸씩 밀립니다.
    /// </summary>
    public enum CurrencyType
    {
        Gold        = 0,
        Gem         = 1,
        Cash        = 2,   // 추후 IAP(현금 결제)용. 지금은 스텁.
        GachaTicket = 3,   // 소환권 — 보관·증감은 GachaTicket 이 담당
    }
}

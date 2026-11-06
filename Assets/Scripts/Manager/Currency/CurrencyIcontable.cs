using UnityEngine;

namespace Manager.currency
{
    /// <summary>재화 종류 → 아이콘 매핑(상품 카드 비용 아이콘용) — Create → Shop → Currency Icon Table 로 에셋 1개 생성.</summary>
    [CreateAssetMenu(fileName = "CurrencyIconTable", menuName = "Shop/Currency Icon Table")]
    public class CurrencyIconTable : ScriptableObject
    {
        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite gemIcon;
        [SerializeField] private Sprite gachaTicketIcon;   // 소환권

        public Sprite Get(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Gold:        return goldIcon;
                case CurrencyType.Gem:         return gemIcon;
                case CurrencyType.GachaTicket: return gachaTicketIcon;
                default:                       return null;   // Cash는 아이콘 대신 ₩ 텍스트 사용
            }
        }
    }
}

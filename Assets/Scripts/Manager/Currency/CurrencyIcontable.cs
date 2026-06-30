using UnityEngine;
using Manager.currency;

/// <summary>
/// 재화 종류 → 아이콘 스프라이트 매핑.
/// 상품 카드가 자기 CostType에 맞는 아이콘을 골라 표시할 때 사용.
/// Create → Shop → Currency Icon Table 로 에셋 1개 생성 후 스프라이트 연결.
/// </summary>

namespace Manager.currency
{
    [CreateAssetMenu(fileName = "CurrencyIconTable", menuName = "Shop/Currency Icon Table")]
    public class CurrencyIconTable : ScriptableObject
    {
        [SerializeField] private Sprite goldIcon;
        [SerializeField] private Sprite gemIcon;

        public Sprite Get(CurrencyType type)
        {
            switch (type)
            {
                case CurrencyType.Gold: return goldIcon;
                case CurrencyType.Gem:  return gemIcon;
                default:                return null;   // Cash는 아이콘 대신 ₩ 텍스트 사용
            }
        }
    }
}
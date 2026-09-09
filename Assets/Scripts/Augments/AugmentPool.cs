using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 등장할 수 있는 카드 목록과 추첨 규칙을 담는 에셋.
///
/// [왜 매니저가 아니라 별도 에셋인가]
/// "1~3월드용 풀", "4월드부터 풀" 처럼 구간별로 다른 카드 세트를 쓰고 싶을 때
/// 에셋만 갈아끼우면 됩니다. 코드 수정이 필요 없습니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/카드 풀", fileName = "AugmentPool")]
public class AugmentPool : ScriptableObject
{
    [Header("등장 카드 목록")]
    [SerializeField] private List<AugmentCard> cards = new List<AugmentCard>();

    [Header("등급별 가중치 배수")]
    [Tooltip("카드 개별 weight 에 추가로 곱해집니다. 여기서 전체 등급 비율을 한 번에 조절하세요")]
    [SerializeField] private float commonMultiplier    = 1.00f;
    [SerializeField] private float rareMultiplier      = 0.50f;
    [SerializeField] private float epicMultiplier      = 0.18f;
    [SerializeField] private float legendaryMultiplier = 0.05f;

    [Header("등급 상승 보정")]
    [Tooltip("스테이지가 오를수록 높은 등급이 더 잘 나오게 합니다. 0이면 보정 없음")]
    [SerializeField] private float rarityBoostPerWorld = 0.08f;

    public IReadOnlyList<AugmentCard> Cards => cards;

    /// <summary>저장 파일에 있는 ID로 카드 에셋을 되찾습니다. 없으면 null.</summary>
    public AugmentCard FindById(string id)
    {
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null && cards[i].Id == id) return cards[i];
        return null;
    }

    /// <summary>
    /// 서로 다른 카드 count 장을 가중치 추첨으로 뽑습니다.
    /// </summary>
    /// <param name="count">뽑을 장수 (보통 3)</param>
    /// <param name="stackOf">해당 카드가 지금 몇 스택인지 알려주는 함수. maxStack 초과 방지용</param>
    /// <param name="world">현재 월드. 등급 상승 보정에 사용</param>
    public List<AugmentCard> Draw(int count, System.Func<AugmentCard, int> stackOf, int world)
    {
        var result = new List<AugmentCard>(count);

        // 1) 후보 추리기 — 최대 스택에 도달한 영구 카드는 제외
        var candidates = new List<AugmentCard>();
        var weights    = new List<float>();

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null) continue;
            if (c.Weight <= 0f) continue;

            if (c.IsPermanent && c.MaxStack > 0 && stackOf(c) >= c.MaxStack)
                continue;   // 이미 꽉 찬 카드는 안 나오게

            candidates.Add(c);
            weights.Add(GetWeight(c, world));
        }

        // 2) 후보가 뽑을 장수보다 적으면 있는 만큼만 (게임이 멈추면 안 되니까)
        int pick = Mathf.Min(count, candidates.Count);

        // 3) 가중치 추첨을 pick 번 반복.
        //    한 번 뽑은 카드는 후보에서 빼서 같은 카드가 두 장 뜨는 걸 막습니다.
        for (int n = 0; n < pick; n++)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0f) break;

            // [가중치 추첨의 원리]
            // 0 ~ total 사이 난수를 하나 뽑고, 후보들의 무게를 앞에서부터 빼나가다가
            // 0 이하가 되는 순간의 후보가 당첨입니다.
            // 무게가 큰 후보일수록 '구간'이 넓어서 더 자주 걸립니다.
            float r = Random.Range(0f, total);
            int chosen = weights.Count - 1;   // 부동소수점 오차 대비 기본값

            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) { chosen = i; break; }
            }

            result.Add(candidates[chosen]);
            candidates.RemoveAt(chosen);
            weights.RemoveAt(chosen);
        }

        return result;
    }

    /// <summary>카드 개별 weight × 등급 배수 × 월드 보정</summary>
    private float GetWeight(AugmentCard card, int world)
    {
        float rarityMul;
        int   rarityStep;   // 등급이 높을수록 월드 보정을 크게 받음

        switch (card.Rarity)
        {
            case AugmentRarity.Legendary: rarityMul = legendaryMultiplier; rarityStep = 3; break;
            case AugmentRarity.Epic:      rarityMul = epicMultiplier;      rarityStep = 2; break;
            case AugmentRarity.Rare:      rarityMul = rareMultiplier;      rarityStep = 1; break;
            default:                      rarityMul = commonMultiplier;    rarityStep = 0; break;
        }

        // 월드가 오를수록 고등급 가중치를 서서히 올립니다.
        // 1월드에서는 보정 0, 5월드 Epic 이면 ×(1 + 0.08×2×4) = ×1.64
        if (rarityStep > 0 && rarityBoostPerWorld > 0f)
            rarityMul *= 1f + rarityBoostPerWorld * rarityStep * Mathf.Max(0, world - 1);

        return Mathf.Max(0f, card.Weight * rarityMul);
    }
}

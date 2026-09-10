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

        // 왜 제외됐는지 세어둡니다. 장수가 모자랄 때 원인을 짚어주기 위해서입니다.
        int nullSlots = 0, zeroWeight = 0, maxedOut = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];
            if (c == null)        { nullSlots++;  continue; }
            if (c.Weight <= 0f)   { zeroWeight++; continue; }

            if (c.IsPermanent && c.MaxStack > 0 && stackOf(c) >= c.MaxStack)
            {
                maxedOut++;
                continue;   // 이미 꽉 찬 카드는 안 나오게
            }

            // ★ 최종 가중치로 걸러야 합니다.
            //
            //   원래는 카드 개별 weight 만 보고 후보에 넣었는데,
            //   등급 배수(예: legendaryMultiplier = 0)를 곱하면 최종 가중치가 0이 될 수 있습니다.
            //   그러면 후보 목록에는 들어가 있지만 추첨에서는 절대 안 뽑히고,
            //   아래 total <= 0 검사에 걸려 루프가 break 되면서
            //   "3장을 요청했는데 2장만 나오는" 증상이 됩니다.
            //
            //   후보 자격은 "뽑힐 가능성이 있는가"로 판단해야 한다는 이야기입니다.
            float w = GetWeight(c, world);
            if (w <= 0f) { zeroWeight++; continue; }

            candidates.Add(c);
            weights.Add(w);
        }

        // 2) 후보가 뽑을 장수보다 적으면 있는 만큼만 (게임이 멈추면 안 되니까)
        int pick = Mathf.Min(count, candidates.Count);

        // ★ 왜 모자란지 알려줍니다.
        //   조용히 넘어가면 "카드가 두 장만 나오는데 이유를 모르겠는" 상황이 됩니다.
        //   실패했을 때 원인을 남기는 건 방어 코드의 기본입니다.
        if (pick < count)
        {
            Debug.LogWarning(
                $"[AugmentPool] {count}장을 요청했지만 후보가 {candidates.Count}장뿐입니다.\n" +
                $"  등록된 카드 {cards.Count}개 중 — " +
                $"비어있음(None) {nullSlots} / 가중치 0 {zeroWeight} / 최대스택 도달 {maxedOut}\n" +
                $"  → Cards 목록에 빈 칸이 있는지, weight 와 등급 배수가 0은 아닌지 확인하세요.");
        }

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

    // ─────────────────────────────────────────────────────────
    //  진단
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 지금 이 풀에서 각 카드가 실제로 몇 %의 확률로 뽑히는지 출력합니다.
    /// 에셋 인스펙터 우클릭 → "1월드 기준 등장 확률 확인".
    ///
    /// 카드가 요청한 장수만큼 안 나오거나 특정 카드가 안 보일 때 여기부터 보세요.
    /// </summary>
    [ContextMenu("1월드 기준 등장 확률 확인")]
    private void DumpChances()
    {
        if (cards == null || cards.Count == 0)
        {
            Debug.LogWarning("[AugmentPool] Cards 목록이 비어 있습니다.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[AugmentPool] {name} — 카드 {cards.Count}개 (1월드 기준)");

        // 먼저 총합을 구해야 각자의 비율을 낼 수 있습니다.
        float total = 0f;
        for (int i = 0; i < cards.Count; i++)
            if (cards[i] != null) total += GetWeight(cards[i], 1);

        int usable = 0;

        for (int i = 0; i < cards.Count; i++)
        {
            var c = cards[i];

            if (c == null)
            {
                sb.AppendLine($"  [{i}] ⚠ 비어 있음(None) — 이 칸 때문에 카드 장수가 모자랄 수 있습니다");
                continue;
            }

            float w = GetWeight(c, 1);
            if (w <= 0f)
            {
                sb.AppendLine($"  [{i}] ⚠ {c.DisplayName} — 가중치 0, 절대 안 나옴 " +
                              $"(weight={c.Weight}, 등급={c.Rarity})");
                continue;
            }

            usable++;
            sb.AppendLine($"  [{i}] {c.DisplayName} ({c.Rarity}) — {w / total * 100f:0.0}%");
        }

        sb.AppendLine($"  → 뽑을 수 있는 카드: {usable}장");
        if (usable < 3)
            sb.AppendLine("  ⚠ 3장 미만입니다. 카드를 더 만들거나 위의 경고를 해결하세요.");

        Debug.Log(sb.ToString());
    }
}
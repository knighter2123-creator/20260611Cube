using System;
using UnityEngine;

/// <summary>각성 단계별 보상 정의 SO — 밸런스 수치는 데이터로 빼 재컴파일 없이 인스펙터에서 조정 (코드는 규칙, 데이터는 수치).</summary>
// [생성] Project 우클릭 → Create → Game → 각성 보상 테이블
[CreateAssetMenu(menuName = "Game/각성 보상 테이블", fileName = "AwakeningRewardTable")]
public class AwakeningRewardTable : ScriptableObject
{
    /// <summary>각성 한 단계의 보상 묶음.</summary>
    [Serializable]
    public class Tier
    {
        [Tooltip("이 단계에 해당하는 각성 스테이지 데이터 에셋을 연결하세요.\n" +
                 "여기서 id를 읽어 세이브의 claimedEvolveRewards와 대조합니다.")]
        public EvolveStageData stage;

        [Tooltip("이 단계를 클리어했을 때 플레이어가 갈아입을 스프라이트.\n" +
                 "비워두면 이전 단계 모습을 그대로 유지합니다 (모든 단계마다 새 아트가 없어도 됨).")]
        public Sprite playerSprite;

        [Tooltip("이 단계에서 '추가로' 늘어나는 동시 발사체 수. 누적값이 아니라 증가분입니다.")]
        [Min(0)] public int addProjectiles = 0;

        [Tooltip("이 단계에서 '추가로' 늘어나는 연사(연속 공격) 횟수. 누적값이 아니라 증가분입니다.")]
        [Min(0)] public int addBurstShots = 0;

        [Tooltip("보상 미리보기에 쓸 문구 (선택).\n" +
                 "비워두면 아래 숫자들로 자동 생성됩니다. 직접 쓰면 그게 우선입니다.\n" +
                 "예: \"화살이 2발로 갈라집니다\"")]
        [TextArea(1, 3)] public string description;

        /// <summary>입장 패널용 보상 한 줄 요약(예: "공격력 +30%  |  발사체 +1") — 보상 종류를 아는 테이블 쪽에 둬 종류 추가 시 한 곳만 수정.</summary>
        public string RewardSummary()
        {
            // 직접 쓴 문구가 있으면 그게 우선입니다
            if (!string.IsNullOrWhiteSpace(description)) return description.Trim();

            string summary = "";

            // 공격력 버프는 각성 스테이지 데이터가 들고 있습니다
            if (stage != null && stage.damageBuffPercent > 0f)
                summary = $"공격력 +{stage.damageBuffPercent * 100f:0.#}%";

            summary = Append(summary, addProjectiles > 0 ? $"발사체 +{addProjectiles}" : null);
            summary = Append(summary, addBurstShots  > 0 ? $"연속 공격 +{addBurstShots}" : null);
            summary = Append(summary, playerSprite != null ? "외형 변화" : null);

            return summary;
        }

        // 구분자는 PlayerStatusText.SEPARATOR 재사용 (폰트에 '·' 이 없어 통일한 값, 고칠 곳을 하나로)
        private static string Append(string body, string part)
        {
            if (string.IsNullOrEmpty(part)) return body;
            return string.IsNullOrEmpty(body) ? part : body + PlayerStatusText.SEPARATOR + part;
        }
    }

    // 값은 누적이 아닌 '증가분'으로 입력 — 단계를 끼워 넣어도 그 칸만 채우면 되고 합계는 코드가 계산

    [Tooltip("각성 스테이지 순서대로 넣으세요. 현재 기획은 5단계입니다.")]
    [SerializeField] private Tier[] tiers = new Tier[0];

    public int TierCount => tiers != null ? tiers.Length : 0;

    public Tier GetTier(int index)
    {
        if (tiers == null || index < 0 || index >= tiers.Length) return null;
        return tiers[index];
    }

    /// <summary>각성 스테이지에 대응하는 티어 반환(없으면 null) — 입장 패널 보상 미리보기용.</summary>
    public Tier FindByStage(EvolveStageData stage)
    {
        if (stage == null || tiers == null) return null;

        for (int i = 0; i < tiers.Length; i++)
            if (tiers[i] != null && tiers[i].stage == stage) return tiers[i];

        return null;
    }

    /// <summary>해당 인덱스 단계의 스테이지 id. 연결이 비어 있으면 null.</summary>
    public string GetStageId(int index)
    {
        Tier t = GetTier(index);
        return (t != null && t.stage != null) ? t.stage.id : null;
    }

#if UNITY_EDITOR
    /// <summary>인스펙터 값 변경 시 자동 검증 (에디터 전용) — 설정 실수를 런타임 전에 잡음.</summary>
    private void OnValidate()
    {
        if (tiers == null) return;

        for (int i = 0; i < tiers.Length; i++)
        {
            if (tiers[i] == null) continue;

            if (tiers[i].stage == null)
            {
                Debug.LogWarning($"[각성 보상 테이블] {i}번 칸의 '스테이지 데이터'가 비어 있습니다. " +
                                 $"이 단계는 영원히 달성되지 않습니다.", this);
                continue;
            }

            // id 중복 검사 — 같은 에셋을 두 칸에 넣으면 보상이 이중으로 더해집니다
            for (int j = i + 1; j < tiers.Length; j++)
            {
                if (tiers[j] != null && tiers[j].stage == tiers[i].stage)
                    Debug.LogWarning($"[각성 보상 테이블] {i}번과 {j}번이 같은 스테이지를 가리킵니다 " +
                                     $"({tiers[i].stage.name}). 보상이 두 번 더해집니다.", this);
            }

            // requiredLevel 오름차순 검사 — 스프라이트는 마지막 지정값이 이겨 배열 순서가 곧 외형 순서 (입장 순서와 어긋나지 않게)
            if (i > 0 && tiers[i - 1] != null && tiers[i - 1].stage != null
                && tiers[i - 1].stage.requiredLevel > tiers[i].stage.requiredLevel)
            {
                Debug.LogWarning($"[각성 보상 테이블] 순서가 뒤바뀌었습니다 — " +
                                 $"{i - 1}번(Lv.{tiers[i - 1].stage.requiredLevel})이 " +
                                 $"{i}번(Lv.{tiers[i].stage.requiredLevel})보다 뒤에 와야 합니다. " +
                                 $"요구 레벨 오름차순으로 정렬하세요.", this);
            }
        }
    }
#endif
}
using System;
using UnityEngine;

/// <summary>
/// 각성(진화) 단계별 보상 정의 — ScriptableObject 데이터 에셋.
///
/// ─── 왜 코드가 아니라 데이터로 빼는가? (학습 포인트) ─────────────────
/// "2단계에서 발사체 +1" 같은 건 밸런스 수치입니다. 밸런스는 개발 기간 내내
/// 수십 번 바뀌는데, 그때마다 스크립트를 고치면 매번 재컴파일(유니티는 몇 초~수십 초)이
/// 걸리고, 고치다 오타 하나 나면 게임 전체가 컴파일 실패합니다.
///
/// ScriptableObject로 빼두면 인스펙터에서 숫자만 바꾸면 끝입니다. 컴파일 0초.
/// 이 프로젝트가 이미 동료/적/상점/미션을 전부 SO로 빼 둔 것과 같은 이유예요.
///
/// ★ "코드는 규칙을, 데이터는 수치를" — 이 기준으로 나누면 대부분 맞습니다.
/// ────────────────────────────────────────────────────────────────────
///
/// [만드는 법] Project 창 우클릭 → Create → Game → 각성 보상 테이블
/// </summary>
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

        /// <summary>
        /// 입장 패널에 띄울 보상 한 줄 요약.
        /// 예) "공격력 +30%  |  발사체 +1"
        ///
        /// ─── 왜 UI가 아니라 데이터 쪽에 두는가? (학습 포인트) ─────────────
        /// "이 티어가 무엇을 주는가"는 테이블이 가장 잘 압니다. UI에 두면
        /// 나중에 보상 종류를 하나 추가할 때(관통, 유도 등) 테이블과 UI를
        /// 둘 다 고쳐야 하고, 한쪽을 빼먹으면 화면이 조용히 거짓말을 합니다.
        ///
        /// PlayerStatusText 를 UI 밖으로 뺀 것과 같은 판단입니다 —
        /// **부품(TMP_Text)을 몰라도 되는 코드는 UI 밖에 둔다.**
        /// ──────────────────────────────────────────────────────────────
        /// </summary>
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

        // ★ 구분자는 PlayerStatusText.SEPARATOR 를 재사용합니다.
        //   프로젝트 폰트(주아체)에 가운뎃점(·)이 없어서 "  |  " 로 통일해 두셨는데,
        //   여기서 새 구분자를 쓰면 그 결정이 또 갈라집니다.
        //   폰트를 바꿀 때 고칠 곳이 한 군데로 유지됩니다.
        private static string Append(string body, string part)
        {
            if (string.IsNullOrEmpty(part)) return body;
            return string.IsNullOrEmpty(body) ? part : body + PlayerStatusText.SEPARATOR + part;
        }
    }

    // ─── 왜 '증가분'으로 적게 했는가? ─────────────────────────────────
    // 누적값(1단계=1발, 2단계=2발, 3단계=2발 …)으로 적으면,
    // 중간에 한 단계를 끼워 넣을 때 그 뒤 모든 칸을 손으로 다시 써야 합니다.
    // 증가분(0, +1, 0, +1, 0)으로 적으면 끼워 넣은 칸만 채우면 끝입니다.
    // 합계는 코드가 계산하면 되니까요 — 사람이 계산해서 적는 값은 언젠가 틀립니다.
    // ────────────────────────────────────────────────────────────────

    [Tooltip("각성 스테이지 순서대로 넣으세요. 현재 기획은 5단계입니다.")]
    [SerializeField] private Tier[] tiers = new Tier[0];

    public int TierCount => tiers != null ? tiers.Length : 0;

    public Tier GetTier(int index)
    {
        if (tiers == null || index < 0 || index >= tiers.Length) return null;
        return tiers[index];
    }

    /// <summary>
    /// 해당 각성 스테이지에 대응하는 티어를 찾습니다. 없으면 null.
    /// 입장 패널의 '보상 미리보기'가 씁니다.
    /// </summary>
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
    /// <summary>
    /// 인스펙터에서 값이 바뀔 때마다 자동 검증.
    /// 에디터 전용이라 빌드에는 포함되지 않습니다(#if UNITY_EDITOR).
    ///
    /// ★ 이런 자가 검증을 붙여두면 "왜 각성해도 아무 일이 없지?"를
    ///   런타임에 Debug.Log 심어가며 찾는 시간을 없앨 수 있습니다.
    /// </summary>
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

            // ★ 순서 검사 — requiredLevel 오름차순이어야 합니다.
            //
            //   발사체/연사 '합계'는 각 티어를 개별로 확인하므로 순서와 무관합니다.
            //   하지만 **스프라이트는 "마지막으로 지정된 것이 이긴다"** 규칙이라
            //   배열 순서가 곧 성장 순서입니다. 순서가 뒤섞이면 3단계를 깼는데
            //   1단계 모습이 나오는 식으로 조용히 틀립니다.
            //
            //   EvolveStageEntry 는 Awake 에서 같은 기준으로 정렬하고 있어서,
            //   여기만 어긋나면 "입장 순서와 외형 순서가 다른" 상태가 됩니다.
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
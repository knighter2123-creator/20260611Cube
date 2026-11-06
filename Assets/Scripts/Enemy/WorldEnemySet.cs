using UnityEngine;

/// <summary>한 월드(1-1~1-10)의 적 프리팹 데이터 에셋 — SO 로 데이터를 코드에서 분리해 월드가 늘어도 에셋만 추가.</summary>
// [생성] Project 우클릭 → Create → Game → World Enemy Set
[CreateAssetMenu(menuName = "Game/World Enemy Set", fileName = "WorldXSet")]
public class WorldEnemySet : ScriptableObject
{
    /// <summary>특정 스테이지 예외 규칙(예: 5스테이지는 속도 1.5배) — [Serializable] 이어야 인스펙터에 보임.</summary>
    [System.Serializable]
    public class StageOverride
    {
        [Tooltip("특수 처리할 스테이지 번호 (1~10)")]
        public int stage = 5;

        [Tooltip("이 스테이지에서만 쓸 프리팹. 비워두면(None) normalPrefab을 그대로 사용합니다.")]
        public GameObject prefab;

        [Tooltip("이동 속도 배율. 1 = 기본, 1.5 = 50% 빠름")]
        public float speedMultiplier = 1f;

        [Tooltip("스폰 주기 배율. 1 = 기본, 0.8 = 20% 더 자주 등장")]
        public float respawnDelayMultiplier = 1f;
    }

    [Header("이 세트가 담당하는 월드 번호 (기록용)")]
    [Tooltip("실제 선택은 배열 순서(index)로 하지만, 에셋을 눈으로 구분하기 위해 적어둡니다.")]
    public int world = 1;

    [Header("기본 프리팹")]
    [Tooltip("이 월드의 일반 잡몹")]
    public GameObject normalPrefab;

    [Tooltip("이 월드의 보스 (killGoal 달성 시 등장)")]
    public GameObject bossPrefab;

    [Header("특수 스테이지 규칙")]
    [Tooltip("여기에 등록되지 않은 스테이지는 전부 normalPrefab을 기본 속도로 사용합니다.")]
    public StageOverride[] overrides;

    /// <summary>해당 스테이지 예외 규칙 반환(없으면 null) — for 사용은 열거자 GC 를 피하는 습관(여기선 스테이지당 1번이라 일관성 목적).</summary>
    public StageOverride GetOverride(int stage)
    {
        if (overrides == null) return null;

        for (int i = 0; i < overrides.Length; i++)
        {
            if (overrides[i] != null && overrides[i].stage == stage)
                return overrides[i];
        }

        return null;   // 예외 규칙 없음 = 기본값 사용
    }

#if UNITY_EDITOR
    // 같은 스테이지 규칙이 둘이면 GetOverride 는 앞쪽만 씁니다. 뒤쪽을 고쳐도 반영이 안 돼 헷갈리므로 알려 줍니다.
    private void OnValidate()
    {
        if (overrides == null) return;

        for (int i = 0; i < overrides.Length; i++)
        {
            if (overrides[i] == null) continue;
            for (int j = 0; j < i; j++)
            {
                if (overrides[j] != null && overrides[j].stage == overrides[i].stage)
                {
                    Debug.LogWarning($"[WorldEnemySet] '{name}' 에 {overrides[i].stage} 스테이지 규칙이 둘 이상입니다. " +
                                     $"앞쪽(Element {j})만 적용됩니다.", this);
                    break;
                }
            }
        }
    }
#endif
}

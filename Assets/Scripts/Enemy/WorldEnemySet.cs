using UnityEngine;

/// <summary>
/// 한 월드(예: 월드 1 = 1-1 ~ 1-10)에서 등장할 적 프리팹 정보를 담는 데이터 에셋.
///
/// ─── 왜 ScriptableObject인가? (학습 포인트) ─────────────────────────────
/// MonoBehaviour는 "씬 안의 게임오브젝트에 붙어야만" 존재할 수 있습니다.
/// 반면 ScriptableObject(SO)는 씬과 무관하게 "프로젝트 폴더에 파일로" 존재합니다.
///
///   MonoBehaviour → 행동(Behaviour). Update가 돌고, 위치가 있고, 씬에 산다.
///   ScriptableObject → 데이터(Data). 그냥 값 덩어리. 에셋 파일로 저장된다.
///
/// 월드가 20개로 늘어나도 코드는 한 줄도 안 고치고 에셋만 20개 만들면 됩니다.
/// "데이터를 코드에서 분리한다"는 게 게임 개발에서 가장 자주 쓰는 패턴이에요.
///
/// [CreateAssetMenu] 를 붙이면 Project 창에서
///   우클릭 → Create → Game → World Enemy Set
/// 으로 이 SO의 에셋 파일을 만들 수 있게 됩니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(menuName = "Game/World Enemy Set", fileName = "WorldXSet")]
public class WorldEnemySet : ScriptableObject
{
    /// <summary>
    /// 특정 스테이지만 다르게 처리하고 싶을 때 쓰는 예외 규칙.
    /// 예) "5스테이지에서는 속도가 1.5배인 적이 나온다"
    ///
    /// [System.Serializable] 을 붙여야 유니티 인스펙터에 이 클래스가 펼쳐져 보입니다.
    /// (MonoBehaviour나 ScriptableObject를 상속하지 않은 일반 클래스는
    ///  이 속성이 없으면 인스펙터에 아예 안 나타납니다.)
    /// </summary>
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

    /// <summary>
    /// 해당 스테이지의 예외 규칙을 찾아 돌려줍니다. 없으면 null.
    ///
    /// ─── 왜 foreach가 아니라 for인가? (학습 포인트) ────────────────────
    /// 배열에 foreach를 쓰면 대부분의 경우 컴파일러가 최적화해 주지만,
    /// List<T>나 인터페이스 대상 foreach는 열거자(Enumerator) 객체를 만들면서
    /// GC(가비지 컬렉션) 부담을 줍니다. 방치형 게임처럼 같은 코드가
    /// 수천 번 반복 호출되는 환경에서는 이 습관이 프레임 드랍을 막아줍니다.
    /// (다만 이 함수는 스테이지 전환 때 딱 1번만 불리므로, 여기서는
    ///  "습관 들이기" 목적이 큽니다. 성능보다 일관성이 중요해요.)
    /// ──────────────────────────────────────────────────────────────
    /// </summary>
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
}

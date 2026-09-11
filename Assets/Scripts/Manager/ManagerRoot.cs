using UnityEngine;

/// <summary>
/// 모든 매니저의 부모. 이 오브젝트 하나만 DontDestroyOnLoad 로 유지하면
/// 자식 매니저 전부가 함께 살아남는다.
///
/// ★★ Script Execution Order 설정이 반드시 필요하다 ★★
///   Project Settings → Script Execution Order 에서
///       ManagerRoot   = -200
///       SaveManager   = -100
///   으로 지정할 것.
///
///   이유는 아래 Awake 주석 참고. 설정하지 않으면 자식 매니저의 Awake 가
///   ManagerRoot 보다 먼저 돌 수 있고, 그 순간 중복 제거 규칙이 어긋난다.
/// </summary>
public class ManagerRoot : MonoBehaviour
{
    public static ManagerRoot Instance { get; private set; }

    [Header("디버그")]
    [Tooltip("루트 생성/중복 제거를 콘솔에 찍습니다. 원인 파악 후 끄세요.")]
    [SerializeField] private bool logLifecycle = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (logLifecycle)
                Debug.Log($"[ManagerRoot] 중복 루트 제거 — 씬 '{gameObject.scene.name}' 의 '{name}'. " +
                          $"기존 루트를 유지합니다.", this);

            // ★★ 여기가 이번 수정의 핵심이다 ★★
            //
            //   Destroy() 는 "지금" 지우지 않는다. 프레임 끝에 지운다.
            //   그래서 이 줄만 있으면, 지워지기로 예약된 이 루트의 자식 매니저들이
            //   같은 프레임에 자기 Awake 를 그대로 실행한다.
            //
            //   자식 매니저들은 저마다 이런 코드를 갖고 있다.
            //       if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            //       Instance = this;
            //
            //   여기서 static Instance 는 씬을 넘어 살아남는 값이다.
            //   타이밍이 어긋나 이 값이 잠깐 null 이 되는 순간에 죽을 자식이 Awake 를 돌면,
            //   그 자식이 Instance = this 로 자기를 등록해 버린다.
            //   그리고 프레임 끝에 이 루트가 통째로 지워지면서 그 자식도 함께 사라진다.
            //   결과: 살아 있어야 할 매니저는 이미 없고, static Instance 는 죽은 객체를 가리킨다.
            //
            //   SetActive(false) 는 즉시 적용된다. 그리고 비활성 오브젝트의 컴포넌트에는
            //   Awake 가 호출되지 않는다. 그래서 이 한 줄이
            //   "죽을 자식들이 static 을 건드리는 것"을 원천 차단한다.
            //
            //   Script Execution Order 로 ManagerRoot 를 가장 앞(-200)에 두는 것과
            //   반드시 짝으로 써야 한다. 자식 Awake 가 이미 돌아버린 뒤라면
            //   SetActive(false) 로도 되돌릴 수 없기 때문이다.
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);   // 부모째로 유지 → 자식 매니저 전부 유지

        if (logLifecycle)
            Debug.Log($"[ManagerRoot] 루트 등록 — id={GetInstanceID()}, " +
                      $"자식 {transform.childCount}개, 씬 '{gameObject.scene.name}'", this);
    }

    private void OnDestroy()
    {
        // static 이 파괴된 오브젝트를 붙잡지 않게 정리.
        // Instance == this 로 검사하는 이유는, 중복 루트가 스스로 지워질 때
        // 살아 있는 진짜 루트까지 null 로 만들면 안 되기 때문이다.
        if (Instance == this)
        {
            Instance = null;

            // ★ 이 경고가 Login → Main 왕복 중에 보이면, 살아 있어야 할 루트가 죽은 것이다.
            //   그 아래 매니저 전부가 함께 사라졌다는 뜻이므로 여기가 출발점이다.
            if (logLifecycle)
                Debug.LogWarning("[ManagerRoot] 살아 있던 루트가 파괴되었습니다. " +
                                 "이후 모든 매니저 Instance 가 null 이 됩니다.", this);
        }
    }

}
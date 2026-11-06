using UnityEngine;

/// <summary>모든 매니저의 부모 — 이것 하나만 DontDestroyOnLoad 로 두면 자식 매니저가 함께 살아남음, 중복 루트는 자식 Awake 전에 꺼서 제거.</summary>
// ⚠ Script Execution Order 필수: ManagerRoot = -200, SaveManager = -100 (안 하면 자식 Awake 가 먼저 돌아 중복 제거가 어긋남)
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
                GameLog.Info($"[ManagerRoot] 중복 루트 제거 — 씬 '{gameObject.scene.name}' 의 '{name}'. " +
                          $"기존 루트를 유지합니다.", this);

            // ★ 즉시 꺼서 죽을 자식 매니저의 Awake 를 막음 — Destroy 는 프레임 끝이라 그 사이 자식이 Instance = this 로 등록하면 죽은 객체를 가리키게 됨 (Execution Order -200 과 짝)
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);   // 부모째로 유지 → 자식 매니저 전부 유지
    }

    private void OnDestroy()
    {
        // 내가 Instance 일 때만 비움 (중복 루트가 지워질 때 진짜 루트까지 null 로 만들지 않게)
        if (Instance == this) Instance = null;
    }
}
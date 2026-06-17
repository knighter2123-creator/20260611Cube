using UnityEngine;

/// <summary>
/// 게임 배속을 관리합니다. (1x → 2x → 3x → 1x 순환)
/// Time.timeScale을 직접 조정하므로 별도 연동 없이 모든 오브젝트에 적용됩니다.
/// </summary>
public class GameSpeedManager : MonoBehaviour
{
    public static GameSpeedManager Instance { get; private set; }

    [Header("배속 설정")]
    [SerializeField] private float[] speedSteps = { 1f, 2f, 3f };  // 순환할 배속 목록

    private int _currentIndex = 0;

    public float CurrentSpeed => speedSteps[_currentIndex];

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ApplySpeed();
    }

    /// <summary>다음 배속으로 전환합니다. (1x → 2x → 3x → 1x)</summary>
    public void CycleSpeed()
    {
        _currentIndex = (_currentIndex + 1) % speedSteps.Length;
        ApplySpeed();
    }

    /// <summary>배속을 1x(기본)으로 즉시 초기화합니다.</summary>
    public void ResetSpeed()
    {
        _currentIndex = 0;
        ApplySpeed();
    }

    private void ApplySpeed()
    {
        Time.timeScale = speedSteps[_currentIndex];
        Debug.Log($"[GameSpeed] {speedSteps[_currentIndex]}x 배속 적용");
    }

    void OnDestroy()
    {
        // 씬 전환 시 timeScale 초기화
        Time.timeScale = 1f;
    }
}

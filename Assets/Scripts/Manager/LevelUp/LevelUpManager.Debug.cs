// LevelUpManager.Debug.cs
#if UNITY_EDITOR
using UnityEngine;

partial class LevelUpManager
{
    [Header("디버그")] [SerializeField] private bool enableDebugKeys = true;

    void Update()
    {
        if (!enableDebugKeys) return;

        if (Input.GetKeyDown(KeyCode.F1))
            DebugSetLevel(30);
        if (Input.GetKeyDown(KeyCode.F2))
            DebugSetLevel(50);
        if (Input.GetKeyDown(KeyCode.F3))
            DebugSetLevel(70);
        if (Input.GetKeyDown(KeyCode.F4))
            DebugSetLevel(100);
        if (Input.GetKeyDown(KeyCode.F5))
            DebugSetLevel(200);
    }

    private void DebugSetLevel(int level)
    {
        if (stat == null)
        {
            Debug.LogWarning("[LevelUpManager] stat이 null — Init 전이라 레벨 설정 불가");
            return;
        }

        stat.Level = level;
        stat.Experience = 0;
        stat.MaxExperience = CalculateMaxExp(level); // 필요 경험치도 동기화

        OnLevelUp?.Invoke(stat.Level); // 레벨 표시 UI 갱신
        OnExpChanged?.Invoke(stat.Experience); // 경험치 바 갱신

        Debug.Log($"[LevelUpManager] 디버그: 레벨을 {level}로 설정");
    }
}
#endif

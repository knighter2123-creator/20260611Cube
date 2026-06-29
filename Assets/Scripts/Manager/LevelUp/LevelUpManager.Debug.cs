// LevelUpManager.Debug.cs
#if UNITY_EDITOR
using UnityEngine;

partial class LevelUpManager
{
    [Header("디버그")] [SerializeField] private bool enableDebugKeys = true;

    void Update()
    {
        if (!enableDebugKeys) return;

        // F1 : 레벨 30으로 점프
        if (Input.GetKeyDown(KeyCode.F1))
            DebugSetLevel(30);

        // F2 : 경험치 50 추가 (레벨업 로직 그대로 테스트)
        if (Input.GetKeyDown(KeyCode.F2))
            AddExp(50);
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

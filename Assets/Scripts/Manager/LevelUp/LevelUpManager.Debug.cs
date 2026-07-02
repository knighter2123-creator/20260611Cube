// LevelUpManager.Debug.cs
#if UNITY_EDITOR
using UnityEngine;

    // LevelUpManager.Debug.cs — Update를 직접 두지 말고 별도 메서드로
    partial class LevelUpManager
    {
        [Header("디버그")] [SerializeField] private bool enableDebugKeys = true;

        // Update() 대신 이 메서드를 메인 Update에서 호출
        private void HandleDebugKeys()
        {
            if (!enableDebugKeys) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) DebugSetLevel(30);
            if (Input.GetKeyDown(KeyCode.Alpha2)) DebugSetLevel(50);
            if (Input.GetKeyDown(KeyCode.Alpha3)) DebugSetLevel(70);
            if (Input.GetKeyDown(KeyCode.Alpha4)) DebugSetLevel(100);
            if (Input.GetKeyDown(KeyCode.Alpha5)) DebugSetLevel(200);
        }

        void Update()
        {
#if UNITY_EDITOR
            HandleDebugKeys();
#endif
        }

        private void DebugSetLevel(int level)
        {
            if (stat == null)
            {
                Debug.LogWarning("[LevelUpManager] stat이 null — Init 전이라 레벨 설정 불가");
                return;
            }

            stat.Level = level;
            stat.Experience = 1;
            stat.MaxExperience = CalculateMaxExp(level); // 필요 경험치도 동기화

            OnLevelUp?.Invoke(stat.Level); // 레벨 표시 UI 갱신
            OnExpChanged?.Invoke(stat.Experience); // 경험치 바 갱신

            Debug.Log($"[LevelUpManager] 디버그: 레벨을 {level}로 설정");
        }

#endif
    }

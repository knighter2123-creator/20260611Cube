using UnityEngine;

public class ManagerRoot : MonoBehaviour
{
    public static ManagerRoot Instance { get; private set; }

    private void Awake()
    {
        // 이 오브젝트는 루트여야 함
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);   // 씬 전환으로 중복 생성된 경우 제거
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);   // 부모째로 유지 → 자식 매니저 전부 유지
    }
}
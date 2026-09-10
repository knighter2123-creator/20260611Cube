using UnityEngine;
using UnityEngine.SceneManagement;


public class GameManager : MonoBehaviour
{
    Enemy enemy;
    Player player;
    LevelUpManager levelUpManager;
    
    public static GameManager Instance;
    
    public Player GetPlayer => player;
    public Enemy GetEnemy => enemy;

    void Awake()
    {
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        

        // 🔥 여기서 확보
        if (levelUpManager == null)
            levelUpManager = FindAnyObjectByType<LevelUpManager>();
    }

    void Start()
    {
        if (player == null)
            player = FindAnyObjectByType<Player>();

        if (levelUpManager == null)
            levelUpManager = FindAnyObjectByType<LevelUpManager>();
    }
   
    
    
    // void Update()
    // {
    //     // 안드로이드 뒤로가기 버튼 및 PC ESC 키 감지
    //     if (Input.GetKeyDown(KeyCode.Escape))
    //     {
    //         // 현재 활성화된 씬의 이름을 가져옴
    //         string currentSceneName = SceneManager.GetActiveScene().name;
    //
    //         // 원하는 특정 씬 이름 확인 (예: "GameScene")
    //         if (currentSceneName == "LoginScene")
    //         {
    //             
    //         }
    //         else if (currentSceneName == "MainMenu")
    //         {
    //             
    //         }
    //     }
    // }
}



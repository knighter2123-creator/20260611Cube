using System.Collections;
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
}
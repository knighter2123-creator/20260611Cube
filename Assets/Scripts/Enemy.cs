using UnityEngine;

public class Enemy : MonoBehaviour, IEnemyDead
{
    private EnemyStat estat;
    public bool isDead { get; set; }

    public void TakeDamage(float damage) 
    {
        estat.Health -= damage;

        if (estat.Health <= 0) 
        {
            Die();
        }
    }

    void Awake()
    {
      
    }
    
    void Start()
    {
        
    }

   
    void Update()
    {
        
    }

    void Die()
    {
        Destroy(gameObject);
        isDead = true;
    }
}

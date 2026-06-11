using UnityEngine;

public interface IEnemyDead
{
    void TakeDamage(float damage);
    
    bool isDead {get; set;}
}

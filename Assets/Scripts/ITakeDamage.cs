using UnityEngine;

public interface ITakeDamage
{
    void TakeDamage(float damage);
    
    bool isDead {get; set;}
}

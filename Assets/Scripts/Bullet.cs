using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet 설정")]
    [SerializeField] private float speed    = 15f;
    [SerializeField] private float lifeTime = 3f;

    private float       damage;
    private Vector2     direction;
    private float       timer;
    private Rigidbody2D rb;
    private bool        isReturned;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
    }

    void OnEnable()
    {
        timer      = 0f;
        isReturned = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    void OnDisable()
    {
        if (rb != null) rb.linearVelocity = Vector2.zero;
    }

    public void Init(Vector2 dir, float dmg)
    {
        direction         = dir.normalized;
        damage            = dmg;
        timer             = 0f;
        isReturned        = false;
        rb.linearVelocity = direction * speed;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            ReturnToPool();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;
        if (isReturned) return;

        ITakeDamage target = other.GetComponent<ITakeDamage>();
        if (target == null || target.isDead) return;

        target.TakeDamage(damage);

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReturned) return;
        isReturned = true;

        if (ObjectPoolManager.Instance != null)
            ObjectPoolManager.Instance.ReturnBullet(gameObject);
        else
            gameObject.SetActive(false);
    }
}
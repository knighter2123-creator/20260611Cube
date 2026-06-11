using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet 설정")]
    [SerializeField] private float speed    = 15f;
    [SerializeField] private float lifeTime = 3f;

    private float      damage;
    private Vector2    direction;
    private float      timer;
    private Rigidbody2D rb;
    private bool       isReturned;

    // ──────────────────────────────────────────────
    //  초기화
    // ──────────────────────────────────────────────
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    /// <summary>Player가 발사 직전에 호출합니다.</summary>
    public void Init(Vector2 dir, float dmg)
    {
        direction  = dir.normalized;
        damage     = dmg;
        timer      = 0f;
        isReturned = false;

        rb.linearVelocity = direction * speed;
    }

    // ──────────────────────────────────────────────
    //  Unity 생명 주기
    // ──────────────────────────────────────────────
    void OnEnable()
    {
        timer      = 0f;
        isReturned = false;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    void OnDisable()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifeTime)
            ReturnToPool();
    }

    // ──────────────────────────────────────────────
    //  충돌 처리 (2D Trigger)
    // ──────────────────────────────────────────────
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy != null && !enemy.isDead)
            enemy.TakeDamage(damage);

        ReturnToPool();
    }

    // ──────────────────────────────────────────────
    //  풀 반환 (중복 방지)
    // ──────────────────────────────────────────────
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
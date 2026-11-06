using UnityEngine;

public class Companion : MonoBehaviour
{
    private CompanionData data;

    private ActiveSkill skill => data != null ? data.ownedSkill : null;   // ScriptableObject 라 ?. 대신 유니티식 null 검사

    private float skillTimer = 0f;
    private bool  isPlaced   = false;

    public bool          IsPlaced      => isPlaced;
    public string        CompanionName => data != null ? data.companionName : "unknown";
    public string        Id            => data != null ? data.id : null;
    public CompanionData Data          => data;
    public ActiveSkill   OwnedSkill    => skill;

    // 스킬에서 플레이어 스탯(크리티컬 확률/배율, baseDamage)을 참조하기 위한 프로퍼티
    public PlayerStat    Stat          => Player.Instance?.stat;

    public void Init(CompanionData companionData)
    {
        data = companionData;
        if (data == null)
        {
            Debug.LogError("[Companion] Init 에 null 데이터가 들어왔습니다.", this);
            return;
        }

        if (data.ownedSkill == null)
            Debug.LogWarning($"[Companion] {data.companionName}에 스킬이 없습니다.");
    }

    void Update()
    {
        if (!isPlaced)    return;
        if (skill == null) return;

        skillTimer += Time.deltaTime;

        // 이 동료 등급·성급의 쿨다운을 매번 읽음 (스킬 에셋을 여러 동료가 공유해 에셋에 쓰지 않음, 성급이 오르면 바로 반영)
        if (skillTimer >= skill.GetCooldown(this))
        {
            Enemy target = FindClosestEnemy();
            if (target != null)
            {
                skillTimer = 0f;
                skill.Cast(target, this);
            }
        }
    }

    // ── 배치 / 회수 ──
    public void Place(Vector3 position)
    {
        transform.position = position;
        isPlaced           = true;
        skillTimer         = 0f;
        gameObject.SetActive(true);
    }

    public void Retrieve()
    {
        isPlaced = false;
        gameObject.SetActive(false);
    }

    // ── 적 탐지 — 매 프레임 불릴 수 있어 할당 없는 Enemy.Active 목록 + 제곱 거리 비교 (FindGameObjectsWithTag 는 매번 배열 할당) ──
    private Enemy FindClosestEnemy()
    {
        Vector3 myPos        = transform.position;
        float   rangeSqr     = data.detectRange * data.detectRange;
        float   closestSqr   = float.MaxValue;
        Enemy   closestEnemy = null;

        var enemies = Enemy.Active;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy e = enemies[i];
            if (e == null || e.isDead) continue;

            float sqr = (e.transform.position - myPos).sqrMagnitude;
            if (sqr > rangeSqr || sqr >= closestSqr) continue;

            closestSqr   = sqr;
            closestEnemy = e;
        }

        return closestEnemy;
    }

    void OnDrawGizmosSelected()
    {
        if (data == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, data.detectRange);
    }
}
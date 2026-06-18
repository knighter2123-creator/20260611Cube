using UnityEngine;

public class Companion : MonoBehaviour
{
    private CompanionData data;

    private ActiveSkill skill => data?.ownedSkill;

    private float skillTimer = 0f;
    private bool  isPlaced   = false;

    public bool          IsPlaced      => isPlaced;
    public string        CompanionName => data != null ? data.companionName : "unknown";
    public CompanionData Data          => data;
    public ActiveSkill   OwnedSkill    => skill;

    // 스킬에서 플레이어 스탯(크리티컬 확률/배율, baseDamage)을 참조하기 위한 프로퍼티
    public PlayerStat    Stat          => Player.Instance?.stat;

    public void Init(CompanionData companionData)
    {
        data = companionData;

        if (data.ownedSkill == null)
            Debug.LogWarning($"[Companion] {data.companionName}에 스킬이 없습니다.");
        else
            Debug.Log($"[Companion] {data.companionName} 초기화 — 스킬: {data.ownedSkill.skillName}");
    }

    void Update()
    {
        if (!isPlaced)    return;
        if (skill == null) return;

        skillTimer += Time.deltaTime;

        if (skillTimer >= skill.cooldown)
        {
            Enemy target = FindClosestEnemy();
            if (target != null)
            {
                skillTimer = 0f;
                skill.Execute(target, this);
            }
        }
    }

    // ──────────────────────────────────────────────
    //  배치 / 회수
    // ──────────────────────────────────────────────
    public void Place(Vector3 position)
    {
        transform.position = position;
        isPlaced           = true;
        skillTimer         = 0f;
        gameObject.SetActive(true);
        Debug.Log($"[Companion] {CompanionName} 배치 @ {position}");
    }

    public void Retrieve()
    {
        isPlaced = false;
        gameObject.SetActive(false);
        Debug.Log($"[Companion] {CompanionName} 회수");
    }

    // ──────────────────────────────────────────────
    //  적 탐지
    // ──────────────────────────────────────────────
    private Enemy FindClosestEnemy()
    {
        GameObject[] enemies      = GameObject.FindGameObjectsWithTag("Enemy");
        float        closestDist  = Mathf.Infinity;
        Enemy        closestEnemy = null;

        foreach (GameObject enemyObj in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
            if (dist > data.detectRange) continue;

            Enemy e = enemyObj.GetComponent<Enemy>();
            if (e == null || e.isDead) continue;

            if (dist < closestDist)
            {
                closestDist  = dist;
                closestEnemy = e;
            }
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
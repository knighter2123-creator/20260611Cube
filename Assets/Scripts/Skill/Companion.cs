using UnityEngine;

public class Companion : MonoBehaviour
{
    // ✅ ScriptableObject 데이터 참조
    private CompanionData data;
    private ActiveSkill   equippedSkill;

    private float skillTimer = 0f;
    private bool  isPlaced   = false;

    public bool           IsPlaced       => isPlaced;
    public string         CompanionName  => data != null ? data.companionName : "unknown";
    public CompanionData  Data           => data;
    public ActiveSkill    EquippedSkill  => equippedSkill;

    // ──────────────────────────────────────────────
    //  초기화 (CompanionManager에서 Instantiate 후 호출)
    // ──────────────────────────────────────────────
    public void Init(CompanionData companionData)
    {
        data = companionData;
    }

    // ──────────────────────────────────────────────
    void Update()
    {
        if (!isPlaced)      return;
        if (equippedSkill == null) return;

        skillTimer += Time.deltaTime;

        if (skillTimer >= equippedSkill.cooldown)
        {
            Enemy target = FindClosestEnemy();
            if (target != null)
            {
                skillTimer = 0f;
                equippedSkill.Execute(target, this);
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
        Debug.Log($"[Companion] {CompanionName} 배치 완료 @ {position}");
    }

    public void Retrieve()
    {
        isPlaced = false;
        gameObject.SetActive(false);
        Debug.Log($"[Companion] {CompanionName} 회수");
    }

    // ──────────────────────────────────────────────
    //  스킬 장착 / 해제
    // ──────────────────────────────────────────────
    public bool EquipSkill(ActiveSkill skill)
    {
        equippedSkill = skill;
        skillTimer    = 0f;
        Debug.Log($"[Companion] {CompanionName} 스킬 장착: {skill.skillName}");
        return true;
    }

    public void UnequipSkill()
    {
        Debug.Log($"[Companion] {CompanionName} 스킬 해제: {equippedSkill?.skillName}");
        equippedSkill = null;
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

            // ✅ data.detectRange 사용
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
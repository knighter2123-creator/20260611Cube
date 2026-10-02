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
        else
            Debug.Log($"[Companion] {data.companionName} 초기화 — 스킬: {data.ownedSkill.skillName}");
    }

    void Update()
    {
        if (!isPlaced)    return;
        if (skill == null) return;

        skillTimer += Time.deltaTime;

        // skill.cooldown(일반 값) 대신 '이 동료 등급·성급의' 쿨다운을 읽습니다.
        //   스킬 에셋은 여러 동료가 같이 쓰므로, 값을 에셋에 쓰지 않고 매번 골라 읽기만 합니다.
        //   (switch 와 사전 조회 한 번이라 매 프레임 불러도 성능 영향은 무시할 수준)
        //   성급이 오르면 다음 프레임부터 바로 새 쿨다운이 적용됩니다.
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
    // ★ [수정] FindGameObjectsWithTag + GetComponent → Enemy.Active 목록
    //   쿨다운이 찼는데 범위 안에 적이 없으면 이 함수는 '매 프레임' 불립니다.
    //   FindGameObjectsWithTag 는 부를 때마다 새 배열을 만들어 동료 수 × 프레임만큼 GC 쓰레기가 쌓였습니다.
    //   Enemy.Active 는 적이 OnEnable/OnDisable 에서 스스로 등록·해제하는 목록이라 할당이 없고,
    //   Player.FindTarget 과 같은 기준으로 적을 찾게 됩니다.
    //   거리 비교는 제곱 거리로 합니다 (Sqrt 생략 — 대소 비교 결과는 같음).
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
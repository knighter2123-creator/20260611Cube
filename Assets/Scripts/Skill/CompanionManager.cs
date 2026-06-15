using System.Collections.Generic;
using UnityEngine;

public class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance;

    [Header("동료 설정")]
    [SerializeField] private int maxCompanions = 6;

    [Header("배치 슬롯")]
    [SerializeField] private Transform[] placementSlots;

    // ✅ 보유 동료 데이터 목록
    private List<CompanionData> ownedCompanionData     = new List<CompanionData>();

    // ✅ 씬에 실제 생성된 Companion 인스턴스
    private List<Companion>     ownedCompanions        = new List<Companion>();

    // 슬롯별 배치 현황
    private Companion[] slotOccupants;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance      = this;
        slotOccupants = new Companion[placementSlots.Length];
    }

    // ──────────────────────────────────────────────
    //  동료 획득
    // ──────────────────────────────────────────────
    public bool AddCompanion(CompanionData data)
    {
        if (ownedCompanions.Count >= maxCompanions)
        {
            Debug.Log("[CompanionManager] 동료 슬롯이 가득 찼습니다. (최대 6명)");
            return false;
        }

        // ✅ data 자체 null 체크
        if (data == null)
        {
            Debug.LogError("[CompanionManager] CompanionData가 null입니다.");
            return false;
        }

        // ✅ prefab null 체크
        if (data.prefab == null)
        {
            Debug.LogError($"[CompanionManager] {data.companionName}의 prefab이 null입니다. CompanionData에 Prefab을 연결해주세요.");
            return false;
        }

        GameObject obj = Instantiate(data.prefab);

        // ✅ Companion 컴포넌트 null 체크
        Companion companion = obj.GetComponent<Companion>();
        if (companion == null)
        {
            Debug.LogError($"[CompanionManager] {data.prefab.name} 프리팹에 Companion 컴포넌트가 없습니다.");
            Destroy(obj);
            return false;
        }

        companion.Init(data);
        obj.SetActive(false);

        ownedCompanionData.Add(data);
        ownedCompanions.Add(companion);

        Debug.Log($"[CompanionManager] {data.companionName} 동료 획득");
        return true;
    }

    // ──────────────────────────────────────────────
    //  배치 / 회수
    // ──────────────────────────────────────────────
    public bool PlaceCompanion(Companion companion, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= placementSlots.Length)
        {
            Debug.LogWarning("[CompanionManager] 유효하지 않은 슬롯 인덱스");
            return false;
        }

        if (slotOccupants[slotIndex] != null)
        {
            Debug.Log($"[CompanionManager] 슬롯 {slotIndex}에 이미 동료가 배치되어 있습니다.");
            return false;
        }

        if (!ownedCompanions.Contains(companion))
        {
            Debug.LogWarning("[CompanionManager] 보유하지 않은 동료입니다.");
            return false;
        }

        RetrieveCompanion(companion);
        slotOccupants[slotIndex] = companion;
        companion.Place(placementSlots[slotIndex].position);
        return true;
    }

    public void RetrieveCompanion(Companion companion)
    {
        for (int i = 0; i < slotOccupants.Length; i++)
        {
            if (slotOccupants[i] != companion) continue;
            slotOccupants[i] = null;
            companion.Retrieve();
            return;
        }
    }

    // ──────────────────────────────────────────────
    //  스킬 장착
    // ──────────────────────────────────────────────
    public bool EquipSkillToCompanion(Companion companion, ActiveSkill skill)
    {
        if (!ownedCompanions.Contains(companion))
        {
            Debug.LogWarning("[CompanionManager] 보유하지 않은 동료입니다.");
            return false;
        }

        return companion.EquipSkill(skill);
    }

    // ──────────────────────────────────────────────
    //  조회
    // ──────────────────────────────────────────────
    public List<Companion>     GetOwnedCompanions()     => ownedCompanions;
    public List<CompanionData> GetOwnedCompanionData()  => ownedCompanionData;
    public Companion           GetSlotOccupant(int idx) => slotOccupants[idx];
    public bool                IsFull                   => ownedCompanions.Count >= maxCompanions;
}
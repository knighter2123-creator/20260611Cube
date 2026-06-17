using System.Collections.Generic;
using UnityEngine;

public class CompanionManager : MonoBehaviour
{
    public static CompanionManager Instance;

    [Header("동료 설정")]
    [SerializeField] private int maxCompanions = 6;

    [Header("배치 슬롯")]
    [SerializeField] private Transform[] placementSlots;

    private List<CompanionData> ownedCompanionData = new List<CompanionData>();
    private List<Companion>     ownedCompanions    = new List<Companion>();
    private Companion[]         slotOccupants;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance      = this;
        slotOccupants = new Companion[placementSlots.Length];
        DontDestroyOnLoad(gameObject);
    }

    // ──────────────────────────────────────────────
    //  동료 획득
    // ──────────────────────────────────────────────
    public bool AddCompanion(CompanionData data)
    {
        if (ownedCompanions.Count >= maxCompanions)
        {
            Debug.Log("[CompanionManager] 동료 슬롯이 가득 찼습니다.");
            return false;
        }
        if (data == null)
        {
            Debug.LogError("[CompanionManager] CompanionData가 null입니다.");
            return false;
        }
        if (data.prefab == null)
        {
            Debug.LogError($"[CompanionManager] {data.companionName}의 prefab이 null입니다.");
            return false;
        }

        GameObject obj      = Instantiate(data.prefab);
        Companion  companion = obj.GetComponent<Companion>();

        if (companion == null)
        {
            Debug.LogError($"[CompanionManager] {data.prefab.name}에 Companion 컴포넌트가 없습니다.");
            Destroy(obj);
            return false;
        }

        companion.Init(data);
        obj.SetActive(false);

        ownedCompanionData.Add(data);
        ownedCompanions.Add(companion);

        Debug.Log($"[CompanionManager] {data.companionName} 획득");
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
            Debug.Log($"[CompanionManager] 슬롯 {slotIndex}에 이미 동료가 있습니다.");
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
    
    public void RemoveCompanion(Companion companion)
    {
        if (!ownedCompanions.Contains(companion)) return;

        ownedCompanions.Remove(companion);
        if (ownedCompanionData != null)
            ownedCompanionData.Remove(companion.Data); // Data 프로퍼티 명칭에 맞게 조정

        Destroy(companion.gameObject);
        Debug.Log($"[CompanionManager] {companion.CompanionName} 제거");
    }

    // ──────────────────────────────────────────────
    //  조회
    // ──────────────────────────────────────────────
    public List<Companion>     GetOwnedCompanions()     => ownedCompanions;
    public List<CompanionData> GetOwnedCompanionData()  => ownedCompanionData;
    public Companion           GetSlotOccupant(int idx) => slotOccupants[idx];
    public bool                IsFull                   => ownedCompanions.Count >= maxCompanions;
    public int SlotCount => placementSlots.Length;
}
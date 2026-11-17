using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>인벤토리 설정 — Resources/InventorySettings.asset (없으면 기본값).</summary>
[CreateAssetMenu(fileName = "InventorySettings", menuName = "Game/Inventory Settings")]
public class InventorySettings : ScriptableObject
{
    public const string RESOURCE_PATH = "InventorySettings";

    [Header("뒤끝 게임 정보 테이블 (계정당 1행)")]
    public string tableName   = "INVENTORY_DATA";
    [Tooltip("map 컬럼 — { itemId: 수량 }")]
    public string itemsColumn = "items";
    [Tooltip("number 컬럼 — 저장할 때마다 +1 (응답이 끊긴 저장을 두 번 더하지 않게)")]
    public string revColumn   = "rev";

    [Header("뒤끝 차트 (아이템 정의)")]
    [Tooltip("차트 이름. 파일 ID 를 비우면 이 이름으로 찾음")]
    public string chartName   = "InventoryItem";
    [Tooltip("(선택) 차트 파일 ID — 넣으면 이름 찾기를 건너뜀 (차트를 다시 올리면 바꿔야 함)")]
    public string chartFileId = "";

    [Header("동기화 (초)")]
    [Min(0.5f)] public float uploadDelay   = 5f;
    [Min(5f)]   public float retryInterval = 60f;

    [Serializable]
    public struct IconEntry
    {
        public string key;
        public Sprite sprite;
    }

    [Header("아이콘 (차트 iconKey → 스프라이트, 조각은 비우면 동료 아이콘)")]
    public List<IconEntry> icons = new List<IconEntry>();

    [Header("차트에 행이 없을 때 표시")]
    [Tooltip("{name} = 동료 이름")]
    public string fragmentNameFormat     = "{name} 조각";
    public string fragmentDescription    = "동료의 성급을 올리거나 진화할 때 씁니다.";
    public string gachaTicketName        = "소환권";
    public string gachaTicketIconKey     = "gacha_ticket";
    public string gachaTicketDescription = "동료 소환 1회에 1장을 씁니다.";
    [Tooltip("{id} = 아이템 id")]
    public string unknownItemName        = "알 수 없는 아이템 ({id})";

    private static InventorySettings cached;

    public static InventorySettings Get()
    {
        if (cached != null) return cached;
        cached = Resources.Load<InventorySettings>(RESOURCE_PATH);
        if (cached == null)
        {
            Debug.LogWarning($"[Inventory] Resources/{RESOURCE_PATH}.asset 이 없어 기본 설정을 씁니다.");
            cached = CreateInstance<InventorySettings>();
        }
        return cached;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    public Sprite FindIcon(string key)
    {
        if (string.IsNullOrEmpty(key) || icons == null) return null;
        foreach (IconEntry e in icons)
            if (e.key == key) return e.sprite;
        return null;
    }
}

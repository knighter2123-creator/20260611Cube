using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 CompanionData를 id로 찾을 수 있게 모아두는 레지스트리.
/// 세이브에는 보유 동료를 id(문자열)로 저장하므로, 로드 시 id→데이터 복원에 사용한다.
/// 에셋을 하나 만들고(all 목록에 모든 CompanionData 연결) CompanionManager에 연결하세요.
/// </summary>
[CreateAssetMenu(fileName = "CompanionDatabase", menuName = "Companion/CompanionDatabase")]
public class CompanionDatabase : ScriptableObject
{
    public List<CompanionData> all = new List<CompanionData>();

    // id → 데이터 캐시. 처음 조회할 때 만들고, 인스펙터에서 목록을 고치면(OnValidate) 버립니다.
    private Dictionary<string, CompanionData> byId;

    public CompanionData GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (byId == null) BuildCache();
        return byId.TryGetValue(id, out CompanionData data) ? data : null;
    }

    private void BuildCache()
    {
        byId = new Dictionary<string, CompanionData>(all.Count);
        foreach (var c in all)
        {
            if (c == null || string.IsNullOrEmpty(c.id)) continue;

            // 기존 동작(목록 앞쪽 우선)을 유지하고, id 가 겹치면 알립니다 — 세이브에서 둘이 섞입니다.
            if (byId.TryGetValue(c.id, out CompanionData existing))
            {
                if (existing != c)
                    Debug.LogError($"[CompanionDatabase] '{existing.name}' 와 '{c.name}' 의 id 가 '{c.id}' 로 같습니다.", this);
                continue;
            }
            byId.Add(c.id, c);
        }
    }

    private void OnEnable()   => byId = null;   // 도메인 리로드 / 에셋 재로드 시 캐시 초기화
#if UNITY_EDITOR
    private void OnValidate() => byId = null;
#endif
}

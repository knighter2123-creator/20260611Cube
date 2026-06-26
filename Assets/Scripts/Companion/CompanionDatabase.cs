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

    public CompanionData GetById(string id)
    {
        foreach (var c in all)
            if (c != null && c.id == id) return c;
        return null;
    }
}
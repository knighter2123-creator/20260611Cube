using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 / 골드 / 보석 탭 토글. 선택된 탭의 패널만 켜고 나머지는 끈다.
/// 탭 추가 = 인스펙터의 tabs 리스트에 항목 1개 추가 (코드 수정 불필요).
///
/// [작동 순서]
///   Start: 각 탭 버튼에 "누르면 Select(내 번호)" 를 연결 → 기본 탭 선택
///   버튼 클릭 → Select(i): 모든 탭을 돌며 i 번만 켜고 나머지는 끔
///
/// [왜 "켤 것만 켜기" 가 아니라 "전부 돌며 켜고 끄기" 인가]
///   "이전 탭을 기억했다가 그것만 끄기" 는 기억한 값이 한 번 어긋나면 패널 두 개가 동시에 켜집니다.
///   매번 전체를 원하는 상태로 다시 맞추면 이전 상태가 무엇이었든 결과가 항상 같습니다.
///   (AugmentManager.RecalculatePermanent 의 "매번 처음부터 다시 계산" 과 같은 생각입니다)
/// </summary>
public class ShopTabController : MonoBehaviour
{
    // [Serializable] 을 붙여야 인스펙터 리스트에 펼쳐서 보입니다.
    [Serializable]
    public class Tab
    {
        public string id;               // "Gacha", "Gold", "Gem"
        public Button button;           // 탭 버튼
        public GameObject panel;        // 이 탭이 보여줄 패널
        public GameObject selectedMark; // 선택 시 켜질 하이라이트(선택 사항)
    }

    [SerializeField] private List<Tab> tabs = new();

    // ★ 기본 탭은 '번호' 가 아니라 'id' 로 정합니다.
    //   번호는 탭을 추가 · 삭제 · 순서 변경할 때마다 가리키는 탭이 바뀝니다.
    //   실제로 탭이 2개(0~1)로 줄었는데 defaultIndex 가 2 로 남아, 아래 Clamp 때문에 뽑기가 아닌 골드 탭이 열렸습니다.
    //   id 는 순서와 무관하게 같은 탭을 가리킵니다.
    [Tooltip("상점을 열었을 때 처음 보일 탭의 id (tabs 의 id 와 철자까지 같아야 함). 비우거나 못 찾으면 Default Index 를 씁니다.")]
    [SerializeField] private string defaultTabId = "Gacha_UI";

    [Tooltip("Default Tab Id 가 비어 있거나 찾지 못했을 때만 쓰는 예비 번호")]
    [Min(0)] [SerializeField] private int defaultIndex = 0;

    /// <summary>지금 선택된 탭 번호. 아직 선택 전이면 -1.</summary>
    public int CurrentIndex { get; private set; } = -1;

    private void Start()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;   // 클로저 캡처: for 의 i 는 루프 전체가 공유하는 변수 하나라, 그대로 쓰면 전부 마지막 값이 됨
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => Select(index));
        }

        if (tabs.Count == 0) return;

        Select(ResolveDefaultIndex());
    }

    /// <summary>처음 열 탭 번호. id 우선, 못 찾으면 예비 번호(범위 밖이면 경고 후 범위 안으로).</summary>
    private int ResolveDefaultIndex()
    {
        if (!string.IsNullOrEmpty(defaultTabId))
        {
            int byId = tabs.FindIndex(t => t.id == defaultTabId);
            if (byId >= 0) return byId;

            Debug.LogWarning($"[ShopTabController] 기본 탭 id '{defaultTabId}' 를 찾지 못해 Default Index({defaultIndex}) 를 씁니다. " +
                             "tabs 의 id 철자를 확인하세요.", this);
        }

        // 인스펙터에서 탭을 지운 뒤 defaultIndex 가 범위를 벗어나면 모든 패널이 꺼진 빈 화면이 됩니다.
        // 범위 안으로 당겨서 최소한 첫/마지막 탭은 보이게 하되, 조용히 넘어가지 않고 알립니다.
        if (defaultIndex >= tabs.Count)
            Debug.LogWarning($"[ShopTabController] Default Index({defaultIndex}) 가 탭 수({tabs.Count})를 넘어 마지막 탭을 엽니다.", this);

        return Mathf.Clamp(defaultIndex, 0, tabs.Count - 1);
    }

    public void Select(int index)
    {
        // 잘못된 번호면 아무것도 바꾸지 않습니다 (전부 꺼지는 것보다 지금 화면 유지가 낫습니다).
        if (index < 0 || index >= tabs.Count) return;

        CurrentIndex = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            bool on = (i == index);
            if (tabs[i].panel != null)        tabs[i].panel.SetActive(on);
            if (tabs[i].selectedMark != null) tabs[i].selectedMark.SetActive(on);
            if (tabs[i].button != null)       tabs[i].button.interactable = !on; // 선택 탭은 비활성
        }
    }

    /// <summary>id로 탭 전환 (추후 외부 호출용).</summary>
    public void SelectById(string id)
    {
        int idx = tabs.FindIndex(t => t.id == id);
        if (idx >= 0) Select(idx);
        else Debug.LogWarning($"[ShopTabController] '{id}' 탭을 찾지 못했습니다.", this);
    }
}

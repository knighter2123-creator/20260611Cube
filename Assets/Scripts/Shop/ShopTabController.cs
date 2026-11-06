using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>뽑기/골드/보석 탭 토글(탭 추가 = 인스펙터 tabs 에 항목 추가) — Select(i) 는 매번 전체를 돌며 i 만 켜고 나머지를 꺼서 이전 상태와 무관하게 결과가 같음.</summary>
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

    // 기본 탭은 번호가 아닌 id 로 (번호는 탭 추가·삭제·순서 변경 때 엉뚱한 탭을 가리킴)
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

        // 범위를 벗어나면 Clamp 후 경고 (빈 화면 방지)
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

}

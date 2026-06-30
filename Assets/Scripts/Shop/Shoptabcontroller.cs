using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 / 골드 / 보석 탭 토글. 선택된 탭의 패널만 켜고 나머지는 끈다.
/// 탭 추가 = 인스펙터의 tabs 리스트에 항목 1개 추가 (코드 수정 불필요).
/// </summary>
public class ShopTabController : MonoBehaviour
{
    [Serializable]
    public class Tab
    {
        public string id;               // "Gacha", "Gold", "Gem"
        public Button button;           // 탭 버튼
        public GameObject panel;        // 이 탭이 보여줄 패널
        public GameObject selectedMark; // 선택 시 켜질 하이라이트(선택 사항)
    }

    [SerializeField] private List<Tab> tabs = new();
    [SerializeField] private int defaultIndex = 0;

    private void Start()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;   // 클로저 캡처: i를 그대로 쓰면 전부 마지막 값이 됨
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => Select(index));
        }
        Select(defaultIndex);
    }

    public void Select(int index)
    {
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
    }
}
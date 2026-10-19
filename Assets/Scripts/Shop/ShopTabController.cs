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

        // 인스펙터에서 탭을 지운 뒤 defaultIndex 가 범위를 벗어나면 모든 패널이 꺼진 빈 화면이 됩니다.
        // 범위 안으로 당겨서 최소한 첫/마지막 탭은 보이게 합니다.
        Select(Mathf.Clamp(defaultIndex, 0, tabs.Count - 1));
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

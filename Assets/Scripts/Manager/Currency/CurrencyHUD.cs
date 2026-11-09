using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>재화 HUD: CurrencyManager 이벤트를 구독해 숫자 갱신 — OnEnable 과 Start 에서 두 번 시도하고 매니저가 없으면 에러로 알림 (조용히 포기하면 숫자가 멈춘 원인을 못 찾아서).</summary>
public class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI gemText;

    [Header("누를 때마다 줄임 표기(1.2K) <-> 정확한 수치(1,234) 전환")]
    [Tooltip("골드 버튼 (비우면 골드 글자에 버튼을 붙임). 아이콘까지 묶인 버튼을 넣으면 아이콘을 눌러도 됨")]
    [SerializeField] private Button goldButton;
    [Tooltip("보석 버튼 (비우면 보석 글자에 버튼을 붙임)")]
    [SerializeField] private Button gemButton;

    private bool goldDetail;   // true = 정확한 수치
    private bool gemDetail;

    private CurrencyManager bound;   // 실제로 구독한 대상
    private bool reportedMissing;    // 에러 로그는 1회만

    private bool started;            // Start 를 지났는가 — 그 전의 실패는 Awake 순서 문제일 수 있어 에러로 보지 않음

    private void Awake()
    {
        Hook(goldButton, goldText, ToggleGold);
        Hook(gemButton,  gemText,  ToggleGem);
    }

    private void OnEnable() => TryBind();

    // 버튼이 비어 있으면 글자에 버튼을 붙임 — 씬마다 손으로 붙이지 않아도 되게
    private static void Hook(Button button, TextMeshProUGUI text, UnityEngine.Events.UnityAction onClick)
    {
        if (button == null)
        {
            if (text == null) return;
            text.raycastTarget = true;   // 글자는 기본으로 클릭을 안 받는 경우가 많음
            button = text.GetComponent<Button>();
            if (button == null)
            {
                button = text.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;   // 누를 때 글자 색이 바뀌지 않게
            }
        }
        button.onClick.AddListener(onClick);
    }

    private void ToggleGold()
    {
        goldDetail = !goldDetail;
        if (bound != null) UpdateGold(bound.Gold);
    }

    private void ToggleGem()
    {
        gemDetail = !gemDetail;
        if (bound != null) UpdateGem(bound.Gem);
    }

    // Start 에서 한 번 더 — 모든 Awake 가 끝난 뒤라 매니저가 있으면 반드시 잡힘
    private void Start()
    {
        started = true;
        TryBind();
    }

    private void OnDisable() => Unbind();

    private void TryBind()
    {
        if (bound != null) return;   // 이미 구독함

        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            if (started && !reportedMissing)
            {
                reportedMissing = true;
                Debug.LogError("[CurrencyHUD] CurrencyManager.Instance 가 없습니다. " +
                               "LoginScene 부터 실행했는지, 그리고 Login→Main 왕복 중에 " +
                               "Managers 루트가 파괴되지 않았는지 확인하세요.", this);
            }
            return;
        }

        bound = cm;

        cm.OnGoldChanged += UpdateGold;
        cm.OnGemChanged  += UpdateGem;

        // 매니저가 아직 세이브를 안 읽었으면 여기서 깨움
        cm.EnsureLoaded();

        UpdateGold(cm.Gold);
        UpdateGem(cm.Gem);
    }

    private void Unbind()
    {
        if (bound == null) return;

        // Instance 가 아니라 구독한 bound 에서 해제 (그 사이 Instance 가 바뀌면 원래 구독이 누수)
        bound.OnGoldChanged -= UpdateGold;
        bound.OnGemChanged  -= UpdateGem;
        bound = null;
    }

    // 표기는 NumberFormat 한 곳에 맡김 — 보유량과 비용이 같은 자릿수 체계로 보여야 살 수 있는지 바로 알 수 있어서
    // 정확한 수치 모드면 쉼표 표기
    private void UpdateGold(int value) { if (goldText != null) goldText.text = Format(value, goldDetail); }
    private void UpdateGem(int value)  { if (gemText  != null) gemText.text  = Format(value, gemDetail); }

    private static string Format(int value, bool detail) => detail ? NumberFormat.Comma(value) : NumberFormat.Short(value);
}
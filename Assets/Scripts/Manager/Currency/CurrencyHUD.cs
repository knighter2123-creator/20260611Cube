using UnityEngine;
using TMPro;

/// <summary>재화 HUD: CurrencyManager 이벤트를 구독해 숫자 갱신 — OnEnable 과 Start 에서 두 번 시도하고 매니저가 없으면 에러로 알림 (조용히 포기하면 숫자가 멈춘 원인을 못 찾아서).</summary>
public class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI gemText;

    private CurrencyManager bound;   // 실제로 구독한 대상
    private bool reportedMissing;    // 에러 로그는 1회만

    private void OnEnable() => TryBind();

    // Start 에서 한 번 더 — 모든 Awake 가 끝난 뒤라 매니저가 있으면 반드시 잡힘
    private void Start() => TryBind();

    private void OnDisable() => Unbind();

    private void TryBind()
    {
        if (bound != null) return;   // 이미 구독함

        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            if (!reportedMissing)
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
    private void UpdateGold(int value) { if (goldText != null) goldText.text = NumberFormat.Short(value); }
    private void UpdateGem(int value)  { if (gemText  != null) gemText.text  = NumberFormat.Short(value); }
}
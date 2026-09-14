using UnityEngine;
using TMPro;

/// <summary>
/// 재화 표시 HUD. CurrencyManager 의 이벤트를 구독해 숫자를 갱신한다.
///
/// [고친 것 — 조용한 포기]
///   원래는 OnEnable 에서 이렇게 끝났습니다.
///
///     var cm = CurrencyManager.Instance;
///     if (cm == null) return;      // ← 로그도 없고 재시도도 없음
///
///   매니저가 아직(또는 영영) 없으면 구독이 안 된 채로 끝나고,
///   숫자는 프리팹에 적혀 있던 문자열 그대로 화면에 남습니다.
///   적을 잡아 AddGold 가 돌아도 HUD 는 구독을 안 했으니 미동도 없습니다.
///
///   "수치가 1에 고정되고 AddGold 가 안 먹는다"의 정체가 이것입니다.
///   재화가 안 오르는 게 아니라, 화면이 재화를 보고 있지 않은 것입니다.
///
///   실패는 조용하면 안 됩니다. 재시도하고, 그래도 안 되면 크게 알립니다.
///   (GuideQuestManager.TryBindLevelUp 이 쓰는 것과 같은 패턴입니다)
/// </summary>
public class CurrencyHUD : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI gemText;

    private CurrencyManager bound;   // 실제로 구독한 대상
    private bool reportedMissing;    // 에러 로그는 1회만

    private void OnEnable() => TryBind();

    // Start 에서 한 번 더 시도한다.
    // OnEnable 은 다른 오브젝트의 Awake 보다 먼저 돌 수 있지만,
    // Start 는 모든 Awake 가 끝난 뒤라 매니저가 존재한다면 반드시 잡힌다.
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

        // 매니저가 아직 세이브를 안 읽었다면 여기서 깨운다.
        // (Start 한 번에 의존하지 않도록 CurrencyManager 에 EnsureLoaded 를 열어뒀다)
        cm.EnsureLoaded();

        UpdateGold(cm.Gold);
        UpdateGem(cm.Gem);
    }

    private void Unbind()
    {
        if (bound == null) return;

        // ★ Instance 가 아니라 bound 를 쓰는 이유
        //   구독한 대상과 해제할 대상은 반드시 같아야 합니다.
        //   그 사이에 Instance 가 다른 객체로 바뀌었다면,
        //   Instance 에서 빼는 건 엉뚱한 곳을 건드리는 것이고
        //   원래 구독은 그대로 남아 누수가 됩니다.
        bound.OnGoldChanged -= UpdateGold;
        bound.OnGemChanged  -= UpdateGem;
        bound = null;
    }

    // ★ 표기 규칙을 이 파일에 두지 않고 KoreanNumberFormatter 에 맡깁니다.
    //
    //   [왜 자체 Format 함수를 없앴나]
    //   원래 여기에 1000 단위 K/M/G/T 변환이 있었습니다. 그런데 강화 비용은
    //   KoreanNumberFormatter 로 "73만 2800" 처럼 나오다 보니, 한 화면에
    //   "보유 1.4M / 비용 73만 2800" 이 동시에 떴습니다.
    //   자릿수 체계가 둘이면 유저가 살 수 있는지를 암산해야 합니다.
    //
    //   같은 개념의 규칙이 두 곳에 각각 적혀 있으면 반드시 어긋납니다.
    //   나중에 "억부터는 소수점 한 자리" 같은 걸 넣고 싶어지면
    //   KoreanNumberFormatter 한 곳만 고치면 골드·젬·강화 비용이 동시에 따라옵니다.
    private void UpdateGold(int value) { if (goldText != null) goldText.text = KoreanNumberFormatter.Format(value); }
    private void UpdateGem(int value)  { if (gemText  != null) gemText.text  = KoreanNumberFormatter.Format(value); }
}
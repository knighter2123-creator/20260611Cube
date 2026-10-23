using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 HUD — 지금 배치 기준으로 어떤 시너지가 몇 명째인지 보여줍니다.
///
///   [간단히]  딜러 3/4   제어 1/2 ...        (켜진 시너지는 계열 색, 꺼진 건 회색)
///   [자세히]  탭하면 단계별 효과까지 펼칩니다. 다시 탭하면 접힙니다.
///             다음 단계 목표도 보이도록 모든 단계를 표시합니다(적용 중인 단계만 밝게).
///             StageScene 에서는 Safe Panel 안 오른쪽 위에 오른쪽 정렬로 둡니다 — Safe Panel 이 카메라 구멍을 피해 주므로
///             화면이 어느 쪽으로 돌아가도 가려지지 않습니다.
///
/// [붙이는 곳] StageScene 의 항상 켜져 있는 오브젝트 (예: 다른 HUD 옆).
///   · Label 에 Canvas 아래의 TextMeshPro - Text(UI) 를 연결합니다.
///     폰트 · 크기 · 위치 · 정렬은 그 텍스트의 인스펙터에서 정합니다.
///   · ★ Label 에 ContentSizeFitter(Preferred Size) 를 붙여 두세요.
///     큰 고정 영역을 두면 빈 부분까지 탭을 먹어서 그 아래 맵 칸에 배치가 안 되는 것처럼 보입니다.
///
/// ★ 배치 중에는 탭을 받지 않습니다.
///   CompanionPlacementController 는 "탭 좌표에 UI 가 있으면 무시" 하므로,
///   이 HUD 가 탭을 받는 상태로 남아 있으면 HUD 아래 칸에는 동료를 놓을 수 없습니다.
///
/// ★ TabWindow(Player Total 등)가 열려 있는 동안에도 탭을 받지 않습니다.
///   이 HUD 가 창 위에 그려지는 자리라, 창 안의 버튼을 눌러도 시너지 설명이 대신 펼쳐지기 때문입니다.
/// </summary>
public class SynergyHUD : MonoBehaviour
{
    [Header("표시 텍스트")]
    [Tooltip("★ 한글이 들어 있는 폰트를 쓰는 TMP 텍스트. 기본 폰트면 한글이 □ 로 보일 수 있습니다")]
    [SerializeField] private TMP_Text label;

    [Header("외곽선 (밝은 맵 위에서도 읽히도록)")]
    [Tooltip("0 이면 손대지 않습니다 — 머티리얼 프리셋으로 외곽선을 관리할 때 0 으로 두세요")]
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.2f;
    [SerializeField] private Color32 outlineColor = new Color32(0, 0, 0, 200);

    [Header("동작")]
    [Tooltip("탭해서 단계별 효과를 펼치고 접습니다")]
    [SerializeField] private bool  tapToToggleDetail = true;
    [Tooltip("배치된 동료가 없을 때 '시너지 없음' 을 표시합니다. 끄면 아무것도 표시하지 않습니다")]
    [SerializeField] private bool  showWhenEmpty     = true;
    [SerializeField] private Color inactiveColor     = new Color(0.6f, 0.6f, 0.6f);

    private CompanionManager             bound;           // 실제로 구독한 대상 (CurrencyHUD 와 같은 이유)
    private CompanionPlacementController boundPlacement;
    private Button                       toggleButton;
    private bool                         detailed;
    private bool                         placing;          // 동료 배치 중인가
    private bool                         reportedMissing;

    private readonly StringBuilder sb = new StringBuilder(256);

    // ══════════════════════════════════════════════
    //  라이프사이클
    // ══════════════════════════════════════════════

    private void OnEnable()
    {
        TryBind();
        TryBindPlacement();   // 껐다 켰을 때 OnDisable 에서 푼 구독을 되살립니다
        TabWindow.AnyOpenChanged += UpdateRaycastable;
        UpdateRaycastable();
    }

    // OnEnable 은 매니저 Awake 보다 먼저 돌 수 있어 Start 에서 한 번 더 시도합니다 (CurrencyHUD 와 같은 패턴).
    private void Start()
    {
        EnsureLabel();
        TryBind();
        TryBindPlacement();
        Refresh();
    }

    private void OnDisable()
    {
        Unbind();
        UnbindPlacement();
        TabWindow.AnyOpenChanged -= UpdateRaycastable;
    }

    private void OnDestroy()
    {
        if (toggleButton != null) toggleButton.onClick.RemoveListener(ToggleDetail);
    }

    // ══════════════════════════════════════════════
    //  구독
    // ══════════════════════════════════════════════

    private void TryBind()
    {
        if (bound != null) return;

        CompanionManager cm = CompanionManager.Instance;
        if (cm == null)
        {
            if (!reportedMissing)
            {
                reportedMissing = true;
                Debug.LogWarning("[SynergyHUD] CompanionManager.Instance 가 아직 없습니다. LoginScene 부터 실행했는지 확인하세요.", this);
            }
            return;
        }

        bound = cm;
        bound.OnSynergyChanged += HandleSynergyChanged;
        Refresh();   // ★ 구독 전에 일어난 변화를 놓치지 않도록 지금 값으로 한 번 그립니다
    }

    private void Unbind()
    {
        if (bound == null) return;
        bound.OnSynergyChanged -= HandleSynergyChanged;
        bound = null;
    }

    private void TryBindPlacement()
    {
        if (boundPlacement != null) return;

        boundPlacement = CompanionPlacementController.Instance;
        if (boundPlacement == null) return;   // 배치 기능이 없는 씬이면 그냥 둡니다

        boundPlacement.OnPlacementBegan += HandlePlacementBegan;
        boundPlacement.OnPlacementEnded += HandlePlacementEnded;
        placing = boundPlacement.IsPlacing;
        UpdateRaycastable();
    }

    private void UnbindPlacement()
    {
        if (boundPlacement == null) return;
        boundPlacement.OnPlacementBegan -= HandlePlacementBegan;
        boundPlacement.OnPlacementEnded -= HandlePlacementEnded;
        boundPlacement = null;
        placing = false;
    }

    private void HandleSynergyChanged(SynergyResult _) => Refresh();
    private void HandlePlacementBegan() { placing = true;  UpdateRaycastable(); }
    private void HandlePlacementEnded() { placing = false; UpdateRaycastable(); }

    /// <summary>배치 중이거나 TabWindow 가 열려 있으면 탭을 받지 않습니다 (클래스 주석 참고).</summary>
    private void UpdateRaycastable()
    {
        if (label != null) label.raycastTarget = tapToToggleDetail && !placing && !TabWindow.AnyOpen;
    }

    // ══════════════════════════════════════════════
    //  그리기
    // ══════════════════════════════════════════════

    private void ToggleDetail()
    {
        detailed = !detailed;
        Refresh();
    }

    public void Refresh()
    {
        if (label == null) return;

        // Instance 가 아니라 bound 를 읽습니다 — 구독한 매니저와 그리는 매니저가 같아야 합니다.
        SynergyResult r = bound != null ? bound.Synergy : SynergyResult.Empty;

        sb.Clear();
        bool anyShown = false;

        foreach (SynergyState s in r.States)
        {
            if (s.Count <= 0) continue;   // 한 명도 없는 시너지는 줄을 차지하지 않습니다
            anyShown = true;

            AppendHeader(s);
            if (detailed) AppendTiers(s);
        }

        if (!anyShown && showWhenEmpty)
            sb.Append(Colored(inactiveColor, "시너지 없음"));

        label.text = sb.ToString();
    }

    /// <summary>"딜러 3/4" — 켜졌으면 계열 색 + 굵게, 아니면 회색.</summary>
    private void AppendHeader(SynergyState s)
    {
        if (sb.Length > 0) sb.Append('\n');

        string text = $"{s.Definition.displayName} {s.Count}/{s.DisplayGoal}";
        if (s.IsActive) sb.Append("<b>").Append(Colored(s.Definition.DisplayColor, text)).Append("</b>");
        else            sb.Append(Colored(inactiveColor, text));
    }

    /// <summary>단계별 효과. 적용 중인 단계만 밝게.</summary>
    private void AppendTiers(SynergyState s)
    {
        if (s.Definition.tiers == null) return;

        foreach (SynergyTier t in s.Definition.tiers)
        {
            if (t == null) continue;

            sb.Append('\n');
            bool current = ReferenceEquals(t, s.ActiveTier);

            sb.Append("<size=80%>");
            sb.Append(current ? "<color=#FFFFFF>" : $"<color=#{ColorUtility.ToHtmlStringRGB(inactiveColor)}>");
            sb.Append(t.requiredCount).Append("명: ");

            if (t.effects != null)
            {
                for (int i = 0; i < t.effects.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(SynergyStatText.Describe(t.effects[i].stat, t.effects[i].value));
                }
            }
            sb.Append("</color></size>");
        }
    }

    private static string Colored(Color c, string text)
        => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{text}</color>";

    // ══════════════════════════════════════════════
    //  텍스트 준비
    // ══════════════════════════════════════════════

    private void EnsureLabel()
    {
        if (label == null)
        {
            Debug.LogWarning("[SynergyHUD] Label 이 비어 있어 시너지를 표시하지 않습니다. Canvas 아래의 TMP 텍스트를 연결하세요.", this);
            return;
        }

        label.richText = true;   // 색 · 굵기 · 크기를 태그로 넣기 때문에 꺼져 있으면 태그가 그대로 보입니다

        if (outlineWidth > 0f)
        {
            label.outlineWidth = outlineWidth;
            label.outlineColor = outlineColor;
        }

        if (tapToToggleDetail && toggleButton == null)
        {
            // TMP_Text 도 Graphic 이라 그 자체로 탭을 받을 수 있습니다. 버튼 색 전환은 글자색을 덮어쓰므로 끕니다.
            toggleButton = label.GetComponent<Button>();
            if (toggleButton == null) toggleButton = label.gameObject.AddComponent<Button>();
            toggleButton.transition    = Selectable.Transition.None;
            toggleButton.targetGraphic = label;
            toggleButton.onClick.AddListener(ToggleDetail);
        }

        UpdateRaycastable();
    }
}

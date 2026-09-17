using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 격자의 아이콘 한 칸. (신규)
///
/// ★ 이 칸은 '재사용' 됩니다.
///   CompanionCodexUI 는 탭을 열 때마다 칸을 부수고 새로 만들지 않고,
///   이미 있는 칸에 Bind() 로 내용만 갈아 끼웁니다.
///   그래서 버튼 리스너는 Bind 가 아니라 Init 에서 '딱 한 번' 겁니다 — 아래 설명 참고.
/// </summary>
[RequireComponent(typeof(Button))]
public class CompanionCodexItem : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private Image    iconImage;
    [Tooltip("등급 색으로 칠할 테두리/배경 (선택)")]
    [SerializeField] private Image    gradeFrame;
    [SerializeField] private TMP_Text nameText;
    [Tooltip("미획득일 때 켤 오브젝트 (자물쇠, '?' 등. 선택)")]
    [SerializeField] private GameObject lockedMark;

    [Header("미획득 표시")]
    [Tooltip("미획득 동료 아이콘에 곱할 색. 어두울수록 실루엣처럼 보입니다.")]
    [SerializeField] private Color lockedIconColor = new Color(0.15f, 0.15f, 0.15f, 1f);
    [Tooltip("체크하면 미획득 동료의 이름을 '???' 로 가립니다")]
    [SerializeField] private bool hideNameWhenLocked = false;

    // 현재 이 칸이 보여주는 내용
    private CompanionData data;
    private bool          owned;

    private Button button;
    private Action<CompanionData, bool> onClicked;
    private bool   initialized;

    /// <summary>
    /// 생성 직후 한 번만 호출. 클릭 시 알릴 곳을 받고 버튼 리스너를 겁니다.
    ///
    /// ★ 왜 Bind 에서 AddListener 하면 안 되나
    ///   Bind 는 탭을 열 때마다 불립니다. 거기서 AddListener 를 하면
    ///   세 번 연 뒤에는 리스너가 3개 → 한 번 누르면 상세창이 3번 열립니다.
    ///   겉보기엔 멀쩡해서(같은 창이 3번 그려질 뿐) 오래 숨어 있는 종류의 버그입니다.
    ///   그래서 리스너는 '고정된 함수 하나(HandleClick)' 만 걸고,
    ///   그 함수가 '지금 담긴 data' 를 읽게 만듭니다. 내용이 바뀌어도 리스너는 그대로입니다.
    ///
    /// ★ 왜 Awake 가 아니라 Init 인가
    ///   프리팹을 꺼진 상태로 저장해 두면 Instantiate 직후 Awake 가 돌지 않습니다.
    ///   만든 쪽이 직접 부르는 Init 은 그런 조건과 상관없이 확실히 실행됩니다.
    /// </summary>
    public void Init(Action<CompanionData, bool> clickCallback)
    {
        onClicked = clickCallback;

        if (initialized) return;   // 두 번 불려도 리스너가 중복되지 않게
        initialized = true;

        button = GetComponent<Button>();
        button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        // 등록한 리스너는 등록한 쪽이 해제합니다.
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    /// <summary>이 칸에 동료 하나를 표시합니다. (재사용할 때마다 호출)</summary>
    public void Bind(CompanionData companion, bool isOwned)
    {
        data  = companion;
        owned = isOwned;

        if (iconImage != null)
        {
            iconImage.sprite  = companion.icon;
            // ★ 아이콘이 비어 있는 에셋이면 흰 사각형이 보이므로 아예 숨깁니다.
            iconImage.enabled = companion.icon != null;

            // ★ UI Image 의 color 는 스프라이트 색에 '곱해집니다'.
            //   흰색(1,1,1) = 원본 그대로, 어두운 회색 = 모양만 남은 실루엣.
            //   셰이더 없이 실루엣을 만드는 가장 싼 방법입니다.
            iconImage.color = owned ? Color.white : lockedIconColor;

            // ★ 원본 비율 유지 (정사각형이 아닌 아이콘이 찌그러지지 않게)
            iconImage.preserveAspect = true;
        }

        if (gradeFrame != null)
            gradeFrame.color = CompanionGradeStyle.GetColor(companion.grade);

        if (nameText != null)
            nameText.text = (!owned && hideNameWhenLocked) ? "???" : companion.companionName;

        if (lockedMark != null)
            lockedMark.SetActive(!owned);
    }

    private void HandleClick()
    {
        if (data == null) return;
        // onClicked 는 C# 델리게이트라 ?. 를 써도 안전합니다.
        // (UnityEngine.Object 에는 ?. 를 쓰면 안 됩니다 — CompanionCodexUI 주석 참고)
        onClicked?.Invoke(data, owned);
    }
}

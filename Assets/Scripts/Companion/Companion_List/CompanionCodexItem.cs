using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>도감 격자 아이콘 한 칸(재사용) — 탭을 열 때마다 Bind 로 내용만 바꾸고 버튼 리스너는 Init 에서 한 번만 검.</summary>
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

    /// <summary>생성 직후 1회: 클릭 콜백을 받고 고정 리스너(HandleClick, 지금 담긴 data 를 읽음)를 검 — Bind 에서 걸면 열 때마다 쌓이고, 꺼진 프리팹은 Awake 가 안 돌아 Init.</summary>
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
        // 지금은 도감이 null 을 걸러서 넘기지만, 다른 곳에서 불러도 칸이 NullReferenceException 으로 멈추지 않게.
        if (companion == null)
        {
            Debug.LogWarning("[CompanionCodexItem] Bind 에 null 동료가 들어왔습니다.", this);
            return;
        }

        data  = companion;
        owned = isOwned;

        if (iconImage != null)
        {
            iconImage.sprite  = companion.icon;
            // ★ 아이콘이 비어 있는 에셋이면 흰 사각형이 보이므로 아예 숨깁니다.
            iconImage.enabled = companion.icon != null;

            // Image.color 는 스프라이트에 곱해짐 — 어두운 회색이면 셰이더 없이 실루엣
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
        // C# 델리게이트라 ?. 안전 (UnityEngine.Object 엔 쓰면 안 됨)
        onClicked?.Invoke(data, owned);
    }
}

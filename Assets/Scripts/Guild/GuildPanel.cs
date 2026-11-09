using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>길드 창: 길드가 없으면 [목록·검색·만들기], 있으면 [길드 정보 + 멤버/가입 신청 탭]. GuildManager 를 그리기만 하고 OnChanged 때 다시 그림.</summary>
// [UI] StageScene 에서 Tools/UI 굽기/길드 창(GuildUIBaker) 으로 한 번 만들고 씬 저장 — 굽기 스크립트는 그 뒤 지워도 됨
public class GuildPanel : MonoBehaviour
{
    [Header("공통")]
    [SerializeField] private GameObject      root;
    [SerializeField] private Button          dimButton;
    [SerializeField] private Button          closeButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("작업 결과·오류 한 줄 (몇 초 뒤 사라짐)")]
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private GuildRow        rowTemplate;

    [Header("길드 없음 — 목록 · 검색")]
    [SerializeField] private GameObject      noGuildView;
    [SerializeField] private TMP_InputField  searchInput;
    [SerializeField] private Button          searchButton;
    [SerializeField] private Button          showAllButton;
    [SerializeField] private RectTransform   browseContent;
    [SerializeField] private TextMeshProUGUI browseStatus;
    [SerializeField] private Button          moreButton;
    [SerializeField] private Button          openCreateButton;

    [Header("길드 만들기")]
    [SerializeField] private GameObject      createView;
    [SerializeField] private TMP_InputField  createNameInput;
    [SerializeField] private TMP_InputField  createIntroInput;
    [SerializeField] private TextMeshProUGUI createCostText;
    [SerializeField] private Button          createConfirmButton;
    [SerializeField] private Button          createCancelButton;

    [Header("내 길드")]
    [SerializeField] private GameObject      guildView;
    [SerializeField] private TextMeshProUGUI guildNameText;
    [SerializeField] private TextMeshProUGUI guildInfoText;
    [SerializeField] private TextMeshProUGUI guildIntroText;
    [SerializeField] private Button          membersTab;
    [SerializeField] private Button          applicantsTab;
    [SerializeField] private TextMeshProUGUI applicantsTabLabel;
    [SerializeField] private RectTransform   memberContent;
    [SerializeField] private TextMeshProUGUI memberStatus;
    [SerializeField] private Button          editIntroButton;
    [SerializeField] private Button          autoApproveButton;
    [SerializeField] private TextMeshProUGUI autoApproveLabel;
    [SerializeField] private Button          leaveButton;

    [Header("확인 · 입력 창")]
    [SerializeField] private GameObject      dialog;
    [SerializeField] private TextMeshProUGUI dialogMessage;
    [SerializeField] private TMP_InputField  dialogInput;
    [SerializeField] private Button          dialogOk;
    [SerializeField] private Button          dialogCancel;

    [Header("설정")]
    [Tooltip("길드를 만들 때 내는 보석 (0 = 무료). 서버에 길드가 생기지 않으면 돌려줌")]
    [SerializeField, Min(0)] private int createGemCost = 500;
    [SerializeField, Min(1)] private int nameMaxLength  = 12;
    [SerializeField, Min(1)] private int introMaxLength = 60;
    [Tooltip("작업 결과 한 줄이 보이는 시간(초, 실제 시간)")]
    [SerializeField] private float feedbackSeconds = 3f;

    [Header("굽기 설정 (GuildUIBaker 가 UI 를 만들 때만 씀)")]
    [Tooltip("한글이 들어 있는 TMP 폰트. 비우면 씬에서 가장 많이 쓰는 폰트.")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int sortingOrder = 500;

    [Header("글자 (실행 중에 코드가 넣는 값) — {이름} 자리는 코드가 채움")]
    [SerializeField] private string titleNoGuild     = "길드";
    [SerializeField] private string titleCreate      = "길드 만들기";
    [SerializeField] private string loadingText      = "불러오는 중...";
    [SerializeField] private string noGuildsText     = "아직 길드가 없습니다. 첫 길드를 만들어 보세요!";
    [SerializeField] private string noSearchText     = "그 이름의 길드가 없습니다.";
    [SerializeField] private string noApplicantsText = "받은 가입 신청이 없습니다.";
    [Tooltip("{master} {count}")]
    [SerializeField] private string guildRowSub      = "길드장 {master} | 멤버 {count}명";
    [Tooltip("{master} {count} {auto}")]
    [SerializeField] private string guildInfoFormat  = "길드장 {master} | 멤버 {count}명 | {auto}";
    [Tooltip("멤버 줄: 직책과 최근 접속 사이 (⚠ 폰트에 있는 글자만 — BMJUA 에는 · 가 없음)")]
    [SerializeField] private string separator        = " | ";
    [SerializeField] private string autoOnText       = "자동 가입";
    [SerializeField] private string autoOffText      = "승인 후 가입";
    [SerializeField] private string emptyIntroText   = "길드 소개가 없습니다.";
    [Tooltip("{cost}")]
    [SerializeField] private string costFormat       = "필요한 보석: {cost}";
    [SerializeField] private string freeText         = "무료로 만들 수 있습니다.";
    [Tooltip("{count}")]
    [SerializeField] private string applicantsTabFormat = "가입 신청 {count}";
    [SerializeField] private string applicantsTabEmpty  = "가입 신청";

    [Header("글자 — 직책 · 접속")]
    [SerializeField] private string roleMaster  = "길드장";
    [SerializeField] private string roleVice    = "부길드장";
    [SerializeField] private string roleMember  = "멤버";
    [SerializeField] private string meSuffix    = " (나)";
    [Tooltip("{date}")]
    [SerializeField] private string lastLoginFormat = "최근 접속 {date}";

    [Header("글자 — 버튼")]
    [SerializeField] private string joinLabel     = "가입 신청";
    [SerializeField] private string joinAutoLabel = "가입";
    [SerializeField] private string appliedLabel  = "신청함";
    [SerializeField] private string approveLabel  = "승인";
    [SerializeField] private string declineLabel  = "거절";
    [SerializeField] private string kickLabel     = "추방";
    [SerializeField] private string masterLabel   = "길드장 위임";
    [SerializeField] private string viceLabel     = "부길드장";
    [SerializeField] private string unviceLabel   = "부길드장 해제";
    [SerializeField] private string autoOnButton  = "자동 가입: 켜짐";
    [SerializeField] private string autoOffButton = "자동 가입: 꺼짐";

    [Header("글자 — 확인 창")]
    [SerializeField] private string confirmLeave        = "길드를 탈퇴할까요?";
    [Tooltip("{name}")]
    [SerializeField] private string confirmKick         = "{name} 님을 길드에서 추방할까요?";
    [Tooltip("{name}")]
    [SerializeField] private string confirmMaster       = "{name} 님에게 길드장을 넘길까요?\n넘기면 되돌릴 수 없습니다.";
    [Tooltip("{cost}")]
    [SerializeField] private string confirmCreate       = "보석 {cost}개를 써서 길드를 만들까요?";
    [SerializeField] private string editIntroPrompt     = "길드 소개를 입력하세요.";
    [SerializeField] private string doneText            = "완료했습니다.";

    public TMP_FontAsset Font         => font;
    public int           SortingOrder => sortingOrder;

    // GuildUIBaker 가 Row 템플릿을 구울 때만 쓰는 색 — 구운 뒤에는 하이어라키의 Row 템플릿에서 직접 고치세요
    public static readonly Color RowColor    = new Color(1f, 1f, 1f, 0.04f);
    public static readonly Color MasterColor = new Color(1f, 0.85f, 0.2f);
    public static readonly Color ViceColor   = new Color(0.55f, 0.8f, 1f);

    // 줄은 지우지 않고 다시 씀 (Refresh 한 번에 OnChanged 가 여러 번 오고 멤버는 최대 100줄)
    private sealed class RowPool
    {
        public readonly List<GuildRow> Rows = new List<GuildRow>();
        public int Used;
    }
    private readonly RowPool browseRows = new RowPool();
    private readonly RowPool memberRows = new RowPool();
    private bool   ready;
    private bool   creating;            // 만들기 화면
    private bool   showApplicants;      // 가입 신청 탭
    private float  feedbackUntil;
    private Action<string> dialogAction;   // 확인 시 (입력 칸 값)

    public bool IsOpen => root != null && root.activeSelf;

    private void Awake()
    {
        ready = root != null && rowTemplate != null && noGuildView != null && guildView != null && createView != null
             && browseContent != null && memberContent != null;
        if (!ready)
        {
            Debug.LogError("[GuildPanel] UI 칸이 비어 있습니다. StageScene 을 열고 Tools/UI 굽기/길드 창 메뉴로 만든 뒤 씬을 저장하세요.", this);
            return;
        }

        if (dimButton    != null) dimButton.onClick.AddListener(Close);
        if (closeButton  != null) closeButton.onClick.AddListener(Close);
        if (searchButton != null) searchButton.onClick.AddListener(() => GuildManager.Search(searchInput != null ? searchInput.text : null));
        if (searchInput  != null) searchInput.onSubmit.AddListener(s => GuildManager.Search(s));
        if (showAllButton != null) showAllButton.onClick.AddListener(() =>
        {
            if (searchInput != null) searchInput.text = string.Empty;
            GuildManager.LoadGuildList();
        });
        if (moreButton       != null) moreButton.onClick.AddListener(() => GuildManager.LoadGuildList(more: true));
        if (openCreateButton != null) openCreateButton.onClick.AddListener(() => { creating = true; Draw(); });
        if (createCancelButton  != null) createCancelButton.onClick.AddListener(() => { creating = false; Draw(); });
        if (createConfirmButton != null) createConfirmButton.onClick.AddListener(ConfirmCreate);
        if (membersTab    != null) membersTab.onClick.AddListener(() => { showApplicants = false; Draw(); });
        if (applicantsTab != null) applicantsTab.onClick.AddListener(() => { showApplicants = true; Draw(); });
        if (editIntroButton   != null) editIntroButton.onClick.AddListener(EditIntro);
        if (autoApproveButton != null) autoApproveButton.onClick.AddListener(() =>
        {
            if (GuildManager.MyGuild != null) GuildManager.SetAutoApprove(!GuildManager.MyGuild.autoApprove, Report);
        });
        if (leaveButton  != null) leaveButton.onClick.AddListener(() => Ask(confirmLeave, null, _ => GuildManager.Leave(Report)));
        if (dialogOk     != null) dialogOk.onClick.AddListener(OnDialogOk);
        if (dialogCancel != null) dialogCancel.onClick.AddListener(CloseDialog);

        if (createNameInput  != null) createNameInput.characterLimit  = nameMaxLength;
        if (createIntroInput != null) createIntroInput.characterLimit = introMaxLength;
        if (dialogInput      != null) dialogInput.characterLimit      = introMaxLength;

        rowTemplate.gameObject.SetActive(false);
        if (dialog != null) dialog.SetActive(false);
        root.SetActive(false);
    }

    private void OnEnable()  => GuildManager.OnChanged += OnGuildChanged;
    private void OnDisable() => GuildManager.OnChanged -= OnGuildChanged;

    private void OnGuildChanged()
    {
        if (IsOpen) Draw();
    }

    private void Update()
    {
        if (feedbackText != null && feedbackText.gameObject.activeSelf && Time.unscaledTime > feedbackUntil)
            feedbackText.gameObject.SetActive(false);
    }

    public void Open()
    {
        if (!ready) return;
        creating = false;
        showApplicants = false;
        CloseDialog();
        root.SetActive(true);
        Draw();
        GuildManager.Refresh();
    }

    public void Close()
    {
        CloseDialog();
        if (root != null) root.SetActive(false);
    }

    // ───────────────────────────── 그리기 ─────────────────────────────

    private void Draw()
    {
        bool inGuild = GuildManager.State == GuildState.InGuild && GuildManager.MyGuild != null;
        if (inGuild) creating = false;

        noGuildView.SetActive(!inGuild && !creating);
        createView.SetActive(!inGuild && creating);
        guildView.SetActive(inGuild);

        if (inGuild)       DrawGuild();
        else if (creating) DrawCreate();
        else               DrawBrowse();
    }

    private void DrawBrowse()
    {
        SetTitle(titleNoGuild);
        browseRows.Used = 0;

        bool busy = GuildManager.IsBusy;
        foreach (GuildInfo g in GuildManager.Browse)
        {
            GuildInfo guild = g;
            bool applied = GuildManager.HasApplied(guild.inDate);
            string label = applied ? appliedLabel : guild.autoApprove ? joinAutoLabel : joinLabel;
            AddRow(browseContent, browseRows, ChatText.Plain(guild.name),
                   ChatText.Fill(guildRowSub, "master", ChatText.Plain(guild.masterNickname ?? string.Empty), "count", guild.memberCount.ToString()),
                   new GuildRow.RowAction(label, () => GuildManager.Join(guild.inDate, e => Report(e, GameMessages.Guild.applied)), !busy && !applied));
        }

        HideUnused(browseRows);

        bool empty = GuildManager.Browse.Count == 0;
        string status = GuildManager.State == GuildState.Failed       ? GuildManager.LastError
                      : !empty                                         ? string.Empty
                      : GuildManager.IsBrowseLoading || GuildManager.State == GuildState.Loading || GuildManager.State == GuildState.Unknown
                                                                       ? loadingText
                      : GuildManager.IsSearchResult                    ? (GuildManager.LastError ?? noSearchText)
                      : GuildManager.LastError ?? noGuildsText;
        SetStatus(browseStatus, status);

        if (moreButton != null)
        {
            moreButton.gameObject.SetActive(GuildManager.BrowseHasMore && !GuildManager.IsSearchResult);
            moreButton.interactable = !GuildManager.IsBrowseLoading;
        }
        if (showAllButton    != null) showAllButton.gameObject.SetActive(GuildManager.IsSearchResult);
        if (openCreateButton != null) openCreateButton.interactable = GuildManager.State == GuildState.NoGuild && !busy;
    }

    private void DrawCreate()
    {
        SetTitle(titleCreate);
        if (createCostText != null)
            createCostText.text = createGemCost > 0 ? ChatText.Fill(costFormat, "cost", createGemCost.ToString("N0")) : freeText;
        if (createConfirmButton != null) createConfirmButton.interactable = !GuildManager.IsBusy;
    }

    private void DrawGuild()
    {
        GuildInfo guild = GuildManager.MyGuild;
        GuildRole me    = GuildManager.MyRole;
        bool busy       = GuildManager.IsBusy;
        bool manager    = me >= GuildRole.ViceMaster;
        if (!manager) showApplicants = false;

        SetTitle(guild.name);
        if (guildNameText  != null) guildNameText.text  = ChatText.Plain(guild.name);
        if (guildInfoText  != null) guildInfoText.text  = ChatText.Fill(guildInfoFormat,
            "master", ChatText.Plain(guild.masterNickname ?? string.Empty), "count", guild.memberCount.ToString(),
            "auto", guild.autoApprove ? autoOnText : autoOffText);
        if (guildIntroText != null) guildIntroText.text = string.IsNullOrEmpty(guild.intro) ? emptyIntroText : ChatText.Plain(guild.intro);

        int applicantCount = GuildManager.Applicants.Count;
        if (applicantsTab != null) applicantsTab.gameObject.SetActive(manager);
        if (applicantsTabLabel != null)
            applicantsTabLabel.text = applicantCount > 0
                ? ChatText.Fill(applicantsTabFormat, "count", ChatText.Colored(ChatText.UnreadColor, applicantCount.ToString()))
                : applicantsTabEmpty;
        if (membersTab    != null) membersTab.interactable    = showApplicants;
        if (applicantsTab != null) applicantsTab.interactable = !showApplicants;

        if (editIntroButton != null) { editIntroButton.gameObject.SetActive(me == GuildRole.Master); editIntroButton.interactable = !busy; }
        if (autoApproveButton != null)
        {
            autoApproveButton.gameObject.SetActive(me == GuildRole.Master);
            autoApproveButton.interactable = !busy;
            if (autoApproveLabel != null) autoApproveLabel.text = guild.autoApprove ? autoOnButton : autoOffButton;
        }
        if (leaveButton != null) leaveButton.interactable = !busy;

        memberRows.Used = 0;
        if (showApplicants) DrawApplicants(busy);
        else                DrawMembers(me, busy);
        HideUnused(memberRows);
    }

    private void DrawMembers(GuildRole me, bool busy)
    {
        foreach (GuildMember m in GuildManager.Members)
        {
            GuildMember member = m;
            bool isMe = member.gamerInDate == GuildManager.MyUid;
            string name = ChatText.Plain(member.nickname) + (isMe ? meSuffix : string.Empty);
            string role = member.role == GuildRole.Master     ? ChatText.Colored(MasterColor, roleMaster)
                        : member.role == GuildRole.ViceMaster ? ChatText.Colored(ViceColor, roleVice)
                        : roleMember;
            string sub = member.lastLoginMs > 0
                ? role + separator + ChatText.Fill(lastLoginFormat, "date", FormatDate(member.lastLoginMs))
                : role;

            var actions = new List<GuildRow.RowAction>();
            if (!isMe)
            {
                // 길드장: 모두 추방 · 부길드장 임명/해제 · 위임 / 부길드장: 일반 멤버만 추방
                bool canKick = me == GuildRole.Master || (me == GuildRole.ViceMaster && member.role == GuildRole.Member);
                if (canKick)
                    actions.Add(new GuildRow.RowAction(kickLabel, () =>
                        Ask(ChatText.Fill(confirmKick, "name", ChatText.Plain(member.nickname)), null, _ => GuildManager.Kick(member.gamerInDate, Report)), !busy));
                if (me == GuildRole.Master)
                {
                    actions.Add(member.role == GuildRole.ViceMaster
                        ? new GuildRow.RowAction(unviceLabel, () => GuildManager.RemoveVice(member.gamerInDate, Report), !busy)
                        : new GuildRow.RowAction(viceLabel,   () => GuildManager.MakeVice(member.gamerInDate, Report), !busy));
                    actions.Add(new GuildRow.RowAction(masterLabel, () =>
                        Ask(ChatText.Fill(confirmMaster, "name", ChatText.Plain(member.nickname)), null, _ => GuildManager.MakeMaster(member.gamerInDate, Report)), !busy));
                }
            }
            AddRow(memberContent, memberRows, name, sub, actions.ToArray());
        }
        SetStatus(memberStatus, GuildManager.Members.Count == 0 ? (GuildManager.LastError ?? loadingText) : string.Empty);
    }

    private void DrawApplicants(bool busy)
    {
        foreach (GuildApplicant a in GuildManager.Applicants)
        {
            GuildApplicant applicant = a;
            AddRow(memberContent, memberRows, ChatText.Plain(applicant.nickname), null,
                   new GuildRow.RowAction(declineLabel, () => GuildManager.Decline(applicant.gamerInDate, Report), !busy),
                   new GuildRow.RowAction(approveLabel, () => GuildManager.Approve(applicant.gamerInDate, Report), !busy));
        }
        SetStatus(memberStatus, GuildManager.Applicants.Count == 0 ? noApplicantsText : string.Empty);
    }

    // ───────────────────────────── 작업 ─────────────────────────────

    private void ConfirmCreate()
    {
        string name  = createNameInput  != null ? createNameInput.text  : string.Empty;
        string intro = createIntroInput != null ? createIntroInput.text : string.Empty;
        if (string.IsNullOrWhiteSpace(name)) { Report(GameMessages.Guild.enterName); return; }

        void Create(string _) => GuildManager.Create(name, intro, createGemCost, error =>
        {
            Report(error);
            if (error == null)
            {
                creating = false;
                if (createNameInput  != null) createNameInput.text  = string.Empty;
                if (createIntroInput != null) createIntroInput.text = string.Empty;
            }
        });

        if (createGemCost > 0) Ask(ChatText.Fill(confirmCreate, "cost", createGemCost.ToString("N0")), null, Create);
        else Create(null);
    }

    private void EditIntro()
    {
        GuildInfo guild = GuildManager.MyGuild;
        if (guild == null) return;
        Ask(editIntroPrompt, guild.intro ?? string.Empty, text => GuildManager.UpdateIntro(text, Report));
    }

    /// <summary>확인 창. input 이 null 이 아니면 입력 칸을 보여 주고 그 값으로 onOk.</summary>
    private void Ask(string message, string input, Action<string> onOk)
    {
        if (dialog == null) { onOk?.Invoke(input); return; }   // 확인 창이 없으면 바로 실행

        dialogAction = onOk;
        if (dialogMessage != null) dialogMessage.text = message;
        if (dialogInput != null)
        {
            dialogInput.gameObject.SetActive(input != null);
            dialogInput.text = input ?? string.Empty;
        }
        dialog.SetActive(true);
    }

    private void OnDialogOk()
    {
        Action<string> action = dialogAction;
        string value = dialogInput != null && dialogInput.gameObject.activeSelf ? dialogInput.text : null;
        CloseDialog();
        action?.Invoke(value);
    }

    private void CloseDialog()
    {
        dialogAction = null;
        if (dialog != null) dialog.SetActive(false);
    }

    private void Report(string error) => Report(error, doneText);

    private void Report(string error, string success)
    {
        if (feedbackText == null) return;
        string text = error ?? success;
        if (string.IsNullOrEmpty(text)) return;
        feedbackText.text = text;
        feedbackText.gameObject.SetActive(true);
        feedbackUntil = Time.unscaledTime + Mathf.Max(0.5f, feedbackSeconds);
    }

    // ───────────────────────────── 도우미 ─────────────────────────────

    private void SetTitle(string text)
    {
        if (titleText != null) titleText.text = ChatText.Plain(text ?? string.Empty);
    }

    private void AddRow(RectTransform parent, RowPool pool, string title, string sub, params GuildRow.RowAction[] actions)
    {
        GuildRow row;
        if (pool.Used < pool.Rows.Count) row = pool.Rows[pool.Used];
        else
        {
            row = Instantiate(rowTemplate, parent);
            pool.Rows.Add(row);
        }
        pool.Used++;

        if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);   // 만든 순서 = 그리는 순서라 자리를 옮길 필요 없음
        row.Set(title, sub, actions);
    }

    private static void HideUnused(RowPool pool)
    {
        for (int i = pool.Used; i < pool.Rows.Count; i++)
            if (pool.Rows[i].gameObject.activeSelf) pool.Rows[i].gameObject.SetActive(false);
    }

    private static void SetStatus(TextMeshProUGUI status, string text)
    {
        if (status == null) return;
        text = text ?? string.Empty;
        status.text = text;
        status.gameObject.SetActive(text.Length > 0);
    }

    private static string FormatDate(long ms)
        => DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
}

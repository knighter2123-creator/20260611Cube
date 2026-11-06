using System;
using UnityEngine;

/// <summary>서버·데이터 코드가 만드는 오류·상태 문장 모음(ScriptableObject) — Resources/GameMessages.asset 인스펙터에서 고치고(플레이 중 즉시 반영), 에셋이 없으면 코드 기본 문장 사용.</summary>
// {name}{max}{count} 등 자리는 ChatText.Fill 로 치환 / 새 문장은 묶음 클래스에 필드 추가(기본값 = 문장) → GameMessages.Chat.필드
[CreateAssetMenu(fileName = "GameMessages", menuName = "Game/Game Messages (오류·상태 문장)")]
public class GameMessages : ScriptableObject
{
    public const string RESOURCE_PATH = "GameMessages";   // Assets/Resources/GameMessages.asset

    // ───────────────────────────── 묶음 ─────────────────────────────

    [Serializable]
    public class CommonTexts
    {
        [Header("서버 연결")]
        public string offline        = "서버에 연결할 수 없습니다.";
        public string offlineCheck   = "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.";
        public string offlineRetry   = "서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";

        [Header("계정")]
        public string accountChecking = "계정 정보를 확인하는 중입니다.";
        public string accountPending  = "계정 정보를 확인하는 중입니다. 잠시 후 다시 시도해 주세요.";
        public string accountChanged  = "계정이 바뀌어 처리하지 않았습니다.";
    }

    [Serializable]
    public class ChatTexts
    {
        public string noManager = "채팅 시스템을 찾을 수 없습니다.";

        [Header("접속 상태 (채팅창 가운데 안내)")]
        public string serverPreparing   = "서버 연결 준비 중입니다.";
        public string loginRequired     = "로그인하면 채팅을 할 수 있습니다.";
        public string offlineRetrying   = "서버에 연결할 수 없습니다. 잠시 후 다시 시도합니다.";
        public string accountChecking   = "계정 정보를 확인하는 중입니다...";
        public string nicknamePending   = "닉네임을 정하는 중입니다...";
        public string connecting        = "채팅 서버에 연결하는 중입니다...";
        public string serverUnreachable = "채팅 서버에 연결할 수 없습니다.";
        [Tooltip("연결이 끊겨 다시 붙을 때. {reason} = 끊긴 이유")]
        public string reconnecting      = "{reason} 잠시 후 다시 연결합니다.";

        [Header("보내기")]
        public string emptyMessage     = "메시지를 입력해 주세요.";
        public string sending          = "보내는 중입니다. 잠시 후 다시 보내 주세요.";
        public string tooFast          = "너무 빨리 보내고 있습니다. 잠시 후 다시 보내 주세요.";
        public string tooLong          = "메시지가 너무 깁니다.";
        [Tooltip("{max} = 개인 메시지 최대 글자 수")]
        public string directTooLong    = "개인 메시지는 {max}자까지 보낼 수 있습니다.";
        public string accountPendingSend = "계정 정보를 확인하는 중입니다. 잠시 후 다시 보내 주세요.";
        public string selfDirect       = "나에게는 개인 메시지를 보낼 수 없습니다.";
        public string joiningChannel   = "채팅 채널에 들어가는 중입니다. 잠시 후 다시 보내 주세요.";
        public string sendFailed       = "메시지를 보내지 못했습니다.";

        [Header("뒤끝챗 서버 오류")]
        public string filtered          = "사용할 수 없는 단어가 들어 있습니다.";
        public string banned            = "채팅이 제한되었습니다.";
        [Tooltip("{remain} = 남은 시간 (아래 시간 형식)")]
        public string bannedRemain      = "채팅이 제한되었습니다. ({remain} 남음)";
        public string notNickname       = "닉네임을 정한 뒤 채팅할 수 있습니다.";
        public string serverFull        = "채팅 서버가 가득 찼습니다.";
        public string channelFull       = "채팅 채널이 가득 찼습니다.";
        public string disabledService   = "채팅 서비스가 꺼져 있습니다.";
        public string disabledChannel   = "채팅 채널을 쓸 수 없습니다.";
        public string duplicateConnection = "다른 곳에서 같은 계정으로 채팅에 접속했습니다.";
        public string authFailed        = "채팅 서버 인증에 실패했습니다.";
        [Tooltip("{error} = 뒤끝챗 오류 이름")]
        public string unknownError      = "채팅 오류 ({error})";

        [Header("남은 시간 형식 — {h} 시간, {m} 분, {s} 초")]
        public string hoursMinutes = "{h}시간 {m}분";
        public string minutes      = "{m}분";
        public string seconds      = "{s}초";

        [Header("기타")]
        [Tooltip("공지·시스템 메시지의 보낸 사람 이름")]
        public string systemSender = "알림";
    }

    [Serializable]
    public class DirectTexts
    {
        [Header("상태")]
        public string loginRequired   = "로그인하면 개인 메시지를 쓸 수 있습니다.";
        public string connecting      = "개인 메시지 서버에 연결하는 중입니다...";
        public string roomsLoading    = "대화 목록을 불러오는 중입니다...";
        public string roomsFailed     = "대화 목록을 불러오지 못했습니다.";
        public string loading         = "개인 메시지를 불러오는 중입니다. 잠시 후 다시 시도해 주세요.";

        [Header("대화방")]
        public string selfConversation    = "나와는 대화할 수 없습니다.";
        public string blockedConversation = "차단한 유저와는 대화할 수 없습니다.";
        public string blockedSend         = "차단한 유저에게는 메시지를 보낼 수 없습니다.";
        public string noRoom              = "대화방이 없습니다. 다시 열어 주세요.";

        [Header("보내기 실패 (뒤끝 쪽지)")]
        public string accountChanged = "계정이 바뀌어 보내지 않았습니다.";
        public string offline        = "서버에 연결할 수 없습니다. 잠시 후 다시 보내 주세요.";
        [Tooltip("403")]
        public string forbidden      = "이 유저에게는 메시지를 보낼 수 없습니다 (친구만 보낼 수 있을 수 있습니다).";
        [Tooltip("404")]
        public string notFound       = "상대를 찾을 수 없습니다.";
        [Tooltip("405")]
        public string boxFull        = "상대의 쪽지함이 가득 찼습니다.";
        [Tooltip("그 밖의 실패. {code} = 서버 상태 코드")]
        public string failedCode     = "메시지를 보내지 못했습니다. ({code})";
    }

    [Serializable]
    public class FriendTexts
    {
        [Header("상태")]
        public string loginRequired = "로그인하면 친구 기능을 쓸 수 있습니다.";
        public string loading       = "친구 목록을 불러오는 중입니다...";
        public string loadingRetry  = "친구 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요.";
        public string listFailed    = "친구 목록을 불러오지 못했습니다.";
        [Tooltip("{reason} = 실패 이유")]
        public string retrying      = "{reason} 잠시 후 다시 시도합니다.";
        [Tooltip("이름을 모르는 친구의 표시 이름")]
        public string noName        = "(이름 없음)";

        [Header("요청 전 확인")]
        public string alreadyFriend    = "이미 친구입니다.";
        public string alreadyRequested = "이미 친구 요청을 보냈습니다.";
        public string blockedRequest   = "차단한 유저에게는 친구 요청을 보낼 수 없습니다.";
        public string enterNickname    = "닉네임을 입력해 주세요.";
        public string selfRequest      = "나에게는 친구 요청을 보낼 수 없습니다.";
        public string noIncoming       = "받은 친구 요청이 없습니다.";
        [Tooltip("{max} = 최대 친구 수")]
        public string maxFriends       = "친구는 요청 중인 사람을 포함해 최대 {max}명까지입니다.";

        [Header("닉네임 검색")]
        public string userNotFound = "그 닉네임의 유저가 없습니다.";
        public string searchFailed = "유저를 찾지 못했습니다.";

        [Header("서버 실패")]
        [Tooltip("409")]
        public string conflict     = "이미 친구이거나 요청이 오간 상대입니다.";
        [Tooltip("404")]
        public string notFound     = "상대를 찾을 수 없거나 요청이 이미 처리되었습니다.";
        [Tooltip("412")]
        public string full         = "친구 수가 가득 찼습니다.";
        [Tooltip("그 밖의 실패. {action} = 아래 작업 이름, {code} = 서버 상태 코드")]
        public string actionFailed = "{action}에 실패했습니다. ({code})";

        [Header("작업 이름 ({action} 자리)")]
        public string actionRequest = "친구 요청";
        public string actionAccept  = "친구 수락";
        public string actionRemove  = "친구 삭제";
        public string actionReject  = "요청 거절";
        public string actionRevoke  = "요청 취소";
    }

    [Serializable]
    public class BlockTexts
    {
        public string loading    = "차단 목록을 불러오는 중입니다. 잠시 후 다시 시도해 주세요.";
        public string saving     = "차단 목록을 저장하는 중입니다. 잠시 후 다시 시도해 주세요.";
        public string saveFailed = "차단 목록을 저장하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        public string invalid    = "차단할 수 없는 유저입니다.";
        [Tooltip("{max} = 최대 차단 수")]
        public string max        = "최대 {max}명까지 차단할 수 있습니다.";
    }

    [Serializable]
    public class MailTexts
    {
        public string fetchFailed   = "우편을 불러오지 못했습니다.";
        public string unknownPost   = "알 수 없는 우편입니다.";
        [Tooltip("받기 404 — 이미 받았거나 만료")]
        public string gone          = "이미 받았거나 기간이 지난 우편입니다.";
        public string claimFailed   = "우편을 받지 못했습니다.";
        public string needUpdate    = "이 우편의 보상은 지금 버전에서 받을 수 없습니다. 게임을 업데이트해 주세요.";
        public string needOnline    = "서버에 연결된 상태에서 받을 수 있습니다.";
        public string accountChanged = "계정이 바뀌어 우편을 지급하지 못했습니다.";
        [Tooltip("제목이 없는 우편의 제목")]
        public string defaultTitle  = "우편";
    }

    [Serializable]
    public class RankingTexts
    {
        public string fetchFailed   = "랭킹을 불러오지 못했습니다.";
        public string mineFailed    = "내 순위를 불러오지 못했습니다.";
        public string notConfigured = "랭킹이 아직 준비되지 않았습니다.";
        public string loginRequired = "로그인하면 랭킹을 볼 수 있습니다.";
        public string offline       = "서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";
    }

    [Serializable]
    public class NicknameTexts
    {
        public string readFailed     = "닉네임 정보를 읽지 못했습니다.";
        public string duplicate      = "이미 사용 중인 닉네임입니다.";
        public string invalid        = "사용할 수 없는 닉네임입니다.";
        public string sessionExpired = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
        [Tooltip("{code} = 서버 상태 코드")]
        public string failedCode     = "닉네임을 정하지 못했습니다. ({code})";
        public string needOnline     = "닉네임은 인터넷에 연결된 상태에서 정할 수 있습니다.";
        public string checking       = "닉네임 정보를 확인하는 중입니다. 잠시 후 다시 시도해 주세요.";
    }

    [Serializable]
    public class AuthTexts
    {
        [Header("입력 확인")]
        public string notReady      = "아직 로그인 준비 중입니다. 잠시 후 다시 시도해 주세요.";
        public string enterEmail    = "이메일을 입력해 주세요.";
        public string badEmail      = "이메일 형식이 올바르지 않습니다.";
        [Tooltip("{min} = 비밀번호 최소 길이")]
        public string shortPassword = "비밀번호는 {min}자 이상이어야 합니다.";

        [Header("구글")]
        public string googleLinkNeedOnline = "구글 연동은 인터넷에 연결된 상태에서 할 수 있습니다.";
        public string googleInUse          = "이미 다른 계정에 쓰이는 구글 계정입니다. 로그아웃한 뒤 그 구글 계정으로 로그인해 주세요.";
        public string signUpThenLoginFailed = "가입은 됐지만 로그인하지 못했습니다. 다시 로그인해 주세요.";

        [Header("서버 응답")]
        public string checkInternet = "인터넷 연결을 확인해 주세요.";
        public string maintenance   = "서버 점검 중입니다. 잠시 후 다시 시도해 주세요.";
        public string tooMany       = "요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.";
        public string deviceBlocked = "이 기기는 사용이 제한되었습니다.";
        public string serverError   = "서버 오류입니다. 잠시 후 다시 시도해 주세요.";
        [Tooltip("409")]
        public string emailTaken    = "이미 가입된 이메일입니다. 로그인해 주세요.";
        [Tooltip("401")]
        public string wrongPassword = "이메일 또는 비밀번호가 맞지 않습니다.";
        [Tooltip("403")]
        public string suspended     = "사용이 중지된 계정입니다.";
        [Tooltip("410")]
        public string withdrawing   = "탈퇴 처리 중인 계정입니다.";
        [Tooltip("400")]
        public string badInput      = "입력 형식이 올바르지 않습니다.";
        [Tooltip("그 밖의 실패. {action} = 아래 작업 이름, {code} = 서버 상태·오류 코드")]
        public string actionFailedCode = "{action}에 실패했습니다. ({code})";
        [Tooltip("{action} = 아래 작업 이름")]
        public string actionFailed  = "{action}에 실패했습니다.";

        [Header("작업 이름 ({action} 자리)")]
        public string actionLogin       = "로그인";
        public string actionSignUp      = "회원가입";
        [Tooltip("구글로 처음 들어와 새 계정이 만들어졌을 때")]
        public string actionGoogleSignUp = "가입";
        public string actionGoogleLink  = "구글 연동";
        public string actionGuestLogin  = "게스트 로그인";
        [Tooltip("게스트로 처음 들어와 새 계정이 만들어졌을 때")]
        public string actionGuestSignUp = "게스트 가입";

        [Header("로그인 방법 표시")]
        public string methodEmail = "이메일";
        public string methodGuest = "게스트";

        [Header("이메일 로그인 창")]
        public string noAuthSystem = "로그인 시스템을 찾을 수 없습니다.";
        public string processing   = "처리 중…";
        public string signedIn     = "로그인되었습니다.";
    }

    [Serializable]
    public class AccountTexts
    {
        public string deleteFailed     = "계정을 삭제하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        public string serverDataMismatch = "로그인 정보가 맞지 않아 서버 기록을 지울 수 없습니다. 다시 로그인해 주세요.";
        public string dataDeleteFailed = "데이터를 삭제하지 못했습니다. 잠시 후 다시 시도해 주세요.";
        public string resetRunning     = "이미 초기화가 진행 중입니다.";
        public string noLoginScene     = "로그인 화면을 찾을 수 없습니다.";
        public string targetSaveExists = "대상 계정의 세이브가 이미 있습니다.";
    }

    // ───────────────────────────── 칸 ─────────────────────────────

    [SerializeField] private CommonTexts   common   = new CommonTexts();
    [SerializeField] private ChatTexts     chat     = new ChatTexts();
    [SerializeField] private DirectTexts   direct   = new DirectTexts();
    [SerializeField] private FriendTexts   friends  = new FriendTexts();
    [SerializeField] private BlockTexts    block    = new BlockTexts();
    [SerializeField] private MailTexts     mail     = new MailTexts();
    [SerializeField] private RankingTexts  ranking  = new RankingTexts();
    [SerializeField] private NicknameTexts nickname = new NicknameTexts();
    [SerializeField] private AuthTexts     auth     = new AuthTexts();
    [SerializeField] private AccountTexts  account  = new AccountTexts();

    // ───────────────────────────── 읽기 ─────────────────────────────

    public static CommonTexts   Common   => Current.common;
    public static ChatTexts     Chat     => Current.chat;
    public static DirectTexts   Direct   => Current.direct;
    public static FriendTexts   Friends  => Current.friends;
    public static BlockTexts    Block    => Current.block;
    public static MailTexts     Mail     => Current.mail;
    public static RankingTexts  Ranking  => Current.ranking;
    public static NicknameTexts Nickname => Current.nickname;
    public static AuthTexts     Auth     => Current.auth;
    public static AccountTexts  Account  => Current.account;

    private static GameMessages current;

    /// <summary>Resources 의 에셋 (처음 쓸 때 한 번 읽음). 없으면 코드 기본 문장으로 만든 임시 사본.</summary>
    public static GameMessages Current
    {
        get
        {
            if (current != null) return current;

            current = Resources.Load<GameMessages>(RESOURCE_PATH);
            if (current == null)
            {
                Debug.LogWarning($"[GameMessages] Resources/{RESOURCE_PATH}.asset 이 없어 코드의 기본 문장을 씁니다. " +
                                 "Project 창 우클릭 → Create → Game → Game Messages 로 Assets/Resources 에 만드세요.");
                current = CreateInstance<GameMessages>();
                current.hideFlags = HideFlags.DontSave;
            }
            return current;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;   // 도메인 리로드를 끈 에디터에서 지난 플레이의 임시 사본을 쓰지 않게
}

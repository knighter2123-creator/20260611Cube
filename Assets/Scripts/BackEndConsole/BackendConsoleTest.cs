using BackEnd;
using UnityEngine;


public class BackendConsoleTest : MonoBehaviour
{
    [Tooltip("테스트 계정 아이디 (없으면 자동으로 가입합니다)")]
    [SerializeField] private string testId = "user1";
    [SerializeField] private string testPassword = "1234";

    [Tooltip("켜 두면 플레이를 시작할 때 자동으로 한 번 실행합니다")]
    [SerializeField] private bool runOnStart = true;

    [Tooltip("게임 정보 삽입까지 할지 (뒤끝 콘솔에 USER_DATA 테이블이 있어야 함)")]
    [SerializeField] private bool insertGameData = true;

    private bool running;

    private void Start()
    {
        if (runOnStart) RunTest();
    }

    [ContextMenu("뒤끝 가이드 테스트 실행")]
    private void RunTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[BackendConsoleTest] 플레이 중에만 실행할 수 있습니다.");
            return;
        }
        if (running) return;

        if (AuthManager.Instance != null && AuthManager.Instance.IsSignedIn)
        {
            Debug.LogWarning("[BackendConsoleTest] 게임에서 이미 로그인돼 있어 테스트를 건너뜁니다 — 테스트가 로그인 세션을 바꾸기 때문입니다. 로그아웃한 뒤 다시 실행하세요.");
            return;
        }

        running = true;

        var bro = Backend.Initialize(); // 뒤끝 초기화
        {
            // 뒤끝 초기화에 대한 응답값
            if (bro.IsSuccess())
            {
                Debug.Log("초기화 성공 : " + bro); // 성공일 경우 statusCode 204 Success
            }
            else
            {
                Debug.LogError("초기화 실패 : " + bro); // 실패일 경우 statusCode 400대 에러 발생
            }

            RunSteps();
        }
    }

    private void RunSteps()
    {
        try
        {
            // Step 2에서 user2가 친구 요청을 보낸 유저로 로그인해야합니다.  
            BackendLogin.Instance.CustomLogin("123@123.com", "123123");
       
            BackendGameLog.Instance.GameLogInsert(); // [추가] 게임 로그 저장 기능

            Debug.Log("테스트를 종료합니다.");
       
        }
        finally
        {
            running = false;
        }
    }
}

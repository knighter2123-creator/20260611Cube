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

    // ★ Start 에서 바로 실행하면 안 됩니다. 이 시점엔 AuthManager 가 아직 '마지막 계정 자동 로그인' 을 하기 전이라
    //   IsSignedIn 이 false 로 보여서 아래 검사를 통과하고, 테스트 계정으로 로그인해 기기의 로그인 토큰을 덮어씁니다.
    //   그러면 이어지는 자동 로그인이 테스트 계정 토큰으로 들어가 "저장된 계정과 토큰 계정이 다릅니다" 경고가 뜨고,
    //   게임이 내 계정 대신 테스트 계정으로 바뀝니다 (친구·랭킹이 엉뚱한 계정 것으로 보임).
    //   → AuthManager 가 시작 확인(IsReady)을 마칠 때까지 기다렸다가 판단합니다.
    private System.Collections.IEnumerator Start()
    {
        if (!runOnStart) yield break;

        while (AuthManager.Instance != null && !AuthManager.Instance.IsReady)
            yield return null;

        RunTest();
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

        if (AuthManager.Instance != null && (AuthManager.Instance.IsSignedIn || AuthManager.Instance.IsSigningIn))
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

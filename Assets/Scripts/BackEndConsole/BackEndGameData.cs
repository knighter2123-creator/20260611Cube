using System.Text;
using UnityEngine;

// 뒤끝 SDK namespace 추가
using BackEnd;

/// <summary>
/// RANK_DATA 테이블 한 행 = 플레이어 스테이지 데이터.
/// 컬럼은 RankingManager 와 똑같이 맞춥니다 (progress / world / stage / clearedAt).
/// </summary>
public class UserData
{
    public int  progress;    // StageRecord.BestProgress (1-1 클리어 = 1, 0 = 기록 없음) — 랭킹 점수
    public int  world;       // progress 를 월드-스테이지로 나눈 값 (표시용)
    public int  stage;
    public long clearedAt;   // 기록을 세운 시각 (UTC unix ms) — 서버에는 dateTime(ISO 문자열)으로 저장

    public static UserData FromStageRecord()
    {
        int progress = StageRecord.BestProgress;
        StageRecord.Split(progress, out int world, out int stage);
        return new UserData
        {
            progress  = progress,
            world     = progress > 0 ? world : 0,
            stage     = progress > 0 ? stage : 0,
            clearedAt = StageRecord.BestClearedAt,
        };
    }

    public Param ToParam() => RankingManager.BuildStageParam("progress", progress, clearedAt);

    // 데이터를 디버깅하기 위한 함수입니다.(Debug.Log(UserData);)
    public override string ToString()
    {
        StringBuilder result = new StringBuilder();
        result.AppendLine($"progress : {progress} ({StageRecord.Format(progress)})");
        result.AppendLine($"world : {world}");
        result.AppendLine($"stage : {stage}");
        result.AppendLine($"clearedAt : {(clearedAt > 0 ? System.DateTimeOffset.FromUnixTimeMilliseconds(clearedAt).ToString("u") : "-")}");
        return result.ToString();
    }
}

/// <summary>
/// 뒤끝 가이드 테스트용 (동기 호출). 실제 게임의 업로드는 RankingManager 가 합니다.
/// </summary>
public class BackendGameData
{
    private const string TABLE = "RANK_DATA";

    private static BackendGameData _instance = null;

    public static BackendGameData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new BackendGameData();
            }

            return _instance;
        }
    }

    public static UserData userData;

    private string gameDataRowInDate = string.Empty;

    /// <summary>현재 세이브의 스테이지 기록으로 RANK_DATA 에 새 행을 넣습니다.</summary>
    public void GameDataInsert()
    {
        // 로그인 전에 부르면 서버가 400 "Login is yet" 으로 거절함 — 미리 알기 쉽게 안내
        if (!Backend.IsLogin)
        {
            Debug.LogError("게임 정보 삽입을 건너뜁니다 — 아직 뒤끝에 로그인되지 않았습니다. 로그인(또는 회원가입)부터 하세요.");
            return;
        }

        // 한 계정에 행이 두 개 생기면 RankingManager 가 어느 행을 쓸지 보장되지 않음 — 이미 있으면 수정으로
        var check = Backend.GameData.GetMyData(TABLE, new Where());
        if (check.IsSuccess() && check.FlattenRows().Count > 0)
        {
            gameDataRowInDate = check.FlattenRows()[0]["inDate"].ToString();
            Debug.LogWarning("이미 RANK_DATA 행이 있어 삽입 대신 수정합니다.");
            userData = UserData.FromStageRecord();
            GameDataUpdate();
            return;
        }

        userData = UserData.FromStageRecord();
        Debug.Log("스테이지 데이터를 RANK_DATA 에 삽입합니다.\n" + userData);

        var bro = Backend.GameData.Insert(TABLE, userData.ToParam());

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 데이터 삽입에 성공했습니다. : " + bro);

            //삽입한 게임 정보의 고유값입니다.
            gameDataRowInDate = bro.GetInDate();
        }
        else
        {
            Debug.LogError("게임 정보 데이터 삽입에 실패했습니다. : " + bro);
        }
    }

    public void GameDataGet()
    {
        Debug.Log("게임 정보 조회 함수를 호출합니다.");

        var bro = Backend.GameData.GetMyData(TABLE, new Where());

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 조회에 성공했습니다. : " + bro);

            LitJson.JsonData gameDataJson = bro.FlattenRows(); // Json으로 리턴된 데이터를 받아옵니다.

            // 받아온 데이터의 갯수가 0이라면 데이터가 존재하지 않는 것입니다.
            if (gameDataJson.Count <= 0)
            {
                Debug.LogWarning("데이터가 존재하지 않습니다.");
            }
            else
            {
                LitJson.JsonData row = gameDataJson[0];
                gameDataRowInDate = BackendJson.Str(row, "inDate"); //불러온 게임 정보의 고유값입니다.

                // 컬럼이 없는 예전 행(level/atk 시절)도 0 으로 읽히게 BackendJson 사용
                userData = new UserData
                {
                    progress  = (int)BackendJson.Long(row, "progress"),
                    world     = (int)BackendJson.Long(row, RankingManager.COL_WORLD),
                    stage     = (int)BackendJson.Long(row, RankingManager.COL_STAGE),
                    clearedAt = BackendJson.DateMs(row, RankingManager.COL_CLEARED_AT),
                };

                Debug.Log(userData.ToString());
            }
        }
        else
        {
            Debug.LogError("게임 정보 조회에 실패했습니다. : " + bro);
        }
    }

    // 게임 정보 수정하기 — userData 가 없으면 현재 세이브의 스테이지 기록으로 채움
    public void GameDataUpdate()
    {
        if (userData == null)
        {
            userData = UserData.FromStageRecord();
        }

        Param param = userData.ToParam();
        BackendReturnObject bro = null;

        if (string.IsNullOrEmpty(gameDataRowInDate))
        {
            Debug.Log("내 제일 최신 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.Update(TABLE, new Where(), param);
        }
        else
        {
            Debug.Log($"{gameDataRowInDate}의 게임 정보 데이터 수정을 요청합니다.");

            bro = Backend.GameData.UpdateV2(TABLE, gameDataRowInDate, Backend.UserInDate, param);
        }

        if (bro.IsSuccess())
        {
            Debug.Log("게임 정보 데이터 수정에 성공했습니다. : " + bro);
        }
        else
        {
            Debug.LogError("게임 정보 데이터 수정에 실패했습니다. : " + bro);
        }
    }
}

using System.Text;
using UnityEngine;

// 뒤끝 SDK namespace 추가
using BackEnd;
using BackEnd.Leaderboard;

/// <summary>
/// 뒤끝 가이드 테스트용 (동기 호출) — 유저 리더보드 (SDK 5.4.0+, 옛 URank '랭킹' 대체).
/// RANK_DATA 의 스테이지 데이터(progress/world/stage/clearedAt)를 올립니다. 실제 게임은 RankingManager 가 올립니다.
/// </summary>
public class BackendRank
{
    // [변경 필요] '뒤끝 콘솔 > 리더보드' 에서 만든 유저 리더보드의 UUID (RankingManager 인스펙터 값과 같게)
    private const string LEADERBOARD_UUID = "01a19ff7-6c43-7845-b479-fef29b231f3d";
    private const string TABLE = "RANK_DATA";

    private static BackendRank _instance = null;

    public static BackendRank Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new BackendRank();
            }

            return _instance;
        }
    }

    /// <summary>현재 세이브의 스테이지 기록을 RANK_DATA 에 쓰고 리더보드를 갱신합니다.</summary>
    public void RankInsert() => RankInsert(StageRecord.BestProgress);

    /// <summary>progress (1-1 클리어 = 1) 를 지정해서 올립니다 — 테스트용.</summary>
    public void RankInsert(int progress)
    {
        string rowInDate = string.Empty;

        // 리더보드 갱신에는 게임 정보 행의 inDate 가 필요합니다.
        Debug.Log("데이터 조회를 시도합니다.");
        var bro = Backend.GameData.GetMyData(TABLE, new Where());

        if (bro.IsSuccess() == false)
        {
            Debug.LogError("데이터 조회 중 문제가 발생했습니다 : " + bro);
            return;
        }

        Param param = RankingManager.BuildStageParam("progress", progress, StageRecord.BestClearedAt);

        if (bro.FlattenRows().Count > 0)
        {
            rowInDate = bro.FlattenRows()[0]["inDate"].ToString();
        }
        else
        {
            Debug.Log("데이터가 존재하지 않습니다. 데이터 삽입을 시도합니다.");
            var bro2 = Backend.GameData.Insert(TABLE, param);

            if (bro2.IsSuccess() == false)
            {
                Debug.LogError("데이터 삽입 중 문제가 발생했습니다 : " + bro2);
                return;
            }

            Debug.Log("데이터 삽입에 성공했습니다 : " + bro2);
            rowInDate = bro2.GetInDate();
        }

        Debug.Log($"내 게임 정보의 rowInDate : {rowInDate} / 올릴 기록 : {StageRecord.Format(progress)}");

        // 행을 param 으로 수정하면서 리더보드 점수도 갱신합니다.
        Debug.Log("리더보드 갱신을 시도합니다.");
        var rankBro = Backend.Leaderboard.User.UpdateMyDataAndRefreshLeaderboard(LEADERBOARD_UUID, TABLE, rowInDate, param);

        if (rankBro.IsSuccess() == false)
        {
            Debug.LogError("리더보드 갱신 중 오류가 발생했습니다. : " + rankBro);
            return;
        }

        Debug.Log("리더보드 갱신에 성공했습니다. : " + rankBro);
    }

    public void RankGet()
    {
        BackendUserLeaderboardReturnObject bro = Backend.Leaderboard.User.GetLeaderboard(LEADERBOARD_UUID);

        if (bro.IsSuccess() == false)
        {
            Debug.LogError("리더보드 조회 중 오류가 발생했습니다. : " + bro);
            return;
        }

        Debug.Log("리더보드 조회에 성공했습니다. : " + bro);
        Debug.Log("총 등록 유저 수 : " + bro.GetTotalCount());

        foreach (UserLeaderboardItem item in bro.GetUserLeaderboardList())
        {
            int.TryParse(item.score, out int progress);

            StringBuilder info = new StringBuilder();
            info.AppendLine("순위 : " + item.rank);
            info.AppendLine("닉네임 : " + item.nickname);
            info.AppendLine($"점수 : {item.score} ({StageRecord.Format(progress)})");
            info.AppendLine("gamerInDate : " + item.gamerInDate);
            info.AppendLine("정렬번호 : " + item.index);
            info.AppendLine();
            Debug.Log(info);
        }
    }
}

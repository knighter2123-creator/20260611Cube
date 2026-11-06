using System;
using BackEnd;
using LitJson;

/// <summary>뒤끝 게임 정보 테이블의 '내 행'(계정당 한 행 가정) 읽기·삽입·수정 도우미 — GetMyData/Insert/UpdateV2 를 감싸 콜백을 메인 스레드로.</summary>
public static class BackendTable
{
    /// <summary>내 행. onDone(행 — FlattenRows 를 거친 값, 없으면 null / 실패 문구 — 성공이면 null)</summary>
    public static void GetMyRow(string table, Action<JsonData, string> onDone)
    {
        BackendManager.Call(cb => Backend.GameData.GetMyData(table, new Where(), cb), bro =>
        {
            if (bro == null || !bro.IsSuccess())
            {
                onDone?.Invoke(null, $"{table} 조회 실패: {BackendManager.Describe(bro)}");
                return;
            }

            JsonData rows = bro.FlattenRows();
            onDone?.Invoke(rows != null && rows.IsArray && rows.Count > 0 ? rows[0] : null, null);
        });
    }

    /// <summary>행을 새로 만듭니다. onDone(새 행의 inDate, 실패 문구)</summary>
    public static void Insert(string table, Param param, Action<string, string> onDone)
    {
        BackendManager.Call(cb => Backend.GameData.Insert(table, param, cb), bro =>
        {
            if (bro == null || !bro.IsSuccess()) onDone?.Invoke(null, $"{table} 삽입 실패: {BackendManager.Describe(bro)}");
            else                                 onDone?.Invoke(bro.GetInDate(), null);
        });
    }

    /// <summary>내 행(rowInDate)을 고칩니다. onDone(실패 문구 — 성공이면 null)</summary>
    public static void Update(string table, string rowInDate, Param param, Action<string> onDone)
    {
        BackendManager.Call(cb => Backend.GameData.UpdateV2(table, rowInDate, Backend.UserInDate, param, cb), bro =>
        {
            onDone?.Invoke(bro != null && bro.IsSuccess() ? null : $"{table} 수정 실패: {BackendManager.Describe(bro)}");
        });
    }

    /// <summary>내 행이 있으면 그 행, 없으면 initial 로 만든 뒤 onDone(inDate, 행 — 새로 만들었으면 null, 실패 문구).</summary>
    public static void GetOrInsertMyRow(string table, Func<Param> initial, Action<string, JsonData, string> onDone)
    {
        GetMyRow(table, (row, error) =>
        {
            if (error != null) { onDone?.Invoke(null, null, error); return; }

            string inDate = BackendJson.Str(row, "inDate");
            if (!string.IsNullOrEmpty(inDate)) { onDone?.Invoke(inDate, row, null); return; }

            Insert(table, initial != null ? initial() : new Param(), (newInDate, insertError) =>
                onDone?.Invoke(newInDate, null, insertError));
        });
    }
}

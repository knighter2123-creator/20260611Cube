using System;
using BackEnd;
using UnityEngine;

/// <summary>
/// 뒤끝 닉네임 — 뒤끝 계정의 닉네임(서버 전체에서 중복 불가)을 씁니다.
///   읽기: GetUserInfo 의 row.nickname
///   쓰기: 처음이면 CreateNickname, 이미 있으면 UpdateNickname
/// </summary>
public class BackendNicknameService : INicknameService
{
    public void FetchMine(Action<string, string> onDone)
    {
        BackendManager.Call(cb => Backend.BMember.GetUserInfo(cb), bro =>
        {
            if (bro == null || !bro.IsSuccess())
            {
                Debug.LogWarning($"[Nickname] 내 닉네임 조회 실패: {BackendManager.Describe(bro)}");
                onDone?.Invoke(null, ToResult(bro).Message);
                return;
            }

            string nickname = string.Empty;
            try
            {
                UserInfoResponse info = JsonUtility.FromJson<UserInfoResponse>(bro.GetReturnValue());
                if (info?.row != null && !string.IsNullOrEmpty(info.row.nickname)) nickname = info.row.nickname;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Nickname] 계정 정보 읽기 실패: {e.Message}");
                onDone?.Invoke(null, "닉네임 정보를 읽지 못했습니다.");
                return;
            }
            onDone?.Invoke(nickname, null);
        });
    }

    public void Set(string nickname, bool hasServerName, Action<NicknameResult> onDone)
    {
        Action<BackendReturnObject> handle = bro =>
        {
            NicknameResult result = ToResult(bro);
            if (!result.Ok) Debug.LogWarning($"[Nickname] 닉네임 '{nickname}' 설정 실패: {BackendManager.Describe(bro)}");
            onDone?.Invoke(result);
        };

        if (hasServerName) BackendManager.Call(cb => Backend.BMember.UpdateNickname(nickname, cb), handle);
        else               BackendManager.Call(cb => Backend.BMember.CreateNickname(nickname, cb), handle);
    }

    private static NicknameResult ToResult(BackendReturnObject bro)
    {
        if (bro != null && bro.IsSuccess()) return NicknameResult.Success;

        if (BackendManager.IsNetworkError(bro))
            return new NicknameResult(NicknameResultKind.Offline, "서버에 연결할 수 없습니다. 인터넷 연결을 확인해 주세요.");

        switch (bro.GetStatusCode())
        {
            case "409": return new NicknameResult(NicknameResultKind.Duplicate, "이미 사용 중인 닉네임입니다.");
            case "400": return new NicknameResult(NicknameResultKind.Invalid,   "사용할 수 없는 닉네임입니다.");
            case "401":
            case "403": return new NicknameResult(NicknameResultKind.Session,   "로그인이 만료되었습니다. 다시 로그인해 주세요.");
            default:    return new NicknameResult(NicknameResultKind.Other,     $"닉네임을 정하지 못했습니다. ({bro.GetStatusCode()})");
        }
    }

#pragma warning disable 0649   // JsonUtility 가 채움
    [Serializable] private class UserInfoResponse { public UserInfoRow row; }
    [Serializable] private class UserInfoRow { public string nickname; }
#pragma warning restore 0649
}

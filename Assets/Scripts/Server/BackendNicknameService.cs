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
                onDone?.Invoke(null, GameMessages.Nickname.readFailed);
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
            return new NicknameResult(NicknameResultKind.Offline, GameMessages.Common.offlineCheck);

        switch (bro.GetStatusCode())
        {
            case "409": return new NicknameResult(NicknameResultKind.Duplicate, GameMessages.Nickname.duplicate);
            case "400": return new NicknameResult(NicknameResultKind.Invalid,   GameMessages.Nickname.invalid);
            case "401":
            case "403": return new NicknameResult(NicknameResultKind.Session,   GameMessages.Nickname.sessionExpired);
            default:    return new NicknameResult(NicknameResultKind.Other,     ChatText.Fill(GameMessages.Nickname.failedCode, "code", bro.GetStatusCode()));
        }
    }

#pragma warning disable 0649   // JsonUtility 가 채움
    [Serializable] private class UserInfoResponse { public UserInfoRow row; }
    [Serializable] private class UserInfoRow { public string nickname; }
#pragma warning restore 0649
}

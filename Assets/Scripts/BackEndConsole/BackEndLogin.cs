using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 뒤끝 SDK namespace 추가
using BackEnd;

public class BackendLogin
{
    private static BackendLogin _instance = null;

    public static BackendLogin Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new BackendLogin();
            }

            return _instance;
        }
    }

    // ⚠ 뒤끝 가이드 실습용 — 동기 호출이라 응답이 올 때까지 게임이 멈춥니다. 실제 게임 로그인은 AuthManager 가 합니다.
    //   결과를 알 수 있도록 성공 여부(bool)를 돌려줍니다.

    public bool CustomSignUp(string id, string pw)
    {
        Debug.Log("회원가입을 요청합니다.");

        var bro = Backend.BMember.CustomSignUp(id, pw);

        if (bro.IsSuccess())
        {
            Debug.Log("회원가입에 성공했습니다. : " + bro);
            return true;
        }

        Debug.LogError("회원가입에 실패했습니다. : " + bro);
        return false;
    }

    public bool CustomLogin(string id, string pw)
    {
        Debug.Log("로그인을 요청합니다.");

        var bro = Backend.BMember.CustomLogin(id, pw);

        if (bro.IsSuccess())
        {
            Debug.Log("로그인이 성공했습니다. : " + bro);
            return true;
        }

        // 401 "bad customId" = 아직 가입하지 않은 아이디 → 가입하면 됨 (CustomLoginOrSignUp)
        Debug.LogWarning("로그인이 실패했습니다. : " + bro);
        return false;
    }

    /// <summary>로그인해 보고, 아직 없는 아이디(401 bad customId)면 가입합니다. 가입하면 바로 로그인된 상태가 됩니다.</summary>
    public bool CustomLoginOrSignUp(string id, string pw)
    {
        var bro = Backend.BMember.CustomLogin(id, pw);
        if (bro.IsSuccess())
        {
            Debug.Log("로그인이 성공했습니다. : " + bro);
            return true;
        }

        bool notRegistered = bro.GetStatusCode() == "401" && (bro.GetMessage() ?? string.Empty).Contains("customId");
        if (!notRegistered)
        {
            // 비밀번호가 틀림(bad customPassword) 등 — 가입으로 해결되지 않음
            Debug.LogError("로그인이 실패했습니다. : " + bro);
            return false;
        }

        Debug.Log($"'{id}' 계정이 없어 회원가입합니다.");
        return CustomSignUp(id, pw);
    }

    public void UpdateNickname(string nickname)
    {
        Debug.Log("닉네임 변경을 요청합니다.");

        var bro = Backend.BMember.UpdateNickname(nickname);

        if (bro.IsSuccess())
        {
            Debug.Log("닉네임 변경에 성공했습니다 : " + bro);
        }
        else
        {
            Debug.LogError("닉네임 변경에 실패했습니다 : " + bro);
        }
    }
}
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Stage↔Shop 씬 이동 공용 도구(두 버튼의 중복 코드 통합) — 이동 중이면 무시 → 빌드에 씬 있는지 확인 → 저장 → 비동기 로드 await → finally 로 잠금 해제.</summary>
public static class ShopSceneLoader
{
    // 지금 씬을 옮기는 중인가. 두 버튼이 공유해야 하므로 static 입니다.
    private static bool isLoading;

    public static bool IsLoading => isLoading;

    /// <summary>플레이 시작 시 static 초기화 — 도메인 리로드가 꺼져 있으면 지난 플레이의 isLoading=true 가 남아 버튼이 안 먹음.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => isLoading = false;

    /// <summary>저장 후 sceneName 으로 이동 (버튼용 fire-and-forget).</summary>
    public static void SaveAndLoad(string sceneName)
    {
        // 1) 연타 방지
        if (isLoading) return;

        // 2) 빌드에 없는 씬은 잠금 걸기 전에 실패 처리 (버튼이 살아 있게)
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[ShopSceneLoader] '{sceneName}' 씬을 불러올 수 없습니다. " +
                           "File → Build Profiles 의 Scene List 에 등록됐는지, 이름 철자가 맞는지 확인하세요.");
            return;
        }

        isLoading = true;

        // 3) 저장 (MonoBehaviour 라 ?. 대신 != null — 파괴된 오브젝트도 걸러냄)
        if (SaveManager.Instance != null) SaveManager.Instance.Save();

        // 4~5) 반환값(Awaitable)을 버리는 것이 의도이므로 '_ =' 로 받아 경고를 없앱니다.
        _ = LoadAsync(sceneName);
    }

    /// <summary>LoadSceneAsync 를 Unity 6 Awaitable 로 await — MonoBehaviour·코루틴 없이 static 에서 기다리고 로딩 중 화면이 얼지 않음.</summary>
    private static async Awaitable LoadAsync(string sceneName)
    {
        try
        {
            await SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception e)
        {
            // fire-and-forget 예외는 조용히 사라지므로 직접 잡아 로그
            Debug.LogException(e);
        }
        finally
        {
            // 성공했든 실패했든 잠금을 풉니다. finally 는 return/예외와 상관없이 항상 실행됩니다.
            isLoading = false;
        }
    }
}

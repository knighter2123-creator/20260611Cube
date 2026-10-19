using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage ↔ Shop 씬 이동 공용 도구. OpenShopButton / ShopSceneController 가 같이 씁니다.
///
/// [왜 따로 뺐나]
///   두 버튼은 "저장 → 씬 이동" 이라는 똑같은 일을 하고 방향만 다릅니다.
///   예전에는 두 파일에 같은 코드가 복사돼 있어서, 연타 방지 같은 수정을 하려면 두 군데를 고쳐야 했습니다.
///   공통 부분을 한 곳에 두면 고칠 곳도 한 곳입니다.
///
/// [작동 순서]
///   1) 이미 이동 중이면 무시      — 버튼 연타로 LoadScene 이 두 번 걸리는 것 방지
///   2) 씬이 빌드에 있는지 확인    — 없으면 잠금을 걸기 전에 실패를 알리고 끝냄 (잠금이 영원히 남지 않게)
///   3) 저장                       — 씬이 내려가기 전에 진행을 디스크에 기록
///   4) 비동기 로드 + await        — Unity 6 의 Awaitable 기능. 로드 중에도 프레임이 멈추지 않음
///   5) 잠금 해제 (finally)        — 성공이든 예외든 반드시 풀림
/// </summary>
public static class ShopSceneLoader
{
    // 지금 씬을 옮기는 중인가. 두 버튼이 공유해야 하므로 static 입니다.
    private static bool isLoading;

    public static bool IsLoading => isLoading;

    /// <summary>
    /// ─── 왜 static 값을 직접 초기화하나 (학습 포인트) ─────────────────────────
    /// Project Settings → Editor → "Enter Play Mode Options" 에서 도메인 리로드를 끄면
    /// 플레이 버튼을 다시 눌러도 static 변수가 지난 플레이의 값을 그대로 들고 있습니다.
    /// 지난번 로드 도중 플레이를 멈췄다면 isLoading 이 true 로 남아 버튼이 영영 안 먹습니다.
    /// SubsystemRegistration 은 플레이 시작 시 가장 먼저 도는 시점이라 여기서 되돌려 둡니다.
    /// (도메인 리로드가 켜져 있으면 아무 영향 없는 안전장치입니다)
    /// ───────────────────────────────────────────────────────────────────────
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => isLoading = false;

    /// <summary>
    /// 저장 후 sceneName 으로 이동합니다. 버튼에서 바로 부를 수 있게 결과를 기다리지 않는 형태(fire-and-forget)입니다.
    /// </summary>
    public static void SaveAndLoad(string sceneName)
    {
        // 1) 연타 방지
        if (isLoading) return;

        // 2) Build Settings(Scenes In Build) 에 없는 씬은 LoadScene 이 에러만 내고 아무 일도 안 일어납니다.
        //    잠금을 걸기 전에 확인해야, 실패했을 때 버튼이 계속 살아 있습니다.
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[ShopSceneLoader] '{sceneName}' 씬을 불러올 수 없습니다. " +
                           "File → Build Profiles 의 Scene List 에 등록됐는지, 이름 철자가 맞는지 확인하세요.");
            return;
        }

        isLoading = true;

        // 3) 저장.
        //    ※ SaveManager 는 MonoBehaviour 라 '?.' 대신 '!= null' 로 검사합니다.
        //      '?.' 는 C# 의 진짜 null 만 보고, 유니티가 파괴한 오브젝트(가짜 null)는 통과시켜 버립니다.
        if (SaveManager.Instance != null) SaveManager.Instance.Save();

        // 4~5) 반환값(Awaitable)을 버리는 것이 의도이므로 '_ =' 로 받아 경고를 없앱니다.
        _ = LoadAsync(sceneName);
    }

    /// <summary>
    /// ─── Awaitable (Unity 6 신기능, 학습 포인트) ───────────────────────────────
    /// 예전에는 비동기 로드를 기다리려면 코루틴(IEnumerator + yield return op)을 써야 했고,
    /// 코루틴은 MonoBehaviour 가 있어야만 돌릴 수 있었습니다.
    /// Unity 6 부터는 AsyncOperation 을 바로 await 할 수 있고, 반환형으로 Task 대신
    /// 유니티 전용 Awaitable 을 씁니다 (메인 스레드에서 이어지고, 할당이 적음).
    /// 그래서 이렇게 static 클래스 안에서도 "로드가 끝날 때까지 기다리기"가 가능합니다.
    ///
    /// LoadScene(동기) 대신 LoadSceneAsync 를 쓰면 로딩하는 동안 화면이 얼어붙지 않습니다.
    /// ───────────────────────────────────────────────────────────────────────
    /// </summary>
    private static async Awaitable LoadAsync(string sceneName)
    {
        try
        {
            await SceneManager.LoadSceneAsync(sceneName);
        }
        catch (Exception e)
        {
            // fire-and-forget 로 부른 비동기 함수의 예외는 아무도 받아 주지 않아 조용히 사라질 수 있습니다.
            // 직접 잡아서 로그를 남깁니다.
            Debug.LogException(e);
        }
        finally
        {
            // 성공했든 실패했든 잠금을 풉니다. finally 는 return/예외와 상관없이 항상 실행됩니다.
            isLoading = false;
        }
    }
}

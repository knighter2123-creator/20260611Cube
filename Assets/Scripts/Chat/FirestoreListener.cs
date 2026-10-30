using System;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore 실시간 리스너 하나 — 시작 · 멈춤 · 오류로 끊김 감지를 한곳에 모았습니다.
/// 오류로 끝나면(권한 거부 등) onFailed(문구) 를 부르고, Stop 으로 멈춘 것은 오류로 보지 않습니다.
/// </summary>
public sealed class FirestoreListener
{
    private readonly string logName;        // 로그 머리 — 예: "[Direct] 대화방"
    private readonly string startFailed;    // 시작하지 못했을 때 문구
    private readonly string dropped;        // 연결된 뒤 끊겼을 때 문구
    private ListenerRegistration reg;

    public FirestoreListener(string logName, string startFailed, string dropped = null)
    {
        this.logName     = logName;
        this.startFailed = startFailed;
        this.dropped     = dropped ?? startFailed;
    }

    public bool IsActive => reg != null;

    /// <summary>리스너를 엽니다 (이미 열려 있으면 그대로). 시작부터 실패하면 onFailed 를 바로 부르고 false.</summary>
    public bool Start(Func<ListenerRegistration> listen, Action<string> onFailed)
    {
        if (reg != null) return true;

        try
        {
            ListenerRegistration started = listen();   // 스냅샷 콜백은 메인 스레드에서 옴
            reg = started;

            started.ListenerTask.ContinueWithOnMainThread(task =>
            {
                if (reg != started) return;   // Stop 으로 멈춘 것
                reg = null;
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"{logName} 리스너 끊김: {task.Exception}");
                    onFailed?.Invoke(dropped);
                }
            });
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"{logName} 리스너 시작 실패: {e}");
            reg = null;
            onFailed?.Invoke(startFailed);
            return false;
        }
    }

    public void Stop()
    {
        ListenerRegistration stopping = reg;
        reg = null;   // 먼저 비워서 ListenerTask 완료를 '오류' 로 보지 않게 함
        stopping?.Stop();
    }
}

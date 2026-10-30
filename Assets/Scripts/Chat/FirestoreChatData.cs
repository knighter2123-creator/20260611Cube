using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Extensions;   // ContinueWithOnMainThread
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Firestore 채팅 서비스들(전체 채팅 · 개인 메시지 · 친구)이 함께 쓰는 문서 변환과 쓰기 도우미.
/// </summary>
public static class FirestoreChatData
{
    private const int EXPIRE_DAYS = 7;

    // ───────────────────────────── 두 사람 문서 ─────────────────────────────

    /// <summary>두 사람 사이 문서 ID — 누가 만들어도 같은 문서가 되게 작은 UID 먼저.</summary>
    public static string PairId(string a, string b) => string.CompareOrdinal(a, b) < 0 ? a + "_" + b : b + "_" + a;

    /// <summary>members 배열 — PairId 와 같은 순서.</summary>
    public static List<string> SortedPair(string a, string b)
        => string.CompareOrdinal(a, b) < 0 ? new List<string> { a, b } : new List<string> { b, a };

    /// <summary>문서에 남은 닉네임. 비어 있으면 익명.</summary>
    public static string NameOrAnonymous(object value)
        => value is string s && s.Length > 0 ? s : PlayerProfile.ANONYMOUS_PREFIX;

    public static string NameOf(Dictionary<string, object> names, string uid)
        => NameOrAnonymous(names != null && names.TryGetValue(uid, out object v) ? v : null);

    // ───────────────────────────── 메시지 ─────────────────────────────
    //   chat/{id} 와 dmRooms/{id}/messages/{id} 는 같은 필드입니다.
    //     uid · nickname · progress · text · sentAt(UTC ms, 정렬 기준) · createdAt(서버 시각) · expireAt(TTL)

    /// <summary>문서 → 메시지. 글이 비어 있으면 false.</summary>
    public static bool TryReadMessage(DocumentSnapshot doc, out ChatMessage m)
    {
        doc.TryGetValue("uid",      out string uid);
        doc.TryGetValue("nickname", out string nickname);
        doc.TryGetValue("progress", out long   progress);   // Firestore 정수는 64비트로 옴
        doc.TryGetValue("text",     out string text);
        doc.TryGetValue("sentAt",   out long   sentAt);

        m = new ChatMessage
        {
            Id       = doc.Id,
            Uid      = uid,
            Nickname = NameOrAnonymous(nickname),
            Progress = (int)progress,
            Text     = text,
            SentAt   = sentAt,
        };
        return !string.IsNullOrEmpty(text);
    }

    /// <summary>메시지 → 문서. expireAt 은 보낸 뒤 EXPIRE_DAYS 일 — 콘솔에서 TTL 정책을 켜면 그때 자동 삭제됩니다.</summary>
    public static Dictionary<string, object> ToData(ChatMessage m) => new Dictionary<string, object>
    {
        { "uid",       m.Uid },
        { "nickname",  m.Nickname },
        { "progress",  m.Progress },
        { "text",      m.Text },
        { "sentAt",    m.SentAt },
        { "createdAt", FieldValue.ServerTimestamp },
        { "expireAt",  Timestamp.FromDateTime(DateTime.UtcNow.AddDays(EXPIRE_DAYS)) },
    };

    /// <summary>
    /// 메시지 리스너 스냅샷의 바뀐 부분 → 새 메시지 / 빠진 메시지 Id.
    ///   Added   — 새 메시지. 내 메시지는 서버 확인 전에 먼저 옵니다.
    ///   Removed — 서버가 거절한 내 메시지(보안 규칙 등), 최근 N개 밖으로 밀려난 옛 메시지.
    ///   Modified — 서버 시각(createdAt)이 채워진 것뿐 → 화면엔 영향 없음.
    /// </summary>
    public static void ReadMessageChanges(QuerySnapshot snap, out List<ChatMessage> added, out List<string> removed)
    {
        added   = new List<ChatMessage>();
        removed = new List<string>();
        foreach (DocumentChange change in snap.GetChanges())
        {
            switch (change.ChangeType)
            {
                case DocumentChange.Type.Added:
                    if (TryReadMessage(change.Document, out ChatMessage m)) added.Add(m);
                    break;
                case DocumentChange.Type.Removed:
                    removed.Add(change.Document.Id);
                    break;
            }
        }
    }

    // ───────────────────────────── 쓰기 ─────────────────────────────

    /// <summary>
    /// 쓰기를 실행하고 결과를 메인 스레드에서 onDone 으로 알립니다 — 성공이면 null, 실패면 error.
    /// 내 화면에는 리스너가 서버 확인 전에 바로 보여 주므로, 이 콜백은 서버가 받아 준 뒤에 옵니다.
    /// </summary>
    public static void Run(string logTag, Func<Task> write, string error, Action<string> onDone)
    {
        try
        {
            write().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"{logTag} 쓰기 실패: {task.Exception}");
                    onDone?.Invoke(error);
                    return;
                }
                onDone?.Invoke(null);
            });
        }
        catch (Exception e)
        {
            Debug.LogError($"{logTag} 쓰기 호출 실패: {e}");
            onDone?.Invoke(error);
        }
    }
}

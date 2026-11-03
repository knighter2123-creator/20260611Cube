/// <summary>
/// 뒤끝 이전 중 — 아직 Firestore 를 쓰는 기능의 스위치.
///
/// 로그인이 뒤끝으로 바뀌어 Firebase 로그인이 없으므로, Firestore 보안 규칙(본인만 읽고 쓰기)이 모든 요청을 거부합니다.
/// 그래서 아래 기능은 뒤끝 버전이 생길 때까지 꺼 둡니다. 기능을 뒤끝으로 옮기면 그 기능에서 이 검사를 지웁니다.
///   - CloudSave           클라우드 세이브           → 뒤끝 게임 정보
///   - ServerAccountData   계정 삭제 시 서버 기록 삭제 → 지금은 뒤끝 탈퇴만 함
///   - ChatManager         채팅·친구·개인 메시지      → 지금은 Local(나에게만 보임)로 동작
/// 모든 기능을 옮기면 이 파일과 Firestore 코드를 지웁니다.
/// </summary>
public static class LegacyFirestore
{
    // const 가 아닌 이유: const false 면 검사 뒤 코드가 '도달할 수 없는 코드' 경고(CS0162)로 뒤덮임
    public static readonly bool Enabled = false;
}

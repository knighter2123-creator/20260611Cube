/// <summary>
/// 임시 버프의 종류. AugmentManager 가 종류별로 모아서 배율을 곱합니다.
///
/// 새 종류를 추가하려면
///   ① 여기에 한 줄 추가
///   ② AugmentManager.Buffs.cs 의 RecalculateTemp() 에 case 추가
///   ③ AugmentManager.cs 에 static 접근자 추가
/// 세 곳을 고쳐야 합니다. (리팩토링 문서 C-1 항목이 이걸 한 곳으로 줄이는 방법입니다)
///
/// ※ enum 은 UnityEngine.Object 가 아니라서 파일 이름 규칙에 묶이지 않습니다.
///   그래도 찾기 쉽게 이름을 맞춰 두었습니다.
/// </summary>
public enum AugmentBuffKind
{
    SpawnDelay,     // 적 생성 주기 (줄이면 적이 더 빨리 몰려나옴)
    EnemyDefense    // 적 방어력
}

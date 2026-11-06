/// <summary>임시 버프 종류 — AugmentManager 가 종류별로 배율을 곱함.</summary>
// 새 종류 추가 시 세 곳: ① 여기 ② AugmentManager.Buffs.cs RecalculateTemp 의 case ③ AugmentManager.cs static 접근자
public enum AugmentBuffKind
{
    SpawnDelay,     // 적 생성 주기 (줄이면 적이 더 빨리 몰려나옴)
    EnemyDefense    // 적 방어력
}

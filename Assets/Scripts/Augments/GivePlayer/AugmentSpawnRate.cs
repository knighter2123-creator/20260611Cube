using UnityEngine;

/// <summary>
/// 적 생성 주기 감소 = 적이 더 빨리 몰려나옴.
///
/// [주의] 이건 사실 '이득'입니다. 적이 빨리 나오면 그만큼 빨리 잡아서
/// 골드·경험치 획득 속도가 올라가니까요. 방치형에서 흔한 '파밍 가속' 카드입니다.
///
/// 다만 화면에 적이 너무 많아지면 프레임이 떨어지므로,
/// EnemyRespawn 의 maxTotalSpawn(보스 전까지 뽑을 잡몹 수)과
/// minRespawnDelay(주기 하한선)가 상한 역할을 계속 해줘야 합니다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Augment/적 생성 주기 감소", fileName = "_SpawnRate")]
public class AugmentSpawnRate : AugmentTempBuff
{
    protected override AugmentBuffKind Kind => AugmentBuffKind.SpawnDelay;
}

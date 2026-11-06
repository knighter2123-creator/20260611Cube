using UnityEngine;

/// <summary>적 생성 주기 감소(임시, 파밍 가속 이득 카드) — 적이 너무 많아져 프레임이 떨어지지 않게 EnemyRespawn 의 maxTotalSpawn·minRespawnDelay 가 상한 역할.</summary>
[CreateAssetMenu(menuName = "Game/Augment/적 생성 주기 감소", fileName = "_SpawnRate")]
public class AugmentSpawnRate : AugmentTempBuff
{
    protected override AugmentBuffKind Kind => AugmentBuffKind.SpawnDelay;
}

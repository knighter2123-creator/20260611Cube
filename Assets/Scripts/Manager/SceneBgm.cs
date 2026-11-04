using UnityEngine;

/// <summary>
/// 이 씬의 배경음. 씬에 들어갈 때 SoundManager 로 재생합니다 — 코드 없이 씬마다 BGM 을 정하는 자리.
///
/// [붙이는 위치]  각 씬(StageScene, ShopScene, ...)의 아무 오브젝트에 하나. 클립을 넣으면 재생, 비워 두면 배경음을 멈춤.
///   같은 클립이 이미 재생 중이면 처음부터 다시 틀지 않습니다 (씬을 오가도 끊기지 않게 — SoundManager.PlayBgm).
///   볼륨은 설정 창의 배경음 슬라이더를 따릅니다.
/// </summary>
public class SceneBgm : MonoBehaviour
{
    [Tooltip("이 씬의 배경음. 비우면 이 씬에서는 배경음을 멈춥니다.")]
    [SerializeField] private AudioClip clip;

    [SerializeField] private bool loop = true;

    // Awake 가 아니라 Start — SoundManager(ManagerRoot 하위)가 Awake 에서 Instance 를 세운 뒤에 부르기 위해
    private void Start()
    {
        SoundManager sound = SoundManager.Instance;
        if (sound == null) return;   // 씬 단독 테스트 (ManagerRoot 없음)

        if (clip != null) sound.PlayBgm(clip, loop);
        else              sound.StopBgm();
    }
}

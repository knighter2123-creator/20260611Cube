using UnityEngine;

/// <summary>씬 배경음 지정 — 각 씬에 하나 두고 클립을 넣으면 진입 때 재생(비우면 멈춤), 같은 클립이면 이어서 재생해 씬을 오가도 안 끊김.</summary>
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

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UISpriteAnimation : MonoBehaviour
{
    [Header("프레임 (슬라이스된 스프라이트 순서대로)")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 24f;
    [SerializeField] private bool loop = false;
    [Tooltip("1회 재생 후 자동 파괴 (폭발 이펙트는 true 권장)")]
    [SerializeField] private bool destroyOnFinish = true;
    [SerializeField] private bool playOnEnable = true;

    private Image image;
    private Coroutine playing;

    void Awake()
    {
        image = GetComponent<Image>();
        image.raycastTarget = false; // ✅ 이펙트가 버튼 클릭을 막지 않도록
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0) return;
        if (playing != null) StopCoroutine(playing);
        playing = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        float frameTime = 1f / Mathf.Max(1f, fps);
        do
        {
            for (int i = 0; i < frames.Length; i++)
            {
                image.sprite = frames[i];
                yield return new WaitForSecondsRealtime(frameTime); // timeScale=0에서도 재생
            }
        }
        while (loop);

        playing = null;
        if (destroyOnFinish) Destroy(gameObject);
    }
}
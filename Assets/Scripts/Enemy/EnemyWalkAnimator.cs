using UnityEngine;

/// <summary>걷기 프레임을 sprite 교체로 넘기는 경량 애니메이터(수십 마리·풀 재사용에 Animator 는 과함) — 재생 속도가 실제 이동 속도를 따라 스턴·둔화가 그대로 보임.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyWalkAnimator : MonoBehaviour
{
    [Tooltip("순서대로 재생할 프레임 (Assets/Sprites/Enemy 의 시트를 슬라이스한 스프라이트)")]
    [SerializeField] private Sprite[] frames;

    [Tooltip("기본 속도(TargetMove.speed)로 움직일 때의 초당 프레임 수")]
    [SerializeField] private float framesPerSecond = 8f;

    private SpriteRenderer sr;
    private TargetMove     move;
    private Enemy          enemy;
    private float          phase;   // 누적 프레임 진행도 (정수부 = 현재 프레임)
    private int            shown = -1;

    void Awake()
    {
        sr    = GetComponent<SpriteRenderer>();
        move  = GetComponent<TargetMove>();
        enemy = GetComponent<Enemy>();
    }

    // 꺼낼 때마다 시작 프레임을 흩음 (무리가 같은 발을 동시에 내딛으면 어색)
    void OnEnable()
    {
        if (frames == null || frames.Length == 0) return;
        phase = Random.Range(0, frames.Length);   // int 버전은 최댓값 미포함 → 인덱스 범위 안전
        shown = -1;
        Show();
    }

    void Update()
    {
        if (frames == null || frames.Length < 2) return;
        if (enemy != null && enemy.isDead) return;   // 사망 연출 중엔 EnemyDeathAnimator 가 스프라이트를 넘김

        phase += framesPerSecond * SpeedScale() * Time.deltaTime;
        if (phase >= frames.Length) phase %= frames.Length;
        Show();
    }

    /// <summary>기본 속도 대비 현재 속도. TargetMove 가 없으면 1 (항상 기본 속도로 재생).</summary>
    private float SpeedScale()
    {
        if (move == null || move.speed <= 0f) return 1f;
        return move.CurrentSpeed / move.speed;
    }

    // 프레임이 바뀔 때만 대입 — sprite 대입은 렌더러 갱신을 일으키므로 매 프레임 하지 않습니다.
    private void Show()
    {
        int index = (int)phase;
        if (index == shown) return;
        shown = index;
        sr.sprite = frames[index];
    }
}

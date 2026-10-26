using UnityEngine;

/// <summary>
/// 적의 걷기(날갯짓) 프레임을 순서대로 넘겨 주는 가벼운 스프라이트 애니메이터.
///
/// ─── 왜 Animator 가 아니라 직접 넘기는가? (학습 포인트) ─────────────────
/// 걷기 4프레임을 반복하는 게 전부라면 Animator(상태 머신 + AnimationClip)는 과합니다.
/// 적은 수십 마리가 동시에 돌고 풀에서 계속 재사용되므로,
/// "sprite 하나 바꾸기"만 하는 쪽이 싸고, 풀 재사용 시 상태가 남을 걱정도 없습니다.
///
/// 재생 속도는 TargetMove 의 실제 이동 속도를 따라갑니다.
///   · 스턴      → CurrentSpeed 0 → 제자리에서 멈춤
///   · 둔화      → 발걸음도 느려짐
///   · 1-5 같은 고속 스테이지(속도 배율) → 발걸음도 빨라짐
/// 그래서 디버프 연출을 따로 만들지 않아도 "느려졌다/멈췄다"가 눈에 보입니다.
/// ────────────────────────────────────────────────────────────────────
/// </summary>
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

    // 풀에서 다시 꺼낼 때마다 시작 프레임을 흩어 줍니다.
    // 같이 스폰된 무리가 전부 같은 발을 동시에 내딛으면 어색하기 때문입니다.
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
        if (enemy != null && enemy.isDead) return;   // 사망 연출 중엔 마지막 자세로 고정

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

using UnityEngine;

public class TargetMove : MonoBehaviour
{
    [Header("이동 경로 (순서대로 넣어주세요)")]
    private Transform[] waypoints; // 타겟 오브젝트들을 넣을 배열

    [Header("이동 옵션")]
    public float speed = 0.5f;          // 이동 속도
    public float arrivalDistance = 0.1f; // 타겟에 도착했다고 판정할 거리

    private int currentTargetIndex = 0; // 현재 목표 타겟의 인덱스
    private float initialZ;             // Z축 고정용 변수
    private bool isInitialized = false; // 경로가 설정 되었는 지 확인
    
    public float GetSpeed()
    {
        return speed;
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    // [중요] 생성 시 스포너가 이 함수를 호출하여 경로를 넘겨줍니다.
    public void SetupPath(Transform[] paths)
    {
        waypoints = paths;
        initialZ = transform.position.z; // 내 현재 Z축 깊이 기억
        currentTargetIndex = 0;          // 처음 타겟부터 시작
        isInitialized = true;            // 이동 시작 가능 상태
    }
    void Start()
    {
        // 시작할 때의 Z축 깊이를 기억하여 화면에서 사라지는 것 방지
        initialZ = transform.position.z;
    }

    void Update()
    {
        // 경로 설정이 완료되지 않았거나 배열이 비었으면 대기
        if (!isInitialized || waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentTargetIndex];

        if (target != null)
        {
            // Z축 렌더링 사라짐 방지
            Vector3 targetPosition = target.position;
            targetPosition.z = initialZ; 

            // 이동
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

            // 거리 체크 후 다음 타겟 변경
            float distance = Vector2.Distance(transform.position, targetPosition);
            if (distance <= arrivalDistance)
            {
                currentTargetIndex = (currentTargetIndex + 1) % waypoints.Length;
            }
        }
    }
}

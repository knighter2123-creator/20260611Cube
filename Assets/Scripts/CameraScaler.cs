using UnityEngine;

public class CameraScaler : MonoBehaviour
{
    public float targetWidth = 19.2f; // 원래 보여주고 싶은 타일맵의 가로 월드 크기

    void Start()
    {
        Camera cam = GetComponent<Camera>();
        // 해상도 비율에 맞춰서 어orthographicSize를 자동으로 계산
        cam.orthographicSize = (targetWidth / cam.aspect) / 2f;
    }
}
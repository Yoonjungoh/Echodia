using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public Vector3 Offset = new Vector3(0, 2, -4); // 캐릭터 뒤쪽 카메라 위치
    public float FollowSpeed = 1000f;          // 따라가는 속도
    public float RotationSpeed = 5f;         // 회전 감도
    public float MinPitch = -30f;            // 아래로 보는 각도 제한
    public float MaxPitch = 30f;             // 위로 보는 각도 제한

    [Header("Zoom Settings")]
    public float ZoomSpeed = 5f;             // 마우스 휠 반응 속도
    public float MinZoom = -5f;              // 가장 근접 (숫자가 커질수록 가까움)
    public float MaxZoom = -12f;             // 가장 멀리 (숫자가 작을수록 멀리)

    private MyPlayerController _target;
    private float _yaw;   // 좌우 회전 (Y축)
    private float _pitch; // 상하 회전 (X축)

    public void Init()
    {
        if (_target == null)
        {
            if (Managers.Scene.CurrentScene == Define.Scene.GameRoom)
            {
                _target = Managers.GameRoomObject.MyPlayer;
            }
        }

        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;
    }

    void LateUpdate()
    {
        if (_target == null)
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        // 회전 담당 부분
        _yaw += mouseX * RotationSpeed;
        _pitch -= mouseY * RotationSpeed;
        _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

        // 줌 담당 부분
        Offset.z += scroll * ZoomSpeed;
        Offset.z = Mathf.Clamp(Offset.z, MaxZoom, MinZoom);

        // 위치 이동 및 회전 적용 부분
        Vector3 desiredPosition = _target.transform.position + rotation * Offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, FollowSpeed * Time.deltaTime);
        transform.rotation = rotation;

        // 캐릭터 회전 부분 보정
        if (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
        {
            Vector3 forward = new Vector3(transform.forward.x, 0f, transform.forward.z);
            _target.transform.forward = Vector3.Lerp(_target.transform.forward, forward, Time.deltaTime * _target.RotateSpeed);
        }
    }
}

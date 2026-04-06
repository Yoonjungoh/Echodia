using UnityEngine;

/// <summary>
/// 맵에 진입할 때(순방향, 앞쪽 맵에서 오는 경우) 플레이어가 스폰되는 위치 마커.
/// 씬에 하나만 배치.
/// </summary>
public class MapEnterPoint : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 0f, 0.8f);
        Gizmos.DrawSphere(transform.position, 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);

        UnityEditor.Handles.color = new Color(0f, 0.8f, 0f, 0.8f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.2f, "EnterPoint");
    }
#endif
}

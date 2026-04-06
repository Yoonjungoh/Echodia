using UnityEngine;

/// <summary>
/// 씬에 배치하는 몬스터 스포너 마커.
/// MapEditor의 GenerateMapMeta가 이 컴포넌트를 탐색해 메타 JSON에 기록한다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Tooltip("스폰할 몬스터 타입 ID (Protocol.MonsterType 값)")]
    public int MonsterTypeId;

    [Tooltip("동시에 유지할 몬스터 최대 수")]
    public int Count = 1;

    [Tooltip("몬스터 사망 후 재스폰까지 대기 시간 (초)")]
    public float RespawnSeconds = 30f;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.8f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 1f);

        UnityEditor.Handles.color = new Color(0.8f, 0f, 0f, 0.9f);
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.5f,
            $"Monster [{MonsterTypeId}] x{Count} ({RespawnSeconds}s)"
        );
    }
#endif
}

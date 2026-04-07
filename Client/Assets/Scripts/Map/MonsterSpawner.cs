using Google.Protobuf.Protocol;
using UnityEngine;

/// <summary>
/// 씬에 배치하는 몬스터 스포너 마커.
/// MapEditor의 GenerateMapMeta가 이 컴포넌트를 탐색해 메타 JSON에 기록한다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    [Tooltip("스폰할 몬스터 타입 ID (Protocol.MonsterType 값)")]
    public MonsterType MonsterType;

    [Tooltip("동시에 유지할 몬스터 최대 수")]
    public int Count = 1;

    [Tooltip("몬스터 사망 후 재스폰까지 대기 시간 (초)")]
    public float RespawnSeconds = 30f;

    [Tooltip("스포너 중심으로부터 랜덤 스폰 반경 (0이면 정확한 위치에 스폰)")]
    public float SpawnRadius = 0f;

#if UNITY_EDITOR
    private static GUIStyle _labelStyle;
    private static GUIStyle LabelStyle => _labelStyle ??= new GUIStyle
    {
        normal    = { textColor = new Color(1f, 0.3f, 0.3f) },
        fontStyle = FontStyle.Bold,
        fontSize  = 11,
    };

    private void OnDrawGizmos()
    {
        // 중심 - 항상 표시
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);

        // 바닥 디스크 - 위치를 한눈에 파악하기 쉽게
        UnityEditor.Handles.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, 0.6f);
    }

    private void OnDrawGizmosSelected()
    {
        // 랜덤 스폰 범위
        if (SpawnRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.25f);
            Gizmos.DrawSphere(transform.position, SpawnRadius);

            UnityEditor.Handles.color = new Color(1f, 0.5f, 0.2f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, SpawnRadius);
        }

        // 정보 라벨 (빨간 굵은 텍스트)
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.8f,
            $"[{MonsterType}] x{Count}  Respawn:{RespawnSeconds}s  r={SpawnRadius}",
            LabelStyle
        );
    }
#endif
}

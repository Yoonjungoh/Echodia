using Google.Protobuf.Protocol;
using UnityEngine;

/// <summary>
/// 모든 포탈의 베이스 클래스.
/// 포탈 프리팹에는 반드시 IsTrigger = true인 Collider가 있어야 한다.
/// </summary>
public abstract class BasePortal : MonoBehaviour
{
    public abstract PortalType PortalType { get; }

    private void OnTriggerEnter(Collider other)
    {
        // 로컬 플레이어만 반응 (MyPlayerController 컴포넌트로 구분)
        if (other.GetComponent<MyPlayerController>() == null)
            return;

        OnPlayerEnter(other.gameObject);
    }

    /// <summary>로컬 플레이어가 포탈 트리거 안으로 진입했을 때 호출된다.</summary>
    protected abstract void OnPlayerEnter(GameObject player);
}

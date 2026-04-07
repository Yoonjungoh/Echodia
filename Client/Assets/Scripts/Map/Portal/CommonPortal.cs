using Google.Protobuf.Protocol;
using UnityEngine;

/// <summary>
/// 일반 이동 포탈.
/// 플레이어가 트리거 안에 들어오면 서버에 맵 이동 요청을 보낸다.
/// </summary>
public class CommonPortal : BasePortal
{
    public override PortalType PortalType => PortalType.Common;

    protected override void OnPlayerEnter(GameObject player)
    {
        // TODO: 서버에 맵 이동 요청 패킷 전송
        // e.g. Managers.Network.Send(new C_MoveMap { ... });
        Debug.Log($"[CommonPortal] Player entered portal: {gameObject.name}");
    }
}

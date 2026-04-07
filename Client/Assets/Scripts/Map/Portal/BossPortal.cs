using Google.Protobuf.Protocol;
using UnityEngine;

/// <summary>
/// 보스 입장 포탈.
/// 현재는 입장 조건 미구현 상태로, 진입 시 아무 동작도 하지 않는다.
/// 추후 <see cref="CanEnter"/> 를 구현해 퀘스트·아이템 등 조건을 추가한다.
/// </summary>
public class BossPortal : BasePortal
{
    public override PortalType PortalType => PortalType.Boss;

    /// <summary>
    /// 보스 맵 입장 가능 여부.
    /// 조건 미구현 시 false를 반환한다.
    /// </summary>
    protected virtual bool CanEnter(GameObject player)
    {
        // TODO: 입장 조건 구현 (퀘스트 클리어, 특정 아이템 소지 등)
        return false;
    }

    protected override void OnPlayerEnter(GameObject player)
    {
        if (!CanEnter(player))
        {
            // TODO: 입장 불가 UI 안내 (팝업, 토스트 등)
            Debug.Log("[BossPortal] 입장 조건 미충족 — 보스 포탈 진입 불가");
            return;
        }

        // TODO: 보스 맵 입장 처리 (서버 패킷 전송 등)
        Debug.Log($"[BossPortal] Player entered boss portal: {gameObject.name}");
    }
}

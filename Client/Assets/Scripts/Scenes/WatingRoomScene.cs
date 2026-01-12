using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WatingRoomScene : BaseScene
{
    protected override void Init()
    {
        base.Init();

        // 방 입장 하겠다고 패킷 전송
        C_EnterWaitingRoom enterRoomPacket = new C_EnterWaitingRoom();
        enterRoomPacket.UserId = Managers.WaitingRoomObject.UserId;
        enterRoomPacket.RoomId = Managers.WaitingRoom.RoomInfo.RoomId;
        Managers.Network.Send(enterRoomPacket);

        Managers.UI.ShowSceneUI<UI_WaitingRoom>();
    }

    private void Awake()
    {
        Init();
    }
}

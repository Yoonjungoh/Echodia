using Google.Protobuf.Protocol;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoomScene : BaseScene
{
    protected override void Init()
    {
        base.Init();

        // 게임 입장 하겠다고 패킷 전송
        C_EnterGame enterGamePacket = new C_EnterGame()
        {
            PlayerId = Managers.Network.PlayerId,
            ServerId = Managers.GameRoom.ServerId,
            ChannelId = Managers.GameRoom.ChannelId,
        };
        
        Managers.Network.Send(enterGamePacket);
    }
    
    private void Awake()
    {
        Init();
    }
}

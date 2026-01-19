using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoomManager
{
    public int ServerId { get; set; } = -1; // 현재 접속한 GameRoom의 Server Id
    public int ChannelId { get; set; } = -1;    // 현재 접속한 GameRoom의 Channel Id
    public int MapId { get; set; } = -1;    // 맵 Id는 서버에서 받아옴

    public void Init()
    {

    }

    public void SetMapData(int serverId, int channelId)
    {
        ServerId = serverId;
        ChannelId = channelId;
    }

    public void ExitGame()
    {
        Managers.Scene.LoadScene(Define.Scene.ServerSelect);
    }

    public void Clear()
    {
        //ServerId = -1;
        //ChannelId = -1;
        //MapId = -1;
    }
}
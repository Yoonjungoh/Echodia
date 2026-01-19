using Google.Protobuf.Collections;
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameRoomManager
{
    public int ServerId { get; set; } = -1;
    public int ChannelId { get; set; } = -1;
    public int MapId { get; set; } = -1;    // ¸Ê Id´Â ¼­¹ö¿¡¼­ ¹Þ¾Æ¿È

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
        ServerId = -1;
        ChannelId = -1;
        MapId = -1;
    }
}
using Google.Protobuf.Protocol;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaitingRoomManager
{
    // 현재 Room Id, -1이면 방에 없음
    public RoomInfo RoomInfo { get; set; } = new RoomInfo();
    public bool IsRoomOwner { get { return RoomInfo.RoomOwnerId == Managers.WaitingRoomObject.UserId; } }
    public bool CanEnterWaitingRoom { get { return RoomInfo.CurrentPlayerCount < RoomInfo.MaxPlayerCount; } }
    public bool CanEnterGame { get { return RoomInfo.CurrentPlayerCount == RoomInfo.MaxPlayerCount; } }

    public void Init()
    {
        RoomInfo.RoomId = -1;
    }

    public void EnterRoom(RoomInfo roomInfo)
    {
        RoomInfo = roomInfo;
        if (CanEnterWaitingRoom == false)
            return;

        Managers.Scene.LoadScene(Define.Scene.WaitingRoom);
    }

    public void ExitRoom()
    {
        RoomInfo = new RoomInfo();
        RoomInfo.RoomId = -1;
        Managers.WaitingRoomObject.Clear();
        Managers.Scene.LoadScene(Define.Scene.Lobby);
    }
}
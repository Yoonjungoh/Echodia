using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf.Collections;
using System.Diagnostics;

class PacketHandler
{
    public static void S_AssignUserIdHandler(PacketSession session, IMessage packet)
    {
        // UI 찾는게 더 무겁고 패킷 캐스팅이 더 가벼우니 패킷 먼저 체크
        S_AssignUserId assignUserIdPacket = packet as S_AssignUserId;
        if (assignUserIdPacket == null)
        {
            return;
        }
        // TODO - 재할당 필요할 때 요청
    }

    public static void S_ExitRoomHandler(PacketSession session, IMessage packet)
    {
        S_ExitRoom exitRoomPacket = packet as S_ExitRoom;
        if (exitRoomPacket == null)
        {
            return;
        }

    }

    // 내가 게임에 입장할 때 패킷
    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_EnterGame enterGamePacket = packet as S_EnterGame;
        if (enterGamePacket == null)
        {
            return;
        }

    }

    public static void S_AttackHandler(PacketSession session, IMessage packet)
    {
        S_Attack attackPacket = packet as S_Attack;
        if (attackPacket == null)
        {
            return;
        }
    }

    // 게임에서 죽었을 때
    public static void S_LeaveGameHandler(PacketSession session, IMessage packet)
    {
        S_LeaveGame leaveGamePacket = packet as S_LeaveGame;

    }

    public static void S_SpawnHandler(PacketSession session, IMessage packet)
    {
        S_Spawn spawnPacket = packet as S_Spawn;

    }

    public static void S_DespawnHandler(PacketSession session, IMessage packet)
    {
        S_Despawn despawnPacket = packet as S_Despawn;

    }

    public static void S_MoveHandler(PacketSession session, IMessage packet)
    {
        S_Move movePacket = packet as S_Move;


    }

    public static void S_DieHandler(PacketSession session, IMessage packet)
    {
        S_Die diePacket = packet as S_Die;
    }

    public static void S_TimestampHandler(PacketSession session, IMessage packet)
    {
        S_Timestamp sereverTimestamp = packet as S_Timestamp;
    }

    public static void S_ChangeCreatureStateHandler(PacketSession session, IMessage packet)
    {
        S_ChangeCreatureState changeCreatureStatePacket = packet as S_ChangeCreatureState;
    }

    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {
        S_Connected connectedPacket = packet as S_Connected;
    }

    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        S_Login loginPacket = packet as S_Login;
        if (loginPacket == null)
        {
            return;
        }

    }

    public static void S_RequestPlayerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestPlayerList requestPlayerListPacket = packet as S_RequestPlayerList;
        if (requestPlayerListPacket == null)
        {
            return;
        }


    }

    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_CreatePlayer createPlaerPacket = packet as S_CreatePlayer;
        if (createPlaerPacket == null)
        {
            return;
        }
    }

    public static void S_DeletePlayerHandler(PacketSession session, IMessage packet)
    {
        S_DeletePlayer deletePlaerPacket = packet as S_DeletePlayer;
        if (deletePlaerPacket == null)
        {
            return;
        }
    }

    public static void S_UpdateCurrencyDataHandler(PacketSession session, IMessage packet)
    {
        S_UpdateCurrencyData updateCurrencyDataPacket = packet as S_UpdateCurrencyData;
        if (updateCurrencyDataPacket == null)
        {
            return;
        }
    }

    public static void S_UpdateCurrencyDataAllHandler(PacketSession session, IMessage packet)
    {
        S_UpdateCurrencyDataAll updateCurrencyDataAllPacket = packet as S_UpdateCurrencyDataAll;
        if (updateCurrencyDataAllPacket == null || updateCurrencyDataAllPacket.CurrencyData == null)
        {
            return;
        }

    }

    public static void S_RequestServerSummaryListHandler(PacketSession session, IMessage packet)
    {
        S_RequestServerSummaryList requestServerSummaryListPacket = packet as S_RequestServerSummaryList;
        if (requestServerSummaryListPacket == null)
        {
            return;
        }

    }

    // 서버에서 보낸 서버 채널 정보 패킷 처리 (다른 유저가 들어오고 나와도 실행됨)
    public static void S_RequestServerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestServerList requestServerListPacket = packet as S_RequestServerList;
        if (requestServerListPacket == null)
        {
            return;
        }

    }

    public static void S_SelectServerHandler(PacketSession session, IMessage packet)
    {
        S_SelectServer selectServerPacket = packet as S_SelectServer;
        if (selectServerPacket == null)
        {
            return;
        }
    }

    public static void S_SelectPlayerHandler(PacketSession session, IMessage packet)
    {
        S_SelectPlayer selectPlayerPacket = packet as S_SelectPlayer;
        if (selectPlayerPacket == null)
        {
            return;
        }

    }
}
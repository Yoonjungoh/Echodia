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

    // Step 1: 서버와 연결되었을 때 처리 (현재는 처리 없음, 순서상만 존재)
    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {

    }

    // Step 2: 로그인 처리
    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        C_Login loginPacket = new C_Login();

        ServerSession serverSession = session as ServerSession; 
        loginPacket.Id = $"DummyClient_Id_{serverSession.DummyId.ToString("0000")}";
        loginPacket.Password = $"DummyClient_Pw_{serverSession.DummyId.ToString("0000")}";
        serverSession.Send(loginPacket);
    }

    // Step 3: 플레이어 목록 요청 처리 (없으면 Step 4에서 만듦)
    public static void S_RequestPlayerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestPlayerList requestPlayerListPacket = packet as S_RequestPlayerList;
        if (requestPlayerListPacket == null)
        {
            return;
        }
    }

    // Step 4: 플레이어 생성 처리
    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_CreatePlayer createPlaerPacket = packet as S_CreatePlayer;
        if (createPlaerPacket == null)
        {
            return;
        }
    }

    // Step 5: 플레이어 선택 처리
    public static void S_SelectPlayerHandler(PacketSession session, IMessage packet)
    {
        S_SelectPlayer selectPlayerPacket = packet as S_SelectPlayer;
        if (selectPlayerPacket == null)
        {
            return;
        }

    }

    // Step 6: 들어갈 서버와 채널 정하기
    public static void S_SelectServerHandler(PacketSession session, IMessage packet)
    {
        S_SelectServer selectServerPacket = packet as S_SelectServer;
        if (selectServerPacket == null)
        {
            return;
        }
    }

    // Step 7: 게임 입장 처리
    public static void S_EnterGameHandler(PacketSession session, IMessage packet)
    {
        S_EnterGame enterGamePacket = packet as S_EnterGame;
        if (enterGamePacket == null)
        {
            return;
        }

    }
}
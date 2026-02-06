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

    // Step 1: 서버와 연결되었을 때 처리
    public static void S_ConnectedHandler(PacketSession session, IMessage packet)
    {
        // 연결 되고 로그인 요청하기
        C_Login loginPacket = new C_Login();
        ServerSession serverSession = session as ServerSession;

        loginPacket.Id = $"DummyClient_Id_{serverSession.DummyId.ToString("0000")}";
        loginPacket.Password = $"DummyClient_Pw_{serverSession.DummyId.ToString("0000")}";
        serverSession.Send(loginPacket);
    }

    // Step 2: 로그인 처리
    public static void S_LoginHandler(PacketSession session, IMessage packet)
    {
        S_Login loginPacket = new S_Login();
        ServerSession serverSession = session as ServerSession;

        if (loginPacket.LoginStatus == LoginStatus.SignUpSuccess)
        {
            // 회원가입 성공 시 재 로그인 요청
            C_Login reLoginPacket = new C_Login();

            reLoginPacket.Id = $"DummyClient_Id_{serverSession.DummyId.ToString("0000")}";
            reLoginPacket.Password = $"DummyClient_Pw_{serverSession.DummyId.ToString("0000")}";
            serverSession.Send(reLoginPacket);
        }
        else if (loginPacket.LoginStatus == LoginStatus.Success)
        {
            // 로그인 성공 시 서버 선택 요청
            C_SelectServer selectServetPacket = new C_SelectServer();
            selectServetPacket.ServerId = 1; // 임시로 1번 서버 선택
            selectServetPacket.ChannelId = 1; // 임시로 1번 채널 선택
            serverSession.Send(selectServetPacket);
        }
    }

    // Step 3: 들어갈 서버와 채널 정하기
    public static void S_SelectServerHandler(PacketSession session, IMessage packet)
    {
        S_SelectServer selectServerPacket = packet as S_SelectServer;
        ServerSession serverSession = session as ServerSession;

        if (selectServerPacket.EnterServerResult == EnterServerResult.Success)
        {
             // 서버 선택 성공 시 플레이어 목록 요청
            C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
            serverSession.Send(requestPlayerListPacket);
        }
    }

    // Step 4: 플레이어 목록 요청 처리 (없으면 Step 4에서 만듦)
    public static void S_RequestPlayerListHandler(PacketSession session, IMessage packet)
    {
        S_RequestPlayerList requestPlayerListPacket = packet as S_RequestPlayerList;
        ServerSession serverSession = session as ServerSession;

        // 플레이어 없으면 생성
        if (requestPlayerListPacket.PlayerInfoList.Count == 0)
        {
            C_CreatePlayer createPlayerPacket = new C_CreatePlayer();
            createPlayerPacket.Name = $"DummyClient_Player_{serverSession.DummyId}";
            serverSession.Send(createPlayerPacket);
        }
        else
        {
            // 존재하는 플레이어 중 첫번째 캐릭터 선택
            C_SelectPlayer selectPlayerPacket = new C_SelectPlayer();
            selectPlayerPacket.PlayerId = requestPlayerListPacket.PlayerInfoList[0].PlayerId;
            serverSession.PlayerId = selectPlayerPacket.PlayerId; // 선택된 플레이어 아이디 저장
        }
    }

    // Step 5: 플레이어 생성 처리
    public static void S_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        S_CreatePlayer createPlayerPacket = packet as S_CreatePlayer; 
        ServerSession serverSession = session as ServerSession;

        if (createPlayerPacket.CanCreate)
        {
            // 플레이어 생성 성공 시 플레이어 리스트 재요청
            C_RequestPlayerList requestPlayerListPacket = new C_RequestPlayerList();
            serverSession.Send(requestPlayerListPacket);
        }
    }

    // Step 5: 플레이어 선택 처리 (여기까지 오면 인게임 스폰 가능)
    public static void S_SelectPlayerHandler(PacketSession session, IMessage packet)
    {
        S_SelectPlayer selectPlayerPacket = packet as S_SelectPlayer;
        ServerSession serverSession = session as ServerSession;

        // 게임 입장 하겠다고 패킷 전송
        C_EnterGame enterGamePacket = new C_EnterGame()
        {
            PlayerId = serverSession.PlayerId,
            ServerId = serverSession.ServerId,
            ChannelId = serverSession.ChannelId,
        };
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

    internal static void S_RequestServerSummaryListHandler(PacketSession session, IMessage message)
    {
        throw new NotImplementedException();
    }

    internal static void S_RequestServerListHandler(PacketSession session, IMessage message)
    {
        throw new NotImplementedException();
    }

    internal static void S_UpdateCurrencyDataAllHandler(PacketSession session, IMessage message)
    {
        throw new NotImplementedException();
    }

    internal static void S_UpdateCurrencyDataHandler(PacketSession session, IMessage message)
    {
        throw new NotImplementedException();
    }

    internal static void S_DeletePlayerHandler(PacketSession session, IMessage message)
    {
        throw new NotImplementedException();
    }
}
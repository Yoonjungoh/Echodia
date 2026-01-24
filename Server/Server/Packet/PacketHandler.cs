using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.DB;
using Server.Game;
using ServerCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

class PacketHandler
{
    public static void C_AssignUserIdHandler(PacketSession session, IMessage packet)
    {
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        S_AssignUserId s_AssignUserId = new S_AssignUserId();
        s_AssignUserId.UserId = clientSession.MyPlayer.Id;
        clientSession.Send(s_AssignUserId);
    }

    public static void C_EnterGameHandler(PacketSession session, IMessage packet)
    {
        C_EnterGame enterGamePacket = packet as C_EnterGame;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.EnterGame(enterGamePacket.PlayerId);

        if (clientSession.MyPlayer == null)
            return;

        // 유저가 접속하려는 채널 찾기 (서버 Id도 필요)
        ServerChannel channel = ServerManager.Instance.FindChannel(enterGamePacket.ServerId, enterGamePacket.ChannelId);
        if (channel == null)
            return;

        // 게임에 입장하기 위해선 ServerId, ChannelId, MapId 모두 유효해야 함
        // MapId만 찾아주면 됨
        // TODO - DB에서 마지막으로 접속한 곳 찾아오기
        int mapId = 0;
        GameRoom gameRoom = channel.GameRoomManager.Find(mapId);
        if (gameRoom == null)
            return;
        
        gameRoom.Push(gameRoom.EnterGame, clientSession.MyPlayer);
    }

    public static void C_AttackHandler(PacketSession session, IMessage packet)
    {
        C_Attack attackPacket = packet as C_Attack;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null || player.GameRoom == null)
            return;

        player.GameRoom.Push(player.GameRoom.HandleAttack, attackPacket.InstigatorId, attackPacket.DamagedObjectId, attackPacket.AttackType);
    }

    public static void C_SpawnProjectileHandler(PacketSession session, IMessage packet)
    {
        C_SpawnProjectile spawnProjectilePacket = packet as C_SpawnProjectile;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null || player.GameRoom == null)
            return;

        player.GameRoom.Push
            (player.GameRoom.SpawnProjectile,
            spawnProjectilePacket.OwnerId, 
            spawnProjectilePacket.ProjectileType);
    }

    public static void C_MoveHandler(PacketSession session, IMessage packet)
	{
		C_Move movePacket = packet as C_Move;
		ClientSession clientSession = session as ClientSession;

		Player player = clientSession.MyPlayer;
		if (player == null)
			return;

        // 게임 룸이면 게임 룸에 전달
        GameRoom gameRoom = player.GameRoom;
        if (gameRoom != null)
        {
            gameRoom.Push(gameRoom.HandleMove, player, movePacket);
            return;
        }
	}
    
    public static void C_TimestampHandler(PacketSession session, IMessage packet)
    {
        C_Timestamp clientTimestampPacket = packet as C_Timestamp;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        NetworkManager.Instance.SendTimestamp(clientTimestampPacket, clientSession);
    }

    public static void C_ChangeCreatureStateHandler(PacketSession session, IMessage packet)
    {
        C_ChangeCreatureState changeCreatureStatePacket = packet as C_ChangeCreatureState;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null || player.GameRoom == null)
            return;

        player.GameRoom.Push(player.GameRoom.HandleChangeCreatureState, player.Id, changeCreatureStatePacket.CreatureState);
    }

    public static void C_LoginHandler(PacketSession session, IMessage packet)
    {
        C_Login loginPacket = packet as C_Login;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null || loginPacket == null)
            return;

        clientSession.HandleLogin(loginPacket);
    }

    public static void C_RequestPlayerListHandler(PacketSession session, IMessage packet)
    {
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.HandleRequestPlayerList();
    }

    public static void C_CreatePlayerHandler(PacketSession session, IMessage packet)
    {
        C_CreatePlayer createPlayerPacket = packet as C_CreatePlayer;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null || createPlayerPacket == null)
            return;

        clientSession.HandleCreatePlayer(createPlayerPacket.Name);
    }
    
    public static void C_DeletePlayerHandler(PacketSession session, IMessage packet)
    {
        C_DeletePlayer deletePlayerPacket = packet as C_DeletePlayer;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null || deletePlayerPacket == null)
            return;

        clientSession.HandleDeletePlayer(deletePlayerPacket.PlayerId);
    }

    public static void C_UpdateCurrencyDataAllHandler(PacketSession session, IMessage packet)
    {
        C_UpdateCurrencyDataAll updateCurrencyDataAllPacket = packet as C_UpdateCurrencyDataAll;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;
        
        clientSession.HandleUpdateCurrencyDataAll();
    }

    public static void C_UpdateCurrencyDataHandler(PacketSession session, IMessage packet)
    {
        C_UpdateCurrencyData updateCurrencyDataPacket = packet as C_UpdateCurrencyData;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.HandleUpdateCurrencyData(updateCurrencyDataPacket.CurrencyType);
    }

    public static void C_RequestServerSummaryListHandler(PacketSession session, IMessage packet)
    {
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession.HandleRequestServerSummaryList();
    }

    public static void C_RequestServerListHandler(PacketSession session, IMessage packet)
    {
        C_RequestServerList requestServerListPacket = packet as C_RequestServerList;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.HandleRequestServerList(requestServerListPacket.ServerId);
    }
    
    public static void C_SelectServerHandler(PacketSession session, IMessage packet)
    {
        C_SelectServer selectServerPacket = packet as C_SelectServer;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.HandleSelectServer(selectServerPacket.ServerId, selectServerPacket.ChannelId);
    }


    public static void C_SelectPlayerHandler(PacketSession session, IMessage packet)
    {
        C_SelectPlayer selectPlayerPacket = packet as C_SelectPlayer;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.HandleSelectPlayer(selectPlayerPacket.PlayerId, selectPlayerPacket.ServerId, selectPlayerPacket.ChannelId);
    }
}

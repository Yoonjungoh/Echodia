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
using static System.Collections.Specialized.BitVector32;

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

        Player user = clientSession.MyPlayer;
        if (user == null || user.Lobby == null || user.WaitingRoom == null)
            return;

        // 해당 유저를 방에 추가하기
        GameRoom gameRoom = user.Lobby.GameRoomManager.Find(enterGamePacket.RoomId);
        if (gameRoom == null)
            return;

        gameRoom.Push(gameRoom.EnterGame, user);
    }

    public static void C_AttackHandler(PacketSession session, IMessage packet)
    {
        C_Attack attackPacket = packet as C_Attack;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null || player.Lobby == null || player.WaitingRoom == null || player.GameRoom == null)
            return;

        player.GameRoom.Push(player.GameRoom.HandleAttack, attackPacket.InstigatorId, attackPacket.DamagedObjectId, attackPacket.AttackType);
    }

    public static void C_SpawnProjectileHandler(PacketSession session, IMessage packet)
    {
        C_SpawnProjectile spawnProjectilePacket = packet as C_SpawnProjectile;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null || player.Lobby == null || player.WaitingRoom == null || player.GameRoom == null)
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

        WaitingRoom waitingRoom = player.WaitingRoom;
        if (waitingRoom != null)
        {
            waitingRoom.Push(waitingRoom.HandleMove, player, movePacket);
            return;
        }
	}

    public static void C_LeaveLobbyHandler(PacketSession session, IMessage packet)
    {
        C_LeaveLobby leaveLobbyPacket = packet as C_LeaveLobby;
        ClientSession clientSession = session as ClientSession;

        Player user = clientSession.MyPlayer;
        if (user == null || user.Lobby == null)
            return;

        user.Lobby.Push(user.Lobby.LeaveLobby, user.Id);
    }

    public static void C_AddRoomHandler(PacketSession session, IMessage packet)
    {
        C_AddRoom addRoomPacket = packet as C_AddRoom;
        ClientSession clientSession = session as ClientSession;

        Player user = clientSession.MyPlayer;
        if (user == null || user.Lobby == null)
            return;

        user.Lobby.Push(user.Lobby.HandleAddRoom, user, addRoomPacket.RoomName);
    }

    public static void C_ExitRoomHandler(PacketSession session, IMessage packet)
    {
        C_ExitRoom exitRoomPacket = packet as C_ExitRoom;
        ClientSession clientSession = session as ClientSession;

        Player user = clientSession.MyPlayer;
        if (user == null || user.Lobby == null || user.WaitingRoom == null)
            return;

        user.Lobby.Push(user.WaitingRoom.LeaveRoom, user.Id);
    }

    public static void C_EnterLobbyHandler(PacketSession session, IMessage packet)
    {
        C_EnterLobby enterLobbyPacket = packet as C_EnterLobby;
        ClientSession clientSession = session as ClientSession;

        // 유저 생성
        Player user = clientSession.EnterLobby(enterLobbyPacket.PlayerId);
        if (clientSession.MyPlayer == null)
            return;
        
        LobbyManager.Instance.EnterLobby(1, user);	// TODO - 1번 로비로 강제 이동
    }

    public static void C_StartGameHandler(PacketSession session, IMessage packet)
    {
        C_StartGame startGamePacket = packet as C_StartGame;
        ClientSession clientSession = session as ClientSession;

        Player user = clientSession.MyPlayer;
        if (user == null || user.Lobby == null)
            return;

        user.WaitingRoom.Push(user.WaitingRoom.StartGame, user.Id, startGamePacket.RoomId);
    }

    public static void C_EnterWaitingRoomHandler(PacketSession session, IMessage packet)
    {
        C_EnterWaitingRoom enterWaitingRoomPacket = packet as C_EnterWaitingRoom;
        ClientSession clientSession = session as ClientSession;

        Player user = clientSession.MyPlayer;
        if (user == null || user.Lobby == null)
            return;

        // 로비에 해당 유저를 먼저 떠나게 하고
        user.Lobby.Push(user.Lobby.LeaveLobby, user.Id);

        // 해당 유저를 방에 추가하기
        WaitingRoom watingRoom = user.Lobby.WaitingRoomManager.Find(enterWaitingRoomPacket.RoomId);
        if (watingRoom == null)
            return;

        // 방 인원 수 초과 체크
        if (watingRoom.CanEnterWaitingRoom == false)
            return;

        watingRoom.Push(watingRoom.EnterRoom, user);
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
        if (player == null || player.Lobby == null || player.GameRoom == null)
            return;

        player.GameRoom.Push(player.GameRoom.HandleChangeCreatureState, player.Id, changeCreatureStatePacket.CreatureState);
    }

    public static void C_StartCountdownHandler(PacketSession session, IMessage packet)
    {
        C_StartCountdown startCountdownPacket = packet as C_StartCountdown;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession.MyPlayer;
        if (player == null || player.Lobby == null || player.GameRoom == null)
            return;

        // 방에 인원 가득차면 게임 스타트를 위한 카운트다운 시작
        if (player.GameRoom.IsRoomFull)
        {
            player.GameRoom.Push(player.GameRoom.HandleStartCountdown, player);
        }
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
        C_RequestPlayerList requestPlayerList = packet as C_RequestPlayerList;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.HandleRequestPlayerList(requestPlayerList);
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
        
        clientSession.HandleUpdateCurrencyDataAll(updateCurrencyDataAllPacket.PlayerId);
    }

    public static void C_UpdateCurrencyDataHandler(PacketSession session, IMessage packet)
    {
        C_UpdateCurrencyData updateCurrencyDataPacket = packet as C_UpdateCurrencyData;
        ClientSession clientSession = session as ClientSession;
        
        if (clientSession == null)
            return;

        clientSession.HandleUpdateCurrencyData(updateCurrencyDataPacket.PlayerId, updateCurrencyDataPacket.CurrencyType);
    }
}

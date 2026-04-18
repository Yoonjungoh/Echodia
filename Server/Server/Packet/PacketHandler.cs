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
using System.Numerics;
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
        // 여기까지 온거면 클라이언트는 GameRoomScene에 있음
        C_EnterGame enterGamePacket = packet as C_EnterGame;
        ClientSession clientSession = session as ClientSession;

        if (clientSession == null)
            return;

        clientSession.EnterGame(enterGamePacket.PlayerId);
        // 새로 만든 캐릭터가 접속할 경우 첫 퀘스트 부여
        clientSession.CheckAndAssignInitialQuest();

        if (clientSession.MyPlayer == null)
            return;

        // 유저가 접속하려는 채널 찾기 (서버 Id도 필요)
        ServerChannel channel = ServerManager.Instance.FindChannel(enterGamePacket.ServerId, enterGamePacket.ChannelId);
        if (channel == null)
            return;

        // 마지막으로 접속했던 맵 Id로 룸 찾기 (DB에 없으면 DefaultStartMapId 사용)
        int mapId = ObjectManager.Instance.GetPlayerLastLogoutMapId(clientSession.MyPlayer.PlayerId);
        GameRoom gameRoom = channel.GameRoomManager.Find(mapId);
        if (gameRoom == null)
        {
            // 저장된 맵이 현재 없으면 기본 맵으로 폴백
            mapId = DataManager.Instance.DefaultStartMapId;
            gameRoom = channel.GameRoomManager.Find(mapId);
            if (gameRoom == null)
                return;
        }

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

    public static void C_RequestInitGameRoomDataHandler(PacketSession session, IMessage packet)
    {
        C_RequestInitGameRoomData requestInitGameroomDataPacket = packet as C_RequestInitGameRoomData;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession.HandleRequestInitGameRoomData(clientSession.MyPlayer.PlayerId);
    }

    public static void C_RequestQuestDataHandler(PacketSession session, IMessage packet)
    {
        C_RequestQuestData requestInitGameroomDataPacket = packet as C_RequestQuestData;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession.SendQuestList();  // DB에 있는 퀘스트 리스트 보내기
    }

    public static void C_AcceptQuestHandler(PacketSession session, IMessage packet)
    {
        C_AcceptQuest acceptQuestPacket = packet as C_AcceptQuest;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession.AcceptQuest(acceptQuestPacket.MainQuestId, acceptQuestPacket.SubQuestId);
    }

    public static void C_CompleteQuestHandler(PacketSession session, IMessage packet)
    {
        C_CompleteQuest completeQuestPacket = packet as C_CompleteQuest;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession.ClaimQuestReward(completeQuestPacket.MainQuestId, completeQuestPacket.SubQuestId);
    }

    public static void C_UseItemHandler(PacketSession session, IMessage packet)
    {
        C_UseItem useItemPacket = packet as C_UseItem;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession?.MyPlayer?.HandleUseItem(useItemPacket.ItemType, useItemPacket.SlotIndex);
    }

    public static void C_PickUpDropItemHandler(PacketSession session, IMessage packet)
    {
        C_PickUpDropItem pickUpDropItemPacket = packet as C_PickUpDropItem;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        Player player = clientSession.MyPlayer;
        if (player?.GameRoom == null)
            return;

        player.GameRoom.Push(player.HandlePickUpDropItem, pickUpDropItemPacket.ItemId);
    }

    public static void C_PongHandler(PacketSession session, IMessage packet)
    {
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession.OnPongReceived();
    }

    public static void C_EquipItemHandler(PacketSession session, IMessage packet)
    {
        C_EquipItem equipItemPacket = packet as C_EquipItem;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession?.MyPlayer?.HandleEquipItem(equipItemPacket.SlotIndex);
    }

    public static void C_UnequipItemHandler(PacketSession session, IMessage packet)
    {
        C_UnequipItem unEquipItemPacket = packet as C_UnequipItem;
        ClientSession clientSession = session as ClientSession;
        if (clientSession == null)
            return;

        clientSession?.MyPlayer?.HandleUnEquipItem(unEquipItemPacket.SlotType);
    }

    public static void C_RequestMapTransferHandler(PacketSession session, IMessage packet)
    {
        C_RequestMapTransfer transferPacket = packet as C_RequestMapTransfer;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession?.MyPlayer;
        if (player == null || player.GameRoom == null)
            return;

        int currentMapId = player.GameRoom.MapId;
        (int prevMapId, int nextMapId) = DataManager.Instance.GetAdjacentMaps(currentMapId);

        int targetMapId;
        Vector3 spawnPos;

        if (transferPacket.TransferPoint == MapTransferPoint.MapTransferLeavePoint)
        {
            // LeavePoint -> 다음 맵의 EnterPoint에 스폰
            targetMapId = nextMapId;
            MapData targetMap = MapManager.Instance.CreateCopy(targetMapId);
            if (targetMapId < 0 || targetMap?.EnterPoint == null)
            {
                ConsoleLogManager.Instance.Log($"[MapTransfer] No next map from mapId={currentMapId}");
                return;
            }
            spawnPos = targetMap.EnterPoint.Position;
        }
        else // MapTransferEnterPoint
        {
            // EnterPoint -> 이전 맵의 LeavePoint에 스폰
            targetMapId = prevMapId;
            MapData targetMap = MapManager.Instance.CreateCopy(targetMapId);
            if (targetMapId < 0 || targetMap?.LeavePoint == null)
            {
                ConsoleLogManager.Instance.Log($"[MapTransfer] No prev map from mapId={currentMapId}");
                return;
            }
            spawnPos = targetMap.LeavePoint.Position;
        }

        ConsoleLogManager.Instance.Log($"[MapTransfer] Player {player.Id}: map {currentMapId} → {targetMapId}, spawnPos={spawnPos}");
        player.GameRoom.Push(player.GameRoom.TransferPlayer, player.Id, targetMapId, spawnPos);
    }

    public static void C_UseSkillHandler(PacketSession session, IMessage packet)
    {
        C_UseSkill useSkillPacket = packet as C_UseSkill;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession?.MyPlayer;
        if (player == null || player.GameRoom == null)
            return;

        player.GameRoom.Push(player.GameRoom.HandleUseSkill, player.Id, useSkillPacket.SkillId);
    }

    public static void C_StopChannelHandler(PacketSession session, IMessage packet)
    {
        C_StopChannel stopChannelPacket = packet as C_StopChannel;
        ClientSession clientSession = session as ClientSession;

        Player player = clientSession?.MyPlayer;
        if (player == null || player.GameRoom == null)
            return;

        player.GameRoom.Push(() =>
        {
            player.SkillExecutor.StopChannel(stopChannelPacket.SkillId, stopChannelPacket.ElapsedMs);
        });
    }
}

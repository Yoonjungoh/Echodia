using Google.Protobuf;
using Google.Protobuf.Protocol;
using ServerCore;
using System;
using System.Collections.Generic;

class PacketManager
{
	#region Singleton
	static PacketManager _instance = new PacketManager();
	public static PacketManager Instance { get { return _instance; } }
	#endregion

	PacketManager()
	{
		Register();
	}

	Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>> _onRecv = new Dictionary<ushort, Action<PacketSession, ArraySegment<byte>, ushort>>();
	Dictionary<ushort, Action<PacketSession, IMessage>> _handler = new Dictionary<ushort, Action<PacketSession, IMessage>>();
		
	public Action<PacketSession, IMessage, ushort> CustomHandler { get; set; }

	public void Register()
	{		
		_onRecv.Add((ushort)MsgId.CAssignUserId, MakePacket<C_AssignUserId>);
		_handler.Add((ushort)MsgId.CAssignUserId, PacketHandler.C_AssignUserIdHandler);		
		_onRecv.Add((ushort)MsgId.CEnterGame, MakePacket<C_EnterGame>);
		_handler.Add((ushort)MsgId.CEnterGame, PacketHandler.C_EnterGameHandler);		
		_onRecv.Add((ushort)MsgId.CAttack, MakePacket<C_Attack>);
		_handler.Add((ushort)MsgId.CAttack, PacketHandler.C_AttackHandler);		
		_onRecv.Add((ushort)MsgId.CMove, MakePacket<C_Move>);
		_handler.Add((ushort)MsgId.CMove, PacketHandler.C_MoveHandler);		
		_onRecv.Add((ushort)MsgId.CSpawnProjectile, MakePacket<C_SpawnProjectile>);
		_handler.Add((ushort)MsgId.CSpawnProjectile, PacketHandler.C_SpawnProjectileHandler);		
		_onRecv.Add((ushort)MsgId.CTimestamp, MakePacket<C_Timestamp>);
		_handler.Add((ushort)MsgId.CTimestamp, PacketHandler.C_TimestampHandler);		
		_onRecv.Add((ushort)MsgId.CChangeCreatureState, MakePacket<C_ChangeCreatureState>);
		_handler.Add((ushort)MsgId.CChangeCreatureState, PacketHandler.C_ChangeCreatureStateHandler);		
		_onRecv.Add((ushort)MsgId.CLogin, MakePacket<C_Login>);
		_handler.Add((ushort)MsgId.CLogin, PacketHandler.C_LoginHandler);		
		_onRecv.Add((ushort)MsgId.CRequestPlayerList, MakePacket<C_RequestPlayerList>);
		_handler.Add((ushort)MsgId.CRequestPlayerList, PacketHandler.C_RequestPlayerListHandler);		
		_onRecv.Add((ushort)MsgId.CCreatePlayer, MakePacket<C_CreatePlayer>);
		_handler.Add((ushort)MsgId.CCreatePlayer, PacketHandler.C_CreatePlayerHandler);		
		_onRecv.Add((ushort)MsgId.CDeletePlayer, MakePacket<C_DeletePlayer>);
		_handler.Add((ushort)MsgId.CDeletePlayer, PacketHandler.C_DeletePlayerHandler);		
		_onRecv.Add((ushort)MsgId.CUpdateCurrencyData, MakePacket<C_UpdateCurrencyData>);
		_handler.Add((ushort)MsgId.CUpdateCurrencyData, PacketHandler.C_UpdateCurrencyDataHandler);		
		_onRecv.Add((ushort)MsgId.CUpdateCurrencyDataAll, MakePacket<C_UpdateCurrencyDataAll>);
		_handler.Add((ushort)MsgId.CUpdateCurrencyDataAll, PacketHandler.C_UpdateCurrencyDataAllHandler);		
		_onRecv.Add((ushort)MsgId.CSelectServer, MakePacket<C_SelectServer>);
		_handler.Add((ushort)MsgId.CSelectServer, PacketHandler.C_SelectServerHandler);		
		_onRecv.Add((ushort)MsgId.CRequestServerList, MakePacket<C_RequestServerList>);
		_handler.Add((ushort)MsgId.CRequestServerList, PacketHandler.C_RequestServerListHandler);		
		_onRecv.Add((ushort)MsgId.CRequestServerSummaryList, MakePacket<C_RequestServerSummaryList>);
		_handler.Add((ushort)MsgId.CRequestServerSummaryList, PacketHandler.C_RequestServerSummaryListHandler);		
		_onRecv.Add((ushort)MsgId.CSelectPlayer, MakePacket<C_SelectPlayer>);
		_handler.Add((ushort)MsgId.CSelectPlayer, PacketHandler.C_SelectPlayerHandler);		
		_onRecv.Add((ushort)MsgId.CRequestInitGameRoomData, MakePacket<C_RequestInitGameRoomData>);
		_handler.Add((ushort)MsgId.CRequestInitGameRoomData, PacketHandler.C_RequestInitGameRoomDataHandler);		
		_onRecv.Add((ushort)MsgId.CAcceptQuest, MakePacket<C_AcceptQuest>);
		_handler.Add((ushort)MsgId.CAcceptQuest, PacketHandler.C_AcceptQuestHandler);		
		_onRecv.Add((ushort)MsgId.CCompleteQuest, MakePacket<C_CompleteQuest>);
		_handler.Add((ushort)MsgId.CCompleteQuest, PacketHandler.C_CompleteQuestHandler);		
		_onRecv.Add((ushort)MsgId.CRequestQuestData, MakePacket<C_RequestQuestData>);
		_handler.Add((ushort)MsgId.CRequestQuestData, PacketHandler.C_RequestQuestDataHandler);		
		_onRecv.Add((ushort)MsgId.CUseItem, MakePacket<C_UseItem>);
		_handler.Add((ushort)MsgId.CUseItem, PacketHandler.C_UseItemHandler);		
		_onRecv.Add((ushort)MsgId.CPickUpDropItem, MakePacket<C_PickUpDropItem>);
		_handler.Add((ushort)MsgId.CPickUpDropItem, PacketHandler.C_PickUpDropItemHandler);		
		_onRecv.Add((ushort)MsgId.CPong, MakePacket<C_Pong>);
		_handler.Add((ushort)MsgId.CPong, PacketHandler.C_PongHandler);		
		_onRecv.Add((ushort)MsgId.CEquipItem, MakePacket<C_EquipItem>);
		_handler.Add((ushort)MsgId.CEquipItem, PacketHandler.C_EquipItemHandler);		
		_onRecv.Add((ushort)MsgId.CUnequipItem, MakePacket<C_UnequipItem>);
		_handler.Add((ushort)MsgId.CUnequipItem, PacketHandler.C_UnequipItemHandler);		
		_onRecv.Add((ushort)MsgId.CRequestMapTransfer, MakePacket<C_RequestMapTransfer>);
		_handler.Add((ushort)MsgId.CRequestMapTransfer, PacketHandler.C_RequestMapTransferHandler);		
		_onRecv.Add((ushort)MsgId.CUseSkill, MakePacket<C_UseSkill>);
		_handler.Add((ushort)MsgId.CUseSkill, PacketHandler.C_UseSkillHandler);
		_onRecv.Add((ushort)MsgId.CStopChannel, MakePacket<C_StopChannel>);
		_handler.Add((ushort)MsgId.CStopChannel, PacketHandler.C_StopChannelHandler);
	}

	public void OnRecvPacket(PacketSession session, ArraySegment<byte> buffer)
	{
		ushort count = 0;

		ushort size = BitConverter.ToUInt16(buffer.Array, buffer.Offset);
		count += 2;
		ushort id = BitConverter.ToUInt16(buffer.Array, buffer.Offset + count);
		count += 2;

		Action<PacketSession, ArraySegment<byte>, ushort> action = null;
		if (_onRecv.TryGetValue(id, out action))
			action.Invoke(session, buffer, id);
	}

	void MakePacket<T>(PacketSession session, ArraySegment<byte> buffer, ushort id) where T : IMessage, new()
	{
		T pkt = new T();
		pkt.MergeFrom(buffer.Array, buffer.Offset + 4, buffer.Count - 4);

		if (CustomHandler != null)
        {
			CustomHandler.Invoke(session, pkt, id);	
		}
        else
		{
			Action<PacketSession, IMessage> action = null;
			if (_handler.TryGetValue(id, out action))
				action.Invoke(session, pkt);
		}
	}

	public Action<PacketSession, IMessage> GetPacketHandler(ushort id)
	{
		Action<PacketSession, IMessage> action = null;
		if (_handler.TryGetValue(id, out action))
			return action;
		return null;
	}
}